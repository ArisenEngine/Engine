using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using Arisen.Native.RHI;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Threading;
using ArisenEngine.Vegetation;
using ArisenEngine.Vegetation.Assets;
using ArisenEngine.Vegetation.GenericRenderPipeline;
using Xunit;
using Xunit.Abstractions;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationDenseValleyMeasurementTests
{
    private const string PackageId = "com.arisen.vegetation.tests";
    private const int ShadowCascadeCount = 4;
    private const int FrameCount = 24;
    private const int WarmupPassCount = 3;
    private const int SetupWarmupPassCount = 3;
    private const int PlannerWarmupPassCount = 12;
    private const float InstanceRadius = 0.35f;
    private const double CellSpacing = 48.0;

    private static readonly Guid s_Species =
        Guid.Parse("7b0f2e52-8b67-4e3d-bf0a-cbc42f622001");
    private static readonly Guid s_Biome =
        Guid.Parse("c0a92f10-0eb9-4d24-b729-7d0f38313001");
    private static readonly Guid s_Mesh0 =
        Guid.Parse("75000000-0000-0000-0000-000000000001");
    private static readonly Guid s_Mesh1 =
        Guid.Parse("75000000-0000-0000-0000-000000000002");
    private static readonly Guid s_Material0 =
        Guid.Parse("76000000-0000-0000-0000-000000000001");
    private static readonly Guid s_Material1 =
        Guid.Parse("76000000-0000-0000-0000-000000000002");

    private readonly ITestOutputHelper m_Output;

    public VegetationDenseValleyMeasurementTests(ITestOutputHelper output)
    {
        m_Output = output;
    }

    [Fact]
    [Trait("Category", "AllocationSensitive")]
    public void DenseValleyPlanningReportIsDeterministicAndAllocationFree()
    {
        (int Clusters, int InstancesPerCluster)[] scales =
        [
            (256, 64),
            (1024, 128),
            (2048, 256)
        ];
        var report = new StringBuilder();
        WarmPlanner();
        report.AppendLine(
            "clusters instances buildMiB candidates culled accepted dropped " +
            "selectedInstances opaque shadow draws selectedGpuMiB planAvgUs planMaxUs allocBytes");

        foreach ((int clusterCount, int instancesPerCluster) in scales)
        {
            DenseValleyMeasurement measurement = Measure(clusterCount, instancesPerCluster);
            Assert.True(measurement.Deterministic, "the camera path must replay deterministically");
            Assert.True(measurement.AllocationFree, "planning must not allocate after warmup");
            Assert.True(measurement.ObservedCulling, "the path must exercise culling");
            Assert.True(measurement.ObservedFineLod, "the path must exercise the fine LOD");
            Assert.True(measurement.ObservedCoarseLod, "the path must exercise the coarse LOD");
            Assert.True(
                measurement.PeakAccepted <= VegetationCullingSettings.Default.MaximumBatchCount);
            Assert.True(
                measurement.PeakSelectedInstances <=
                VegetationCullingSettings.Default.MaximumInstanceCount);
            report.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{measurement.ClusterCount} {measurement.InstanceCount} " +
                $"{measurement.BuildBytes / 1048576.0:F2} {measurement.PeakCandidates} " +
                $"{measurement.PeakCulledClusters} {measurement.PeakAccepted} " +
                $"{measurement.PeakDropped} {measurement.PeakSelectedInstances} " +
                $"{measurement.OpaqueDraws} {measurement.ShadowDraws} " +
                $"{measurement.OpaqueDraws + measurement.ShadowDraws} " +
                $"{measurement.SelectedGpuBytes / 1048576.0:F2} " +
                $"{measurement.AveragePlanMicroseconds:F1} " +
                $"{measurement.MaximumPlanMicroseconds:F1} " +
                $"{measurement.AllocatedBytes}"));
        }

        m_Output.WriteLine(report.ToString());
    }

    private static void WarmPlanner()
    {
        VegetationClusterCullingInput[] inputs = BuildValley(1024, 128);
        var planner = new VegetationCullingPlanner();
        for (int pass = 0; pass < PlannerWarmupPassCount; pass++)
        {
            _ = TimePath(planner, inputs, VegetationCullingSettings.Default);
        }
    }

    [Fact]
    [Trait("Category", "AllocationSensitive")]
    public void TaskGraphSetupShardingReportIsDeterministicAndAllocationFreeInline()
    {
        (int Clusters, int InstancesPerCluster)[] scales =
        [
            (2048, 256),
            (10240, 16)
        ];
        var report = new StringBuilder();
        report.AppendLine(
            "clusters instances extracted gathered accepted workItems gatherSerialUs " +
            "gatherShardedUs planUs prepareSerialUs prepareShardedUs setupSerialUs " +
            "setupShardedUs serialAlloc shardedAlloc");
        WarmPlanner();
        using var taskGraph = new TaskGraph();
        foreach ((int clusterCount, int instancesPerCluster) in scales)
        {
            SetupShardingMeasurement measurement = MeasureSetupSharding(
                clusterCount,
                instancesPerCluster,
                taskGraph);
            Assert.True(
                measurement.Deterministic,
                "serial and sharded setup must produce identical ordered output");
            Assert.True(
                measurement.ObservedPartitioning,
                "the sharded path must dispatch more than one setup work item");
            Assert.Equal(0, measurement.SerialAllocatedBytes);
            report.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{measurement.ClusterCount} {measurement.InstanceCount} " +
                $"{measurement.ExtractedCount} {measurement.PeakGatheredCount} " +
                $"{measurement.PeakAcceptedCount} {measurement.WorkItemCount} " +
                $"{measurement.GatherSerialMicroseconds:F1} " +
                $"{measurement.GatherShardedMicroseconds:F1} " +
                $"{measurement.PlanMicroseconds:F1} " +
                $"{measurement.PrepareSerialMicroseconds:F1} " +
                $"{measurement.PrepareShardedMicroseconds:F1} " +
                $"{measurement.SetupSerialMicroseconds:F1} " +
                $"{measurement.SetupShardedMicroseconds:F1} " +
                $"{measurement.SerialAllocatedBytes} " +
                $"{measurement.ShardedAllocatedBytes}"));
        }

        m_Output.WriteLine(report.ToString());
    }

    private static SetupShardingMeasurement MeasureSetupSharding(
        int clusterCount,
        int instancesPerCluster,
        ITaskGraph taskGraph)
    {
        VegetationClusterCullingInput[] built = BuildValley(clusterCount, instancesPerCluster);
        var path = new SetupShardPath(built);
        VegetationCullingSettings settings = VegetationCullingSettings.Default;
        var serialPlanner = new VegetationCullingPlanner();
        var shardedPlanner = new VegetationCullingPlanner();
        var inlineDispatcher = new VegetationSetupWorkDispatcher(taskSystem: null);
        var taskDispatcher = new VegetationSetupWorkDispatcher(taskGraph);
        for (int pass = 0; pass < SetupWarmupPassCount; pass++)
        {
            _ = path.Run(inlineDispatcher, sharded: false, serialPlanner, settings);
            _ = path.Run(taskDispatcher, sharded: true, shardedPlanner, settings);
        }

        SetupShardPass serial = path.Run(
            inlineDispatcher,
            sharded: false,
            serialPlanner,
            settings);
        SetupShardPass serialConfirmation = path.Run(
            inlineDispatcher,
            sharded: false,
            serialPlanner,
            settings);
        SetupShardPass sharded = path.Run(
            taskDispatcher,
            sharded: true,
            shardedPlanner,
            settings);
        SetupShardPass shardedConfirmation = path.Run(
            taskDispatcher,
            sharded: true,
            shardedPlanner,
            settings);
        SetupShardPass reportedSerial =
            serial.GatherMicroseconds + serial.PrepareMicroseconds <=
            serialConfirmation.GatherMicroseconds + serialConfirmation.PrepareMicroseconds
                ? serial
                : serialConfirmation;
        SetupShardPass reportedSharded =
            sharded.GatherMicroseconds + sharded.PrepareMicroseconds <=
            shardedConfirmation.GatherMicroseconds + shardedConfirmation.PrepareMicroseconds
                ? sharded
                : shardedConfirmation;
        return new SetupShardingMeasurement(
            clusterCount,
            clusterCount * instancesPerCluster,
            path.ExtractedCount,
            reportedSerial.PeakInputCount,
            reportedSerial.PeakAcceptedCount,
            reportedSerial.WorkItemCount,
            reportedSerial.GatherMicroseconds,
            reportedSharded.GatherMicroseconds,
            Math.Min(reportedSerial.PlanMicroseconds, reportedSharded.PlanMicroseconds),
            reportedSerial.PrepareMicroseconds,
            reportedSharded.PrepareMicroseconds,
            reportedSerial.GatherMicroseconds + reportedSerial.PrepareMicroseconds,
            reportedSharded.GatherMicroseconds + reportedSharded.PrepareMicroseconds,
            serial.AllocatedBytes + serialConfirmation.AllocatedBytes,
            reportedSharded.AllocatedBytes,
            serial.Fingerprint == serialConfirmation.Fingerprint &&
            serial.Fingerprint == sharded.Fingerprint &&
            sharded.Fingerprint == shardedConfirmation.Fingerprint,
            reportedSerial.WorkItemCount > 1 && reportedSharded.WorkItemCount > 1);
    }

    private static DenseValleyMeasurement Measure(int clusterCount, int instancesPerCluster)
    {
        long beforeBuild = GC.GetAllocatedBytesForCurrentThread();
        VegetationClusterCullingInput[] inputs = BuildValley(clusterCount, instancesPerCluster);
        long buildBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBuild;

        VegetationCullingSettings settings = VegetationCullingSettings.Default;
        FrameSelection[][] first = RunPath(new VegetationCullingPlanner(), inputs, settings);
        FrameSelection[][] second = RunPath(new VegetationCullingPlanner(), inputs, settings);
        bool deterministic = FramesEqual(first, second);

        var warmed = new VegetationCullingPlanner();
        for (int pass = 0; pass < WarmupPassCount; pass++)
        {
            _ = RunPath(warmed, inputs, settings);
        }

        TimedPath settled = TimePath(warmed, inputs, settings);
        TimedPath confirmation = TimePath(warmed, inputs, settings);
        TimedPath reported = confirmation.AverageMicroseconds <= settled.AverageMicroseconds
            ? confirmation
            : settled;
        return new DenseValleyMeasurement(
            clusterCount,
            clusterCount * instancesPerCluster,
            buildBytes,
            reported.PeakCandidates,
            reported.PeakCulledClusters,
            reported.PeakAccepted,
            reported.PeakDropped,
            reported.PeakSelectedInstances,
            reported.PeakAccepted,
            reported.PeakAccepted * ShadowCascadeCount,
            (long)reported.PeakSelectedInstances * VegetationGpuInstance.Stride,
            reported.AverageMicroseconds,
            reported.MaximumMicroseconds,
            deterministic,
            settled.AllocatedBytes == 0 && confirmation.AllocatedBytes == 0,
            reported.ObservedCulling,
            reported.ObservedFineLod,
            reported.ObservedCoarseLod,
            settled.AllocatedBytes + confirmation.AllocatedBytes);
    }

    private static TimedPath TimePath(
        VegetationCullingPlanner planner,
        VegetationClusterCullingInput[] inputs,
        VegetationCullingSettings settings)
    {
        var acceptedPerFrame = new int[FrameCount];
        var instancesPerFrame = new int[FrameCount];
        int peakCandidates = 0;
        int peakCulledClusters = 0;
        int peakDropped = 0;
        bool observedCulling = false;
        bool observedFineLod = false;
        bool observedCoarseLod = false;
        double totalMicroseconds = 0.0;
        double maximumMicroseconds = 0.0;
        long beforeFrames = GC.GetAllocatedBytesForCurrentThread();
        for (int frame = 0; frame < FrameCount; frame++)
        {
            VegetationCullingView view = CreatePathView(frame);
            long start = Stopwatch.GetTimestamp();
            ReadOnlySpan<VegetationCullingSelection> selections = planner.Plan(
                inputs,
                view,
                settings);
            double microseconds = Stopwatch.GetElapsedTime(start).TotalMicroseconds;
            totalMicroseconds += microseconds;
            maximumMicroseconds = Math.Max(maximumMicroseconds, microseconds);

            VegetationCullingMetrics metrics = planner.Metrics;
            int accepted = 0;
            int selectedInstances = 0;
            int dropped = 0;
            for (int index = 0; index < selections.Length; index++)
            {
                ref readonly VegetationCullingSelection selection = ref selections[index];
                if (!selection.Accepted)
                {
                    dropped++;
                    continue;
                }

                accepted++;
                selectedInstances += selection.BudgetInstanceCount;
                if (selection.LodLevel == 0)
                {
                    observedFineLod = true;
                }
                else
                {
                    observedCoarseLod = true;
                }
            }

            acceptedPerFrame[frame] = accepted;
            instancesPerFrame[frame] = selectedInstances;
            peakCandidates = Math.Max(peakCandidates, metrics.CandidateSpeciesCount);
            peakCulledClusters = Math.Max(peakCulledClusters, metrics.CulledClusterCount);
            peakDropped = Math.Max(peakDropped, dropped);
            observedCulling |= metrics.CulledClusterCount > 0;
        }

        long frameBytes = GC.GetAllocatedBytesForCurrentThread() - beforeFrames;

        int peakFrame = 0;
        for (int frame = 1; frame < FrameCount; frame++)
        {
            if (instancesPerFrame[frame] > instancesPerFrame[peakFrame])
            {
                peakFrame = frame;
            }
        }

        return new TimedPath(
            peakCandidates,
            peakCulledClusters,
            acceptedPerFrame[peakFrame],
            peakDropped,
            instancesPerFrame[peakFrame],
            totalMicroseconds / FrameCount,
            maximumMicroseconds,
            frameBytes,
            observedCulling,
            observedFineLod,
            observedCoarseLod);
    }

    private static VegetationClusterCullingInput[] BuildValley(
        int clusterCount,
        int instancesPerCluster)
    {
        int gridSize = (int)Math.Ceiling(Math.Sqrt(clusterCount));
        CookedVegetationSpecies species = CreateSpecies();
        var inputs = new VegetationClusterCullingInput[clusterCount];
        for (int index = 0; index < clusterCount; index++)
        {
            int gridX = index / gridSize;
            int gridZ = index % gridSize;
            WorldPosition origin = new(
                (gridX - ((gridSize - 1) * 0.5)) * CellSpacing,
                0.0,
                (gridZ - ((gridSize - 1) * 0.5)) * CellSpacing);
            Guid clusterGuid = CreateGuid(0x71, index);
            Guid pageGuid = CreateGuid(0x72, index);
            ulong generation = (ulong)(index + 1);
            CookedVegetationInstancePage page = CreatePage(
                clusterGuid,
                pageGuid,
                origin,
                instancesPerCluster);
            long payloadSize = page.Instances.Count * VegetationGpuInstance.Stride;
            byte[] hash = SyntheticHash(index);
            var pageReference = new CookedVegetationInstancePageReference(
                pageGuid,
                PackageId,
                page.Instances.Count,
                origin,
                page.Bounds,
                payloadSize,
                hash);
            var cluster = new CookedVegetationCluster(
                clusterGuid,
                PackageId,
                1,
                new CookedVegetationBiomeReference(s_Biome, PackageId),
                page.Bounds,
                [new CookedVegetationSpeciesReference(s_Species, PackageId)],
                [pageReference],
                page.Instances.Count);
            VegetationResidentPageData residentPage = VegetationResidentPageData
                .Create(
                    page,
                    new VegetationInstancePagePayloadIdentity(pageGuid, payloadSize, hash))
                .WithGeneration(generation);
            VegetationResidentClusterData resident = new(
                VegetationResidentClusterRecord.Create(cluster).WithGeneration(generation),
                [residentPage]);
            inputs[index] = new VegetationClusterCullingInput(
                CreateComponent(clusterGuid, origin, instancesPerCluster),
                resident,
                CreatePreparedView(
                    clusterGuid,
                    generation,
                    origin,
                    page,
                    instancesPerCluster,
                    species));
        }

        return inputs;
    }

    private static VegetationClusterComponent CreateComponent(
        Guid clusterGuid,
        WorldPosition origin,
        int instanceCount) => new()
        {
            ClusterGuid = clusterGuid,
            BiomeGuid = s_Biome,
            SpeciesGuid = s_Species,
            OriginX = origin.X,
            OriginY = origin.Y,
            OriginZ = origin.Z,
            PageCount = 1,
            InstanceCount = instanceCount,
            Flags = VegetationClusterFlags.Visible |
                VegetationClusterFlags.CastShadows |
                VegetationClusterFlags.ReceiveShadows
        };

    private static VegetationPreparedClusterView CreatePreparedView(
        Guid clusterGuid,
        ulong generation,
        WorldPosition origin,
        CookedVegetationInstancePage page,
        int instanceCount,
        CookedVegetationSpecies species)
    {
        CookedVegetationClusterAcceleration acceleration = new(
            page.Bounds,
            [new CookedVegetationSpatialNode(page.Bounds, -1, -1, 0, 1)],
            [new CookedVegetationSpeciesAcceleration(
                new CookedVegetationSpeciesReference(s_Species, PackageId),
                0,
                0,
                1,
                instanceCount,
                page.Bounds,
                InstanceRadius,
                2_000.0f,
                8.0f)]);
        return new VegetationPreparedClusterView(
            clusterGuid,
            generation,
            origin,
            CreateBatches(instanceCount),
            instanceCount,
            acceleration,
            [species]);
    }

    private static VegetationPreparedBatch[] CreateBatches(int instanceCount) =>
    [
        CreateBatch(instanceCount, lodLevel: 0, maximumDistance: 80.0f, maximumScreenError: 1.0f),
        CreateBatch(instanceCount, lodLevel: 1, maximumDistance: 2_000.0f, maximumScreenError: 8.0f)
    ];

    private static VegetationPreparedBatch CreateBatch(
        int instanceCount,
        int lodLevel,
        float maximumDistance,
        float maximumScreenError) => new(
            s_Species,
            lodLevel == 0 ? s_Mesh0 : s_Mesh1,
            lodLevel == 0 ? s_Material0 : s_Material1,
            new RHIBufferHandle { Index = 1, Generation = 1 },
            new RHIBufferHandle { Index = 2, Generation = 1 },
            EIndexType.INDEX_TYPE_UINT32,
            6,
            0,
            0,
            (uint)(lodLevel + 1),
            0,
            (uint)instanceCount,
            lodLevel,
            maximumDistance,
            maximumScreenError,
            VegetationShadowPolicy.Cast,
            new VegetationPreparedMaterialData(Vector4.One, 0.0f, 1.0f, 0, 0));

    private static CookedVegetationSpecies CreateSpecies() => new(
        s_Species,
        PackageId,
        1,
        "Dense Valley Species",
        [
            new CookedVegetationSpeciesLod(
                new CookedVegetationMeshReference(s_Mesh0, PackageId),
                new CookedVegetationMaterialReference(s_Material0, PackageId),
                80.0f,
                1.0f),
            new CookedVegetationSpeciesLod(
                new CookedVegetationMeshReference(s_Mesh1, PackageId),
                new CookedVegetationMaterialReference(s_Material1, PackageId),
                2_000.0f,
                8.0f)
        ],
        VegetationShadowPolicy.Cast,
        new VegetationValueRange(1.0f, 1.0f),
        new VegetationValueRange(0.0f, 0.0f),
        new VegetationValueRange(0.0f, 0.0f),
        new VegetationCollisionPromotionDescriptor(
            VegetationCollisionPromotionMode.None,
            0.0f,
            0.0f,
            0.0f),
        0.0f);

    private static CookedVegetationInstancePage CreatePage(
        Guid clusterGuid,
        Guid pageGuid,
        WorldPosition origin,
        int instanceCount)
    {
        int side = (int)Math.Ceiling(Math.Sqrt(instanceCount));
        float step = (float)(CellSpacing * 0.9 / side);
        var instances = new CookedVegetationInstance[instanceCount];
        for (int index = 0; index < instanceCount; index++)
        {
            float localX = (index % side - ((side - 1) * 0.5f)) * step;
            float localZ = (index / side - ((side - 1) * 0.5f)) * step;
            instances[index] = new CookedVegetationInstance(
                (ulong)(index + 1),
                0,
                new Vector3(localX, 0.0f, localZ),
                Quaternion.Identity,
                1.0f,
                InstanceRadius);
        }

        var page = new CookedVegetationInstancePage(
            pageGuid,
            clusterGuid,
            PackageId,
            1,
            origin,
            ComputeBounds(origin, instances),
            [new CookedVegetationSpeciesReference(s_Species, PackageId)],
            instances);
        return page with
        {
            Acceleration = VegetationInstancePageAssetCooker.BuildAcceleration(page)
        };
    }

    private static WorldBounds ComputeBounds(
        WorldPosition origin,
        IReadOnlyList<CookedVegetationInstance> instances)
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

    private static Guid CreateGuid(byte prefix, int index)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes[0] = prefix;
        bytes[1] = 0x5E;
        BinaryPrimitives.WriteInt32BigEndian(bytes[2..6], index);
        bytes[6] = 0x80;
        bytes[7] = 0xA1;
        bytes[8] = 0x0D;
        bytes[15] = 0x01;
        return new Guid(bytes, bigEndian: true);
    }

    private static byte[] SyntheticHash(int index)
    {
        var hash = new byte[32];
        for (int offset = 0; offset < hash.Length; offset++)
        {
            hash[offset] = (byte)((index * 31) + (offset * 17));
        }

        return hash;
    }

    private static VegetationCullingView CreatePathView(int frame)
    {
        double cameraX = -1_200.0 + (2_400.0 * frame / (FrameCount - 1));
        var camera = new WorldPosition(cameraX, 2.0, 0.0);
        WorldPosition renderOrigin = new(Math.Floor(cameraX / 256.0) * 256.0, 0.0, 0.0);
        Vector3 eye = new(
            (float)(camera.X - renderOrigin.X),
            (float)(camera.Y - renderOrigin.Y),
            (float)(camera.Z - renderOrigin.Z));
        Matrix4x4 view = Matrix4x4.CreateLookAt(eye, eye + Vector3.UnitX, Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 3.0f,
            16.0f / 9.0f,
            0.25f,
            9_000.0f);
        return new VegetationCullingView(
            camera,
            renderOrigin,
            view * projection,
            VegetationCullingProjection.Perspective,
            Math.PI / 3.0,
            2.0,
            1080);
    }

    private static FrameSelection[][] RunPath(
        VegetationCullingPlanner planner,
        VegetationClusterCullingInput[] inputs,
        VegetationCullingSettings settings)
    {
        var frames = new FrameSelection[FrameCount][];
        for (int frame = 0; frame < FrameCount; frame++)
        {
            ReadOnlySpan<VegetationCullingSelection> selections = planner.Plan(
                inputs,
                CreatePathView(frame),
                settings);
            var recorded = new FrameSelection[selections.Length];
            for (int index = 0; index < selections.Length; index++)
            {
                recorded[index] = new FrameSelection(
                    selections[index].ClusterGuid,
                    selections[index].LodLevel,
                    selections[index].Accepted);
            }

            frames[frame] = recorded;
        }

        return frames;
    }

    private static bool FramesEqual(FrameSelection[][] left, FrameSelection[][] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int frame = 0; frame < left.Length; frame++)
        {
            if (left[frame].Length != right[frame].Length)
            {
                return false;
            }

            for (int index = 0; index < left[frame].Length; index++)
            {
                if (!left[frame][index].Equals(right[frame][index]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private readonly record struct FrameSelection(
        Guid ClusterGuid,
        int LodLevel,
        bool Accepted);

    private sealed class SetupShardPath
    {
        private readonly VegetationClusterComponent[] m_Extracted;
        private readonly VegetationResidentClusterData[] m_Residents;
        private readonly DictionaryPreparedClusterSource m_Prepared = new();
        private readonly VegetationClusterCullingInput[] m_Inputs;
        private readonly VegetationPreparedClusterFrame[] m_Frames;
        private readonly VegetationSetupShardBuffer<VegetationClusterCullingInput> m_InputRegions = new();
        private readonly VegetationSetupShardBuffer<VegetationPreparedClusterFrame> m_FrameRegions = new();
        private readonly Action<int> m_GatherWorkItem;
        private readonly Action<int> m_PrepareWorkItem;
        private VegetationCullingSelection[] m_Selections;
        private int m_SelectionCount;
        private int m_PrepareItemCount;

        public SetupShardPath(VegetationClusterCullingInput[] inputs)
        {
            m_Extracted = new VegetationClusterComponent[inputs.Length];
            m_Residents = new VegetationResidentClusterData[inputs.Length];
            m_Inputs = new VegetationClusterCullingInput[inputs.Length];
            m_Frames = new VegetationPreparedClusterFrame[inputs.Length];
            m_Selections = new VegetationCullingSelection[inputs.Length];
            for (int index = 0; index < inputs.Length; index++)
            {
                m_Extracted[index] = inputs[index].Component;
                m_Residents[index] = inputs[index].Resident;
                m_Prepared.Add(inputs[index].Prepared);
            }

            Array.Sort(m_Residents, static (left, right) => left.Guid.CompareTo(right.Guid));
            m_GatherWorkItem = RunGatherWorkItem;
            m_PrepareWorkItem = RunPrepareWorkItem;
        }

        public int ExtractedCount => m_Extracted.Length;

        public SetupShardPass Run(
            VegetationSetupWorkDispatcher dispatcher,
            bool sharded,
            VegetationCullingPlanner planner,
            VegetationCullingSettings settings)
        {
            int workItemCount =
                VegetationSetupWorkPartition.GetWorkItemCount(m_Extracted.Length);
            m_InputRegions.EnsureRegions(
                workItemCount,
                VegetationSetupWorkPartition.MaximumInputsPerWorkItem);
            double gatherTotal = 0.0;
            double planTotal = 0.0;
            double prepareTotal = 0.0;
            int peakInputCount = 0;
            int peakFrameCount = 0;
            int peakAcceptedCount = 0;
            ulong fingerprint = 14695981039346656037UL;
            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < FrameCount; frame++)
            {
                VegetationCullingView view = CreatePathView(frame);
                long gatherStart = Stopwatch.GetTimestamp();
                int inputCount = sharded ? GatherSharded(dispatcher) : GatherSerial();
                gatherTotal += Stopwatch.GetElapsedTime(gatherStart).TotalMicroseconds;
                peakInputCount = Math.Max(peakInputCount, inputCount);

                long planStart = Stopwatch.GetTimestamp();
                ReadOnlySpan<VegetationCullingSelection> selections = planner.Plan(
                    new ReadOnlySpan<VegetationClusterCullingInput>(m_Inputs, 0, inputCount),
                    view,
                    settings);
                planTotal += Stopwatch.GetElapsedTime(planStart).TotalMicroseconds;
                if (selections.Length > m_Selections.Length)
                {
                    Array.Resize(ref m_Selections, selections.Length);
                }

                selections.CopyTo(m_Selections);
                m_SelectionCount = selections.Length;
                int acceptedCount = 0;
                for (int index = 0; index < selections.Length; index++)
                {
                    if (selections[index].Accepted)
                    {
                        acceptedCount++;
                    }
                }

                peakAcceptedCount = Math.Max(peakAcceptedCount, acceptedCount);
                m_PrepareItemCount = inputCount;
                m_FrameRegions.EnsureRegions(
                    VegetationSetupWorkPartition.GetWorkItemCount(inputCount),
                    VegetationSetupWorkPartition.MaximumInputsPerWorkItem);
                long prepareStart = Stopwatch.GetTimestamp();
                int frameCount = sharded ? PrepareSharded(dispatcher) : PrepareSerial();
                prepareTotal += Stopwatch.GetElapsedTime(prepareStart).TotalMicroseconds;
                peakFrameCount = Math.Max(peakFrameCount, frameCount);
                fingerprint = FoldFingerprint(fingerprint, inputCount, frameCount);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            return new SetupShardPass(
                gatherTotal / FrameCount,
                planTotal / FrameCount,
                prepareTotal / FrameCount,
                peakInputCount,
                peakFrameCount,
                peakAcceptedCount,
                workItemCount,
                allocatedBytes,
                fingerprint);
        }

        private int GatherSerial() => VegetationPreparedSetup.GatherCullingInputs(
            m_Extracted,
            0,
            m_Extracted.Length,
            m_Residents,
            m_Prepared,
            m_Inputs);

        private int GatherSharded(VegetationSetupWorkDispatcher dispatcher)
        {
            dispatcher.Dispatch(m_Extracted.Length, m_GatherWorkItem);
            return m_InputRegions.MergeInto(m_Inputs);
        }

        private int PrepareSerial() => VegetationPreparedSetup.BuildPreparedFrames(
            new ReadOnlySpan<VegetationClusterCullingInput>(m_Inputs, 0, m_PrepareItemCount),
            0,
            m_PrepareItemCount,
            new ReadOnlySpan<VegetationCullingSelection>(m_Selections, 0, m_SelectionCount),
            m_Frames);

        private int PrepareSharded(VegetationSetupWorkDispatcher dispatcher)
        {
            dispatcher.Dispatch(m_PrepareItemCount, m_PrepareWorkItem);
            return m_FrameRegions.MergeInto(m_Frames);
        }

        private void RunGatherWorkItem(int workItemIndex)
        {
            if (!VegetationSetupWorkPartition.TryGetRange(
                    m_Extracted.Length,
                    workItemIndex,
                    out int start,
                    out int count))
            {
                throw new InvalidOperationException(
                    $"Gather work item '{workItemIndex}' is outside the extracted range.");
            }

            int written = VegetationPreparedSetup.GatherCullingInputs(
                m_Extracted,
                start,
                count,
                m_Residents,
                m_Prepared,
                m_InputRegions.GetRegion(workItemIndex));
            m_InputRegions.SetCount(workItemIndex, written);
        }

        private void RunPrepareWorkItem(int workItemIndex)
        {
            if (!VegetationSetupWorkPartition.TryGetRange(
                    m_PrepareItemCount,
                    workItemIndex,
                    out int start,
                    out int count))
            {
                throw new InvalidOperationException(
                    $"Prepare work item '{workItemIndex}' is outside the gathered range.");
            }

            int written = VegetationPreparedSetup.BuildPreparedFrames(
                new ReadOnlySpan<VegetationClusterCullingInput>(m_Inputs, 0, m_PrepareItemCount),
                start,
                count,
                new ReadOnlySpan<VegetationCullingSelection>(m_Selections, 0, m_SelectionCount),
                m_FrameRegions.GetRegion(workItemIndex));
            m_FrameRegions.SetCount(workItemIndex, written);
        }

        private ulong FoldFingerprint(ulong hash, int inputCount, int frameCount)
        {
            for (int index = 0; index < inputCount; index++)
            {
                hash = Mix(hash, m_Inputs[index].Resident.Guid);
            }

            for (int index = 0; index < frameCount; index++)
            {
                hash = Mix(hash, m_Frames[index].Component.ClusterGuid);
                hash = Mix(hash, m_Frames[index].Selection.SpeciesGuid);
            }

            return hash;
        }

        private static ulong Mix(ulong hash, Guid value) =>
            (hash ^ (uint)value.GetHashCode()) * 1099511628211UL;
    }

    private sealed class DictionaryPreparedClusterSource : IVegetationPreparedClusterSource
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

    private readonly record struct SetupShardPass(
        double GatherMicroseconds,
        double PlanMicroseconds,
        double PrepareMicroseconds,
        int PeakInputCount,
        int PeakFrameCount,
        int PeakAcceptedCount,
        int WorkItemCount,
        long AllocatedBytes,
        ulong Fingerprint);

    private readonly record struct SetupShardingMeasurement(
        int ClusterCount,
        int InstanceCount,
        int ExtractedCount,
        int PeakGatheredCount,
        int PeakAcceptedCount,
        int WorkItemCount,
        double GatherSerialMicroseconds,
        double GatherShardedMicroseconds,
        double PlanMicroseconds,
        double PrepareSerialMicroseconds,
        double PrepareShardedMicroseconds,
        double SetupSerialMicroseconds,
        double SetupShardedMicroseconds,
        long SerialAllocatedBytes,
        long ShardedAllocatedBytes,
        bool Deterministic,
        bool ObservedPartitioning);

    private readonly record struct TimedPath(
        int PeakCandidates,
        int PeakCulledClusters,
        int PeakAccepted,
        int PeakDropped,
        int PeakSelectedInstances,
        double AverageMicroseconds,
        double MaximumMicroseconds,
        long AllocatedBytes,
        bool ObservedCulling,
        bool ObservedFineLod,
        bool ObservedCoarseLod);

    private readonly record struct DenseValleyMeasurement(
        int ClusterCount,
        int InstanceCount,
        long BuildBytes,
        int PeakCandidates,
        int PeakCulledClusters,
        int PeakAccepted,
        int PeakDropped,
        int PeakSelectedInstances,
        int OpaqueDraws,
        int ShadowDraws,
        long SelectedGpuBytes,
        double AveragePlanMicroseconds,
        double MaximumPlanMicroseconds,
        bool Deterministic,
        bool AllocationFree,
        bool ObservedCulling,
        bool ObservedFineLod,
        bool ObservedCoarseLod,
        long AllocatedBytes);
}
