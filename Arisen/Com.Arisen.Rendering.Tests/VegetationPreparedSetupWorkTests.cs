using System.Collections.Concurrent;
using System.Numerics;
using System.Security.Cryptography;
using Arisen.Native.RHI;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Threading;
using ArisenEngine.Vegetation;
using ArisenEngine.Vegetation.Assets;
using ArisenEngine.Vegetation.GenericRenderPipeline;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationPreparedSetupWorkTests
{
    private static readonly Guid s_Species =
        Guid.Parse("73000000-0000-0000-0000-000000000001");
    private static readonly Guid s_Biome =
        Guid.Parse("74000000-0000-0000-0000-000000000001");
    private const string PackageId = "com.arisen.vegetation.tests";
    private const int ClusterCount = 520;
    private const int PageInstanceCount = 2;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(255, 1)]
    [InlineData(256, 1)]
    [InlineData(257, 2)]
    [InlineData(512, 2)]
    [InlineData(513, 3)]
    [InlineData(1024, 4)]
    public void PartitionReportsBoundedWorkItemCounts(int itemCount, int expectedWorkItems)
    {
        Assert.Equal(expectedWorkItems, VegetationSetupWorkPartition.GetWorkItemCount(itemCount));
    }

    [Fact]
    public void PartitionRangesCoverEveryInputExactlyOnce()
    {
        const int itemCount = 1000;
        int workItemCount = VegetationSetupWorkPartition.GetWorkItemCount(itemCount);
        int nextStart = 0;
        for (int workItemIndex = 0; workItemIndex < workItemCount; workItemIndex++)
        {
            Assert.True(VegetationSetupWorkPartition.TryGetRange(
                itemCount,
                workItemIndex,
                out int start,
                out int count));
            Assert.Equal(nextStart, start);
            Assert.InRange(count, 1, VegetationSetupWorkPartition.MaximumInputsPerWorkItem);
            nextStart += count;
        }

        Assert.Equal(itemCount, nextStart);
    }

    [Fact]
    public void PartitionRejectsOutOfRangeWorkItems()
    {
        Assert.False(VegetationSetupWorkPartition.TryGetRange(0, 0, out _, out _));
        Assert.False(VegetationSetupWorkPartition.TryGetRange(-1, 0, out _, out _));
        Assert.False(VegetationSetupWorkPartition.TryGetRange(257, -1, out _, out _));
        Assert.False(VegetationSetupWorkPartition.TryGetRange(256, 1, out _, out _));
        Assert.False(VegetationSetupWorkPartition.TryGetRange(257, 2, out _, out _));

        Assert.True(VegetationSetupWorkPartition.TryGetRange(257, 1, out int start, out int count));
        Assert.Equal(256, start);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ShardBufferMergesRegionsInRegionOrderAndRejectsOverflow()
    {
        var buffer = new VegetationSetupShardBuffer<int>();
        buffer.EnsureRegions(regionCount: 3, capacityPerRegion: 4);
        Span<int> first = buffer.GetRegion(0);
        first[0] = 10;
        first[1] = 11;
        buffer.SetCount(0, 2);
        buffer.SetCount(1, 0);
        Span<int> third = buffer.GetRegion(2);
        third[0] = 30;
        buffer.SetCount(2, 1);

        var destination = new int[8];
        int merged = buffer.MergeInto(destination);

        Assert.Equal(3, merged);
        Assert.Equal(new[] { 10, 11, 30 }, destination.AsSpan(0, merged).ToArray());
        Assert.Throws<InvalidOperationException>(() => buffer.SetCount(0, 5));
        Assert.Throws<InvalidOperationException>(() => buffer.MergeInto(new int[2]));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetRegion(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.EnsureRegions(-1, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.EnsureRegions(1, 0));
    }

    [Fact]
    public void DispatcherRunsInlineWithoutTaskSystemAndReportsWorkItemCount()
    {
        var dispatcher = new VegetationSetupWorkDispatcher(taskSystem: null);
        int callerThread = Environment.CurrentManagedThreadId;
        var visited = new List<int>();
        var threads = new List<int>();

        int dispatched = dispatcher.Dispatch(600, workItemIndex =>
        {
            visited.Add(workItemIndex);
            threads.Add(Environment.CurrentManagedThreadId);
        });

        Assert.Equal(3, dispatched);
        Assert.Equal(new[] { 0, 1, 2 }, visited);
        Assert.All(threads, thread => Assert.Equal(callerThread, thread));
        Assert.Equal(
            0,
            dispatcher.Dispatch(
                0,
                _ => throw new InvalidOperationException("Empty dispatches must not run work.")));
    }

    [Fact]
    public void DispatcherRunsWorkItemsOnTaskGraphWorkers()
    {
        using var taskGraph = new TaskGraph(workerCount: 4);
        var dispatcher = new VegetationSetupWorkDispatcher(taskGraph);
        int callerThread = Environment.CurrentManagedThreadId;
        var threads = new int[8];

        int dispatched = dispatcher.Dispatch(8 * 256, workItemIndex =>
            threads[workItemIndex] = Environment.CurrentManagedThreadId);

        Assert.Equal(8, dispatched);
        Assert.All(threads, thread => Assert.NotEqual(0, thread));
        Assert.Contains(threads, thread => thread != callerThread);
    }

    [Fact]
    public void DispatcherGrowsAndReusesWorkItemTasksAcrossFrames()
    {
        using var taskGraph = new TaskGraph(workerCount: 2);
        var dispatcher = new VegetationSetupWorkDispatcher(taskGraph);
        var runs = new int[16];
        int[] itemCounts = [700, 1600, 300];

        foreach (int itemCount in itemCounts)
        {
            int expectedWorkItems = VegetationSetupWorkPartition.GetWorkItemCount(itemCount);
            Assert.Equal(
                expectedWorkItems,
                dispatcher.Dispatch(
                    itemCount,
                    workItemIndex => Interlocked.Increment(ref runs[workItemIndex])));
        }

        for (int workItemIndex = 0; workItemIndex < runs.Length; workItemIndex++)
        {
            int expected = 0;
            foreach (int itemCount in itemCounts)
            {
                if (workItemIndex < VegetationSetupWorkPartition.GetWorkItemCount(itemCount))
                {
                    expected++;
                }
            }

            Assert.Equal(expected, runs[workItemIndex]);
        }
    }

    [Fact]
    public void DispatcherRejectsReentrantDispatchAndRecovers()
    {
        var dispatcher = new VegetationSetupWorkDispatcher(taskSystem: null);

        Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(
            1,
            _ => dispatcher.Dispatch(1, _ => { })));

        int dispatched = dispatcher.Dispatch(1, _ => { });
        Assert.Equal(1, dispatched);
    }

    [Fact]
    public void WholeRangeSetupMatchesShardedSetupForOrderedOutput()
    {
        Fixture fixture = CreateFixture(ClusterCount);
        SetupResult whole = RunWholeRange(fixture);
        SetupResult sharded = RunSharded(fixture, new VegetationSetupWorkDispatcher(taskSystem: null));

        Assert.Equal(fixture.ExpectedGatheredCount, whole.InputCount);
        Assert.Equal(fixture.ExpectedPreparedCount, whole.FrameCount);
        Assert.True(
            whole.InputCount > VegetationSetupWorkPartition.MaximumInputsPerWorkItem,
            $"Expected more than one setup shard, gathered {whole.InputCount} inputs.");
        AssertGatheredIdentical(whole, sharded);
        AssertFramesIdentical(whole, sharded);
    }

    [Fact]
    public void ShardedSetupMatchesAcrossInlineAndTaskGraphDispatch()
    {
        Fixture fixture = CreateFixture(ClusterCount);
        SetupResult inline = RunSharded(
            fixture,
            new VegetationSetupWorkDispatcher(taskSystem: null));
        int callerThread = Environment.CurrentManagedThreadId;
        var observedThreads = new ConcurrentBag<int>();
        using var taskGraph = new TaskGraph(workerCount: 4);
        SetupResult parallel = RunSharded(
            fixture,
            new VegetationSetupWorkDispatcher(taskGraph),
            observedThreads);

        AssertGatheredIdentical(inline, parallel);
        AssertFramesIdentical(inline, parallel);
        Assert.Contains(observedThreads, thread => thread != callerThread);
    }

    private static void AssertGatheredIdentical(SetupResult expected, SetupResult actual)
    {
        Assert.Equal(expected.InputCount, actual.InputCount);
        for (int index = 0; index < expected.InputCount; index++)
        {
            VegetationClusterCullingInput expectedInput = expected.Inputs[index];
            VegetationClusterCullingInput actualInput = actual.Inputs[index];
            Assert.Equal(expectedInput.Component.ClusterGuid, actualInput.Component.ClusterGuid);
            Assert.Equal(expectedInput.Resident.Guid, actualInput.Resident.Guid);
            Assert.Equal(expectedInput.Resident.Generation, actualInput.Resident.Generation);
            Assert.Equal(expectedInput.Prepared.ClusterGuid, actualInput.Prepared.ClusterGuid);
            Assert.Equal(expectedInput.Prepared.Generation, actualInput.Prepared.Generation);
            Assert.Equal(expectedInput.Resident.Origin, actualInput.Resident.Origin);
        }
    }

    private static void AssertFramesIdentical(SetupResult expected, SetupResult actual)
    {
        Assert.Equal(expected.FrameCount, actual.FrameCount);
        for (int index = 0; index < expected.FrameCount; index++)
        {
            VegetationPreparedClusterFrame expectedFrame = expected.Frames[index];
            VegetationPreparedClusterFrame actualFrame = actual.Frames[index];
            Assert.Equal(expectedFrame.Component.ClusterGuid, actualFrame.Component.ClusterGuid);
            Assert.Equal(expectedFrame.Prepared.ClusterGuid, actualFrame.Prepared.ClusterGuid);
            Assert.Equal(expectedFrame.Prepared.Generation, actualFrame.Prepared.Generation);
            Assert.Equal(expectedFrame.Selection.ClusterGuid, actualFrame.Selection.ClusterGuid);
            Assert.Equal(expectedFrame.Selection.SpeciesGuid, actualFrame.Selection.SpeciesGuid);
            Assert.Equal(expectedFrame.Selection.LodLevel, actualFrame.Selection.LodLevel);
            Assert.Equal(expectedFrame.Selection.DistanceSquared, actualFrame.Selection.DistanceSquared);
            Assert.Equal(expectedFrame.Selection.Accepted, actualFrame.Selection.Accepted);
        }
    }

    private static SetupResult RunWholeRange(Fixture fixture)
    {
        VegetationClusterComponent[] extracted = fixture.Extracted.ToArray();
        VegetationResidentClusterData[] residents = fixture.Residents.ToArray();
        VegetationCullingSelection[] selections = fixture.Selections.ToArray();
        var inputs = new VegetationClusterCullingInput[extracted.Length];
        int inputCount = VegetationPreparedSetup.GatherCullingInputs(
            extracted,
            0,
            extracted.Length,
            residents,
            fixture.Prepared,
            inputs);
        var frames = new VegetationPreparedClusterFrame[inputCount];
        int frameCount = VegetationPreparedSetup.BuildPreparedFrames(
            new ReadOnlySpan<VegetationClusterCullingInput>(inputs, 0, inputCount),
            0,
            inputCount,
            selections,
            frames);
        return new SetupResult(inputs, inputCount, frames, frameCount);
    }

    private static SetupResult RunSharded(
        Fixture fixture,
        VegetationSetupWorkDispatcher dispatcher,
        ConcurrentBag<int>? observedThreads = null)
    {
        VegetationClusterComponent[] extracted = fixture.Extracted.ToArray();
        VegetationResidentClusterData[] residents = fixture.Residents.ToArray();
        VegetationCullingSelection[] selections = fixture.Selections.ToArray();
        var inputRegions = new VegetationSetupShardBuffer<VegetationClusterCullingInput>();
        inputRegions.EnsureRegions(
            VegetationSetupWorkPartition.GetWorkItemCount(extracted.Length),
            VegetationSetupWorkPartition.MaximumInputsPerWorkItem);
        dispatcher.Dispatch(extracted.Length, workItemIndex =>
        {
            observedThreads?.Add(Environment.CurrentManagedThreadId);
            if (!VegetationSetupWorkPartition.TryGetRange(
                    extracted.Length,
                    workItemIndex,
                    out int start,
                    out int count))
            {
                throw new InvalidOperationException(
                    $"Gather work item '{workItemIndex}' is outside the extracted range.");
            }

            int written = VegetationPreparedSetup.GatherCullingInputs(
                extracted,
                start,
                count,
                residents,
                fixture.Prepared,
                inputRegions.GetRegion(workItemIndex));
            inputRegions.SetCount(workItemIndex, written);
        });

        var inputs = new VegetationClusterCullingInput[extracted.Length];
        int inputCount = inputRegions.MergeInto(inputs);
        var frameRegions = new VegetationSetupShardBuffer<VegetationPreparedClusterFrame>();
        frameRegions.EnsureRegions(
            VegetationSetupWorkPartition.GetWorkItemCount(inputCount),
            VegetationSetupWorkPartition.MaximumInputsPerWorkItem);
        dispatcher.Dispatch(inputCount, workItemIndex =>
        {
            observedThreads?.Add(Environment.CurrentManagedThreadId);
            if (!VegetationSetupWorkPartition.TryGetRange(
                    inputCount,
                    workItemIndex,
                    out int start,
                    out int count))
            {
                throw new InvalidOperationException(
                    $"Prepare work item '{workItemIndex}' is outside the gathered range.");
            }

            int written = VegetationPreparedSetup.BuildPreparedFrames(
                new ReadOnlySpan<VegetationClusterCullingInput>(inputs, 0, inputCount),
                start,
                count,
                selections,
                frameRegions.GetRegion(workItemIndex));
            frameRegions.SetCount(workItemIndex, written);
        });

        var frames = new VegetationPreparedClusterFrame[inputCount];
        int frameCount = frameRegions.MergeInto(frames);
        return new SetupResult(inputs, inputCount, frames, frameCount);
    }

    private static Fixture CreateFixture(int clusterCount)
    {
        var fixture = new Fixture();
        var gatheredGuids = new List<Guid>();
        for (int index = 0; index < clusterCount; index++)
        {
            Guid clusterGuid = CreateGuid(0x71, index);
            Guid pageGuid = CreateGuid(0x72, index);
            var origin = new WorldPosition(index * 4.0, 0.0, 0.0);
            ulong generation = (ulong)(index + 1);
            bool hasResident = index % 7 != 6;
            bool componentMatchesResident = index % 13 != 5;
            bool hasPreparedView = index % 11 != 4;

            fixture.Extracted.Add(new VegetationClusterComponent
            {
                ClusterGuid = clusterGuid,
                BiomeGuid = s_Biome,
                SpeciesGuid = s_Species,
                OriginX = origin.X,
                OriginY = origin.Y,
                OriginZ = origin.Z,
                PageCount = 1,
                InstanceCount = componentMatchesResident
                    ? PageInstanceCount
                    : PageInstanceCount + 1,
                Flags = VegetationClusterFlags.Visible | VegetationClusterFlags.ReceiveShadows
            });
            if (hasResident)
            {
                fixture.Residents.Add(CreateResident(
                    clusterGuid,
                    pageGuid,
                    origin,
                    PageInstanceCount,
                    generation));
            }

            if (hasPreparedView)
            {
                fixture.Prepared.Add(CreateView(
                    clusterGuid,
                    generation,
                    origin,
                    PageInstanceCount));
            }

            if (hasResident && componentMatchesResident && hasPreparedView)
            {
                fixture.ExpectedGatheredCount++;
                gatheredGuids.Add(clusterGuid);
            }
        }

        fixture.Residents.Sort(static (left, right) => left.Guid.CompareTo(right.Guid));
        for (int gatheredIndex = 0; gatheredIndex < gatheredGuids.Count; gatheredIndex++)
        {
            bool hasSelection = gatheredIndex % 11 != 7;
            bool accepted = gatheredIndex % 3 != 0;
            if (!hasSelection)
            {
                continue;
            }

            Guid clusterGuid = gatheredGuids[gatheredIndex];
            fixture.Selections.Add(new VegetationCullingSelection(
                clusterGuid,
                generation: (ulong)(gatheredIndex + 1),
                s_Species,
                lodLevel: 0,
                visiblePageCount: 1,
                visibleInstanceCount: PageInstanceCount,
                budgetInstanceCount: PageInstanceCount,
                distanceSquared: 16.0,
                screenSpaceError: 0.5,
                accepted));
            if (accepted)
            {
                fixture.ExpectedPreparedCount++;
            }
        }

        fixture.Selections.Sort(static (left, right) => left.ClusterGuid.CompareTo(right.ClusterGuid));
        return fixture;
    }

    private static VegetationResidentClusterData CreateResident(
        Guid clusterGuid,
        Guid pageGuid,
        WorldPosition origin,
        int instanceCount,
        ulong generation)
    {
        var instances = new CookedVegetationInstance[instanceCount];
        for (int index = 0; index < instances.Length; index++)
        {
            instances[index] = new CookedVegetationInstance(
                (ulong)(index + 1),
                0,
                new Vector3(index * 0.25f, 0.0f, 0.0f),
                Quaternion.Identity,
                1.0f,
                0.5f);
        }

        var page = new CookedVegetationInstancePage(
            pageGuid,
            clusterGuid,
            PackageId,
            1,
            origin,
            ComputePageBounds(origin, instances),
            [new CookedVegetationSpeciesReference(s_Species, PackageId)],
            instances);
        page = page with { Acceleration = VegetationInstancePageAssetCooker.BuildAcceleration(page) };
        byte[] payload = VegetationInstancePageAssetCooker.WritePayload(page);
        byte[] contentHash = SHA256.HashData(payload);
        var payloadIdentity = new VegetationInstancePagePayloadIdentity(
            page.Guid,
            payload.LongLength,
            contentHash);
        var pageReference = new CookedVegetationInstancePageReference(
            pageGuid,
            PackageId,
            instances.Length,
            origin,
            page.Bounds,
            payload.LongLength,
            contentHash);
        var cluster = new CookedVegetationCluster(
            clusterGuid,
            PackageId,
            1,
            new CookedVegetationBiomeReference(s_Biome, PackageId),
            page.Bounds,
            [new CookedVegetationSpeciesReference(s_Species, PackageId)],
            [pageReference],
            instances.Length);
        VegetationResidentClusterRecord record =
            VegetationResidentClusterRecord.Create(cluster).WithGeneration(generation);
        VegetationResidentPageData residentPage =
            VegetationResidentPageData.Create(page, payloadIdentity).WithGeneration(generation);
        return new VegetationResidentClusterData(record, [residentPage]);
    }

    private static WorldBounds ComputePageBounds(
        WorldPosition origin,
        CookedVegetationInstance[] instances)
    {
        double minX = double.PositiveInfinity;
        double minY = double.PositiveInfinity;
        double minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double maxY = double.NegativeInfinity;
        double maxZ = double.NegativeInfinity;
        foreach (CookedVegetationInstance instance in instances)
        {
            double radius = instance.ConservativeRadius;
            double x = origin.X + instance.LocalPosition.X;
            double y = origin.Y + instance.LocalPosition.Y;
            double z = origin.Z + instance.LocalPosition.Z;
            minX = Math.Min(minX, x - radius);
            minY = Math.Min(minY, y - radius);
            minZ = Math.Min(minZ, z - radius);
            maxX = Math.Max(maxX, x + radius);
            maxY = Math.Max(maxY, y + radius);
            maxZ = Math.Max(maxZ, z + radius);
        }

        return new WorldBounds(
            new WorldPosition(minX, minY, minZ),
            new WorldPosition(maxX, maxY, maxZ));
    }

    private static VegetationPreparedClusterView CreateView(
        Guid clusterGuid,
        ulong generation,
        WorldPosition origin,
        int instanceCount) => new(
            clusterGuid,
            generation,
            origin,
            [CreateBatch(lodLevel: 0, maximumDistance: 32.0f, maximumScreenError: 1.0f)],
            instanceCount);

    private static VegetationPreparedBatch CreateBatch(
        int lodLevel,
        float maximumDistance,
        float maximumScreenError) => new(
            s_Species,
            Guid.Parse($"75000000-0000-0000-0000-{lodLevel + 1:D12}"),
            Guid.Parse($"76000000-0000-0000-0000-{lodLevel + 1:D12}"),
            new RHIBufferHandle { Index = 1, Generation = 1 },
            new RHIBufferHandle { Index = 2, Generation = 1 },
            EIndexType.INDEX_TYPE_UINT32,
            3,
            0,
            0,
            7,
            0,
            1,
            lodLevel,
            maximumDistance,
            maximumScreenError,
            windStiffness: 0.85f,
            VegetationShadowPolicy.Cast,
            new VegetationPreparedMaterialData(
                Vector4.One,
                alphaCutoff: 0.0f,
                metallicFactor: 0.0f,
                roughnessFactor: 1.0f,
                occlusionStrength: 1.0f,
                tintVariation: 1.0f,
                flags: 0u,
                baseColorImageIndex: 0u,
                baseColorSamplerIndex: 0u,
                normalImageIndex: 0u,
                normalSamplerIndex: 0u,
                ormImageIndex: 0u,
                ormSamplerIndex: 0u));

    private static Guid CreateGuid(int prefix, int index) =>
        Guid.Parse($"71000000-0000-0000-{prefix:X4}-{index:D12}");

    private sealed class Fixture
    {
        public List<VegetationClusterComponent> Extracted { get; } = [];
        public List<VegetationResidentClusterData> Residents { get; } = [];
        public List<VegetationCullingSelection> Selections { get; } = [];
        public FakePreparedClusterSource Prepared { get; } = new();
        public int ExpectedGatheredCount { get; set; }
        public int ExpectedPreparedCount { get; set; }
    }

    private sealed class FakePreparedClusterSource : IVegetationPreparedClusterSource
    {
        private readonly Dictionary<(Guid ClusterGuid, ulong Generation), VegetationPreparedClusterView>
            m_Views = new();

        public void Add(VegetationPreparedClusterView view) =>
            m_Views[(view.ClusterGuid, view.Generation)] = view;

        public bool TryGetCluster(
            Guid clusterGuid,
            ulong generation,
            out VegetationPreparedClusterView cluster) =>
            m_Views.TryGetValue((clusterGuid, generation), out cluster);
    }

    private sealed class SetupResult
    {
        public SetupResult(
            VegetationClusterCullingInput[] inputs,
            int inputCount,
            VegetationPreparedClusterFrame[] frames,
            int frameCount)
        {
            Inputs = inputs;
            InputCount = inputCount;
            Frames = frames;
            FrameCount = frameCount;
        }

        public VegetationClusterCullingInput[] Inputs { get; }
        public int InputCount { get; }
        public VegetationPreparedClusterFrame[] Frames { get; }
        public int FrameCount { get; }
    }
}
