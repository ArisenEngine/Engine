using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using ArisenEngine.Vegetation;
using ArisenEngine.Vegetation.Assets;
using ArisenEngine.Vegetation.GenericRenderPipeline;
using ArisenEngine.Resources.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationClusterLookupTests
{
    private const string PackageId = "com.arisen.vegetation.tests";
    private const int ResidentClusterCount = 41;
    private const int AbsentProbeCount = 16;
    private const int DenseResidentClusterCount = 2048;
    private const int DenseProbeCount = 1662;
    private const int MeasurementWarmupPassCount = 12;
    private const int MeasurementPassCount = 2;

    private static readonly Guid s_Species =
        Guid.Parse("73000000-0000-0000-0000-000000000001");
    private static readonly Guid s_OtherSpecies =
        Guid.Parse("73000000-0000-0000-0000-000000000002");
    private static readonly Guid s_Biome =
        Guid.Parse("74000000-0000-0000-0000-000000000001");

    private readonly ITestOutputHelper m_Output;

    public VegetationClusterLookupTests(ITestOutputHelper output)
    {
        m_Output = output;
    }

    [Fact]
    public void ResidentLookupFindsEveryClusterOfASnapshot()
    {
        VegetationResidentClusterData[] ordered = PublishResidentClusters(ResidentClusterCount);

        for (int index = 0; index < ordered.Length; index++)
        {
            Assert.True(
                VegetationClusterLookup.TryFindResidentCluster(
                    ordered,
                    ordered[index].Guid,
                    out VegetationResidentClusterData found));
            Assert.Same(ordered[index], found);
            Assert.Equal(ordered[index].Generation, found.Generation);
        }
    }

    [Fact]
    public void ResidentLookupHandlesEmptySingleAndBoundarySpans()
    {
        Assert.False(
            VegetationClusterLookup.TryFindResidentCluster(
                ReadOnlySpan<VegetationResidentClusterData>.Empty,
                CreateGuid(0x71, 0),
                out _));

        VegetationResidentClusterData[] single = PublishResidentClusters(1);
        Assert.False(
            VegetationClusterLookup.TryFindResidentCluster(
                single,
                CreateGuid(0x71, 1),
                out _));
        Assert.True(VegetationClusterLookup.TryFindResidentCluster(single, single[0].Guid, out _));

        VegetationResidentClusterData[] boundary = PublishResidentClusters(ResidentClusterCount);
        Assert.True(
            VegetationClusterLookup.TryFindResidentCluster(boundary, boundary[0].Guid, out _));
        Assert.True(
            VegetationClusterLookup.TryFindResidentCluster(boundary, boundary[^1].Guid, out _));
    }

    [Fact]
    public void ResidentLookupRejectsGuidsOutsideTheResidentRange()
    {
        VegetationResidentClusterData[] ordered = PublishResidentClusters(ResidentClusterCount);

        Assert.False(
            VegetationClusterLookup.TryFindResidentCluster(
                ordered,
                CreateGuid(0x70, 0),
                out VegetationResidentClusterData belowRange));
        Assert.Null(belowRange);

        Assert.False(
            VegetationClusterLookup.TryFindResidentCluster(
                ordered,
                CreateGuid(0x71, ResidentClusterCount + 1024),
                out VegetationResidentClusterData aboveRange));
        Assert.Null(aboveRange);

        Assert.False(
            VegetationClusterLookup.TryFindResidentCluster(ordered, Guid.Empty, out _));
    }

    [Fact]
    public void ResidentLookupMatchesTheFormerLinearScan()
    {
        VegetationResidentClusterData[] ordered = PublishResidentClusters(ResidentClusterCount);

        foreach (Guid probe in BuildProbes(ordered, static cluster => cluster.Guid))
        {
            bool expected = TryLinearFindResidentCluster(
                ordered,
                probe,
                out VegetationResidentClusterData? expectedCluster);
            bool actual = VegetationClusterLookup.TryFindResidentCluster(
                ordered,
                probe,
                out VegetationResidentClusterData actualCluster);

            Assert.Equal(expected, actual);
            if (!expected)
            {
                continue;
            }

            Assert.NotNull(expectedCluster);
            Assert.Same(expectedCluster, actualCluster);
            Assert.Equal(probe, actualCluster.Guid);
        }
    }

    [Fact]
    public void SelectionLookupReturnsTheFirstEntryOfADuplicateClusterRun()
    {
        Guid clusterGuid = CreateGuid(0x71, 3);
        Guid otherGuid = CreateGuid(0x71, 4);
        VegetationCullingSelection[] ordered =
        [
            CreateSelection(otherGuid, 1, s_Species, lodLevel: 7, accepted: true),
            CreateSelection(clusterGuid, 5, s_Species, lodLevel: 1, accepted: true),
            CreateSelection(clusterGuid, 5, s_OtherSpecies, lodLevel: 3, accepted: false),
            CreateSelection(clusterGuid, 9, s_Species, lodLevel: 0, accepted: true)
        ];

        Assert.True(
            VegetationClusterLookup.TryFindSelection(
                ordered,
                clusterGuid,
                out VegetationCullingSelection selection));
        Assert.Equal(clusterGuid, selection.ClusterGuid);
        Assert.Equal(5UL, selection.Generation);
        Assert.Equal(s_Species, selection.SpeciesGuid);
        Assert.Equal(1, selection.LodLevel);
        Assert.True(selection.Accepted);
    }

    [Fact]
    public void SelectionLookupHonoursTheSlicedLength()
    {
        Guid present = CreateGuid(0x71, 2);
        Guid outsideSlice = CreateGuid(0x71, 9);
        VegetationCullingSelection[] buffer =
        [
            CreateSelection(CreateGuid(0x71, 1), 1, s_Species, lodLevel: 0, accepted: true),
            CreateSelection(present, 1, s_Species, lodLevel: 1, accepted: true),
            CreateSelection(outsideSlice, 1, s_Species, lodLevel: 2, accepted: true),
            CreateSelection(outsideSlice, 2, s_Species, lodLevel: 3, accepted: true)
        ];
        var slice = new ReadOnlySpan<VegetationCullingSelection>(buffer, 0, 2);

        Assert.True(
            VegetationClusterLookup.TryFindSelection(
                slice,
                present,
                out VegetationCullingSelection found));
        Assert.Equal(present, found.ClusterGuid);

        Assert.False(
            VegetationClusterLookup.TryFindSelection(
                slice,
                outsideSlice,
                out VegetationCullingSelection missing));
        Assert.Equal(Guid.Empty, missing.ClusterGuid);
        Assert.False(
            VegetationClusterLookup.TryFindSelection(
                ReadOnlySpan<VegetationCullingSelection>.Empty,
                present,
                out _));
    }

    [Fact]
    public void SelectionLookupMatchesTheFormerLinearScan()
    {
        VegetationCullingSelection[] ordered = BuildOrderedSelections(
            clusterCount: 9,
            generationsPerCluster: 2,
            speciesPerGeneration: 2);
        Assert.Equal(36, ordered.Length);

        foreach (Guid probe in BuildProbes(ordered, static selection => selection.ClusterGuid))
        {
            bool expected = TryLinearFindSelection(
                ordered,
                probe,
                out VegetationCullingSelection expectedSelection);
            bool actual = VegetationClusterLookup.TryFindSelection(
                ordered,
                probe,
                out VegetationCullingSelection actualSelection);

            Assert.Equal(expected, actual);
            if (!expected)
            {
                continue;
            }

            Assert.Equal(expectedSelection.Generation, actualSelection.Generation);
            Assert.Equal(expectedSelection.SpeciesGuid, actualSelection.SpeciesGuid);
            Assert.Equal(expectedSelection.LodLevel, actualSelection.LodLevel);
            Assert.Equal(expectedSelection.Accepted, actualSelection.Accepted);
        }

        Assert.True(
            VegetationClusterLookup.TryFindSelection(
                ordered,
                CreateGuid(0x71, 0),
                out VegetationCullingSelection firstRun));
        Assert.Equal(1UL, firstRun.Generation);
        Assert.Equal(s_Species, firstRun.SpeciesGuid);
        Assert.Equal(10, firstRun.LodLevel);
    }

    [Fact]
    [Trait("Category", "AllocationSensitive")]
    public void DenseLookupPassIsAllocationFreeAndMatchesTheFormerLinearScan()
    {
        VegetationResidentClusterData[] resident = CreateDenseResidentClusters(
            DenseResidentClusterCount);
        Guid[] probes = new Guid[DenseProbeCount];
        for (int index = 0; index < probes.Length; index++)
        {
            probes[index] = resident[((index * 7) + 3) % resident.Length].Guid;
        }

        VegetationCullingSelection[] selections = BuildOrderedSelections(
            DenseResidentClusterCount,
            generationsPerCluster: 1,
            speciesPerGeneration: 2);

        for (int pass = 0; pass < MeasurementWarmupPassCount; pass++)
        {
            RunResidentProbe(resident, probes, binary: true);
            RunSelectionProbe(selections, probes, binary: true);
        }

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        int residentFound = RunResidentProbe(resident, probes, binary: true);
        int selectionFound = RunSelectionProbe(selections, probes, binary: true);
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(DenseProbeCount, residentFound);
        Assert.Equal(DenseProbeCount, selectionFound);
        Assert.Equal(0, allocatedBytes);

        Assert.Equal(DenseProbeCount, RunResidentProbe(resident, probes, binary: false));
        Assert.Equal(DenseProbeCount, RunSelectionProbe(selections, probes, binary: false));

        double residentOrderedUs = double.PositiveInfinity;
        double residentLinearUs = double.PositiveInfinity;
        double selectionOrderedUs = double.PositiveInfinity;
        double selectionLinearUs = double.PositiveInfinity;
        for (int pass = 0; pass < MeasurementPassCount; pass++)
        {
            long start = Stopwatch.GetTimestamp();
            RunResidentProbe(resident, probes, binary: true);
            residentOrderedUs = Math.Min(
                residentOrderedUs,
                Stopwatch.GetElapsedTime(start).TotalMicroseconds);

            start = Stopwatch.GetTimestamp();
            RunResidentProbe(resident, probes, binary: false);
            residentLinearUs = Math.Min(
                residentLinearUs,
                Stopwatch.GetElapsedTime(start).TotalMicroseconds);

            start = Stopwatch.GetTimestamp();
            RunSelectionProbe(selections, probes, binary: true);
            selectionOrderedUs = Math.Min(
                selectionOrderedUs,
                Stopwatch.GetElapsedTime(start).TotalMicroseconds);

            start = Stopwatch.GetTimestamp();
            RunSelectionProbe(selections, probes, binary: false);
            selectionLinearUs = Math.Min(
                selectionLinearUs,
                Stopwatch.GetElapsedTime(start).TotalMicroseconds);
        }

        m_Output.WriteLine(
            "residentClusters={0} selections={1} probes={2} allocationBytes={3} " +
            "residentOrderedUs={4:F1} residentLinearUs={5:F1} " +
            "selectionOrderedUs={6:F1} selectionLinearUs={7:F1}",
            resident.Length,
            selections.Length,
            probes.Length,
            allocatedBytes,
            residentOrderedUs,
            residentLinearUs,
            selectionOrderedUs,
            selectionLinearUs);
    }

    private static int RunResidentProbe(
        VegetationResidentClusterData[] resident,
        Guid[] probes,
        bool binary)
    {
        int found = 0;
        for (int index = 0; index < probes.Length; index++)
        {
            bool hit = binary
                ? VegetationClusterLookup.TryFindResidentCluster(
                    resident,
                    probes[index],
                    out _)
                : TryLinearFindResidentCluster(resident, probes[index], out _);
            found += hit ? 1 : 0;
        }

        return found;
    }

    private static int RunSelectionProbe(
        VegetationCullingSelection[] selections,
        Guid[] probes,
        bool binary)
    {
        int found = 0;
        for (int index = 0; index < probes.Length; index++)
        {
            bool hit = binary
                ? VegetationClusterLookup.TryFindSelection(selections, probes[index], out _)
                : TryLinearFindSelection(selections, probes[index], out _);
            found += hit ? 1 : 0;
        }

        return found;
    }

    private static Guid[] BuildProbes<TEntry>(
        TEntry[] ordered,
        Func<TEntry, Guid> selector)
    {
        var probes = new List<Guid>(ordered.Length + AbsentProbeCount + 1);
        foreach (TEntry entry in ordered)
        {
            probes.Add(selector(entry));
        }

        for (int index = 0; index < AbsentProbeCount; index++)
        {
            probes.Add(CreateGuid(0x71, 100_000 + index));
            probes.Add(CreateGuid(0x70, index));
        }

        probes.Add(Guid.Empty);
        return probes.ToArray();
    }

    private static bool TryLinearFindResidentCluster(
        ReadOnlySpan<VegetationResidentClusterData> clusters,
        Guid clusterGuid,
        out VegetationResidentClusterData? cluster)
    {
        for (int index = 0; index < clusters.Length; index++)
        {
            if (clusters[index].Guid == clusterGuid)
            {
                cluster = clusters[index];
                return true;
            }
        }

        cluster = null;
        return false;
    }

    private static bool TryLinearFindSelection(
        ReadOnlySpan<VegetationCullingSelection> selections,
        Guid clusterGuid,
        out VegetationCullingSelection selection)
    {
        for (int index = 0; index < selections.Length; index++)
        {
            if (selections[index].ClusterGuid == clusterGuid)
            {
                selection = selections[index];
                return true;
            }
        }

        selection = default;
        return false;
    }

    private static VegetationCullingSelection CreateSelection(
        Guid clusterGuid,
        ulong generation,
        Guid speciesGuid,
        int lodLevel,
        bool accepted) =>
        new(
            clusterGuid,
            generation,
            speciesGuid,
            lodLevel,
            visiblePageCount: 1,
            visibleInstanceCount: lodLevel + 1,
            budgetInstanceCount: lodLevel + 1,
            distanceSquared: lodLevel * 4.0,
            screenSpaceError: lodLevel * 0.5,
            accepted);

    private static VegetationCullingSelection[] BuildOrderedSelections(
        int clusterCount,
        int generationsPerCluster,
        int speciesPerGeneration)
    {
        var selections = new VegetationCullingSelection[
            clusterCount * generationsPerCluster * speciesPerGeneration];
        int write = 0;
        for (int clusterIndex = clusterCount - 1; clusterIndex >= 0; clusterIndex--)
        {
            Guid clusterGuid = CreateGuid(0x71, clusterIndex);
            for (int generation = generationsPerCluster; generation >= 1; generation--)
            {
                for (int speciesIndex = speciesPerGeneration - 1; speciesIndex >= 0; speciesIndex--)
                {
                    selections[write++] = CreateSelection(
                        clusterGuid,
                        (ulong)generation,
                        speciesIndex == 0 ? s_Species : s_OtherSpecies,
                        lodLevel: (generation * 10) + speciesIndex,
                        accepted: generation == 1);
                }
            }
        }

        Array.Sort(
            selections,
            static (left, right) =>
            {
                int result = left.ClusterGuid.CompareTo(right.ClusterGuid);
                if (result != 0)
                {
                    return result;
                }

                result = left.Generation.CompareTo(right.Generation);
                return result != 0 ? result : left.SpeciesGuid.CompareTo(right.SpeciesGuid);
            });
        return selections;
    }

    private static VegetationResidentClusterData[] PublishResidentClusters(int count)
    {
        var guids = new Guid[count];
        for (int index = 0; index < count; index++)
        {
            guids[index] = CreateGuid(0x71, index);
        }

        Array.Sort(guids, static (left, right) => left.CompareTo(right));

        var store = new VegetationRuntimeDataStore();
        for (int index = count - 1; index >= 0; index--)
        {
            (CookedVegetationCluster cluster, CookedVegetationInstancePage page) =
                CreateCookedCluster(
                    guids[index],
                    CreateGuid(0x72, index),
                    new WorldPosition(index * 32.0, 0.0, 0.0),
                    instanceCount: 2);
            store.PublishPage(page);
            store.PublishCluster(cluster);
        }

        VegetationClusterDataSnapshot snapshot = store.GetSnapshot();
        Assert.Equal(count, snapshot.Clusters.Length);
        VegetationResidentClusterData[] ordered = snapshot.Clusters.ToArray();
        for (int index = 1; index < ordered.Length; index++)
        {
            Assert.True(
                ordered[index - 1].Guid.CompareTo(ordered[index].Guid) < 0,
                "Resident snapshot clusters must stay Guid-ordered for the lookups to be valid.");
        }

        return ordered;
    }

    private static VegetationResidentClusterData[] CreateDenseResidentClusters(int count)
    {
        var clusters = new VegetationResidentClusterData[count];
        for (int index = 0; index < count; index++)
        {
            (CookedVegetationCluster cluster, CookedVegetationInstancePage page) =
                CreateCookedCluster(
                    CreateGuid(0x71, index),
                    CreateGuid(0x72, index),
                    new WorldPosition(index * 32.0, 0.0, 0.0),
                    instanceCount: 2);
            clusters[index] = CreateResidentCluster(cluster, page, (ulong)(index + 1));
        }

        Array.Sort(
            clusters,
            static (left, right) => left.Guid.CompareTo(right.Guid));
        return clusters;
    }

    private static VegetationResidentClusterData CreateResidentCluster(
        CookedVegetationCluster cluster,
        CookedVegetationInstancePage page,
        ulong generation)
    {
        byte[] payload = VegetationInstancePageAssetCooker.WritePayload(page);
        VegetationResidentPageData residentPage = VegetationResidentPageData
            .Create(
                page,
                new VegetationInstancePagePayloadIdentity(
                    page.Guid,
                    payload.LongLength,
                    SHA256.HashData(payload)))
            .WithGeneration(generation);
        return new VegetationResidentClusterData(
            VegetationResidentClusterRecord.Create(cluster).WithGeneration(generation),
            [residentPage]);
    }

    private static (CookedVegetationCluster Cluster, CookedVegetationInstancePage Page)
        CreateCookedCluster(
            Guid clusterGuid,
            Guid pageGuid,
            WorldPosition origin,
            int instanceCount)
    {
        var instances = new CookedVegetationInstance[instanceCount];
        for (int index = 0; index < instanceCount; index++)
        {
            instances[index] = new CookedVegetationInstance(
                (ulong)(index + 1),
                0,
                new Vector3(index * 0.25f, 0.0f, 0.0f),
                Quaternion.Identity,
                1.0f,
                0.5f);
        }

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

        var page = new CookedVegetationInstancePage(
            pageGuid,
            clusterGuid,
            PackageId,
            1,
            origin,
            new WorldBounds(
                new WorldPosition(minX, minY, minZ),
                new WorldPosition(maxX, maxY, maxZ)),
            [new CookedVegetationSpeciesReference(s_Species, PackageId)],
            instances);
        page = page with
        {
            Acceleration = VegetationInstancePageAssetCooker.BuildAcceleration(page)
        };

        byte[] payload = VegetationInstancePageAssetCooker.WritePayload(page);
        var pageReference = new CookedVegetationInstancePageReference(
            pageGuid,
            PackageId,
            instances.Length,
            origin,
            page.Bounds,
            payload.LongLength,
            SHA256.HashData(payload));
        var cluster = new CookedVegetationCluster(
            clusterGuid,
            PackageId,
            1,
            new CookedVegetationBiomeReference(s_Biome, PackageId),
            page.Bounds,
            [new CookedVegetationSpeciesReference(s_Species, PackageId)],
            [pageReference],
            instances.Length);
        return (cluster, page);
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
}
