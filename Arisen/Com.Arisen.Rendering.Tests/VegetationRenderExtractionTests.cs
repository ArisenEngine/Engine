using ArisenEngine.Core.ECS;
using ArisenEngine.Vegetation;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationRenderExtractionTests
{
    private static readonly Guid s_BiomeA =
        Guid.Parse("61000000-0000-0000-0000-000000000001");
    private static readonly Guid s_BiomeB =
        Guid.Parse("61000000-0000-0000-0000-000000000002");
    private static readonly Guid s_WorldA =
        Guid.Parse("62000000-0000-0000-0000-000000000001");
    private static readonly Guid s_WorldB =
        Guid.Parse("62000000-0000-0000-0000-000000000002");
    private static readonly Guid s_CellA =
        Guid.Parse("63000000-0000-0000-0000-000000000001");
    private static readonly Guid s_CellB =
        Guid.Parse("63000000-0000-0000-0000-000000000002");
    private static readonly Guid s_SpeciesA =
        Guid.Parse("64000000-0000-0000-0000-000000000001");
    private static readonly Guid s_SpeciesB =
        Guid.Parse("64000000-0000-0000-0000-000000000002");

    [Fact]
    public void VisibleClusterExtractionIsContiguousDeterministicAndReusable()
    {
        var world = new EntityManager();
        Guid[] expected =
        [
            ClusterGuid(1),
            ClusterGuid(2),
            ClusterGuid(3),
            ClusterGuid(4),
            ClusterGuid(5),
            ClusterGuid(6),
            ClusterGuid(7),
            ClusterGuid(8),
            ClusterGuid(9)
        ];

        AddCluster(world, CreateCluster(9, s_BiomeB, s_WorldA, 0, 0, 0, s_CellA, s_SpeciesA));
        AddCluster(world, CreateCluster(8, s_BiomeA, s_WorldB, 0, 0, 0, s_CellA, s_SpeciesA));
        AddCluster(world, CreateCluster(7, s_BiomeA, s_WorldA, 1, 0, 0, s_CellA, s_SpeciesA));
        AddCluster(world, CreateCluster(6, s_BiomeA, s_WorldA, 0, 1, 0, s_CellA, s_SpeciesA));
        AddCluster(world, CreateCluster(5, s_BiomeA, s_WorldA, 0, 0, 1, s_CellA, s_SpeciesA));
        AddCluster(world, CreateCluster(4, s_BiomeA, s_WorldA, 0, 0, 0, s_CellB, s_SpeciesA));
        AddCluster(world, CreateCluster(3, s_BiomeA, s_WorldA, 0, 0, 0, s_CellA, s_SpeciesB));
        AddCluster(world, CreateCluster(2, s_BiomeA, s_WorldA, 0, 0, 0, s_CellA, s_SpeciesA));
        AddCluster(world, CreateCluster(1, s_BiomeA, s_WorldA, 0, 0, 0, s_CellA, s_SpeciesA));
        AddCluster(
            world,
            CreateCluster(
                0,
                s_BiomeA,
                s_WorldA,
                0,
                0,
                0,
                s_CellA,
                s_SpeciesA,
                visible: false));
        var source = new VegetationClusterRenderSource(() => world);

        Guid[] first = source.ExtractVisibleClusters()
            .ToArray()
            .Select(cluster => cluster.ClusterGuid)
            .ToArray();
        Guid[] second = source.ExtractVisibleClusters()
            .ToArray()
            .Select(cluster => cluster.ClusterGuid)
            .ToArray();

        Assert.Equal(expected, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ExtractionWithoutAnActiveVegetationPoolReturnsEmpty()
    {
        var missingWorld = new VegetationClusterRenderSource(() => null);
        var emptyWorld = new VegetationClusterRenderSource(() => new EntityManager());

        Assert.True(missingWorld.ExtractVisibleClusters().IsEmpty);
        Assert.True(emptyWorld.ExtractVisibleClusters().IsEmpty);
    }

    [Fact]
    [Trait("Category", "AllocationSensitive")]
    public void WarmVisibleClusterExtractionAllocatesNoManagedMemory()
    {
        var world = new EntityManager();
        for (int index = 8; index >= 1; index--)
        {
            AddCluster(
                world,
                CreateCluster(
                    index,
                    s_BiomeA,
                    s_WorldA,
                    index & 1,
                    (index >> 1) & 1,
                    (index >> 2) & 1,
                    s_CellA,
                    s_SpeciesA));
        }

        var source = new VegetationClusterRenderSource(() => world);
        for (int index = 0; index < 8; index++)
        {
            source.ExtractVisibleClusters();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        int extractedCount = 0;
        for (int index = 0; index < 64; index++)
        {
            extractedCount += source.ExtractVisibleClusters().Length;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(512, extractedCount);
        Assert.Equal(0, allocated);
    }

    private static VegetationClusterComponent CreateCluster(
        int clusterOrdinal,
        Guid biomeGuid,
        Guid worldGuid,
        int cellX,
        int cellY,
        int cellZ,
        Guid owningCellGuid,
        Guid speciesGuid,
        bool visible = true) =>
        new()
        {
            ClusterGuid = ClusterGuid(clusterOrdinal),
            BiomeGuid = biomeGuid,
            SpeciesGuid = speciesGuid,
            WorldGuid = worldGuid,
            OwningCellGuid = owningCellGuid,
            CellX = cellX,
            CellY = cellY,
            CellZ = cellZ,
            Flags = visible
                ? VegetationClusterFlags.Visible
                : VegetationClusterFlags.None
        };

    private static void AddCluster(
        EntityManager world,
        in VegetationClusterComponent component)
    {
        Entity entity = world.CreateEntity();
        world.AddComponent(entity, component);
    }

    private static Guid ClusterGuid(int ordinal) =>
        Guid.Parse($"65000000-0000-0000-0000-{ordinal:D12}");
}
