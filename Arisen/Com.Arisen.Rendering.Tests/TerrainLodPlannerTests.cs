using System.Numerics;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainLodPlannerTests
{
    [Fact]
    public void PlannerProducesDeterministicMixedLodWithCompatibleStitchEdges()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            2,
            1,
            resolution: 33,
            tileOrigin: new TerrainTileCoordinate(-1, 2),
            height: (x, z) => x < 16 && z < 16
                ? checked((ushort)(1000 + (((x + z) & 1) * 30_000)))
                : (ushort)1000);
        var runtimeData = Publish(fixture);
        var planner = new TerrainLodPlanner(runtimeData);
        TerrainTileComponent[] visible = fixture.Tiles
            .Reverse()
            .Select(tile => TerrainRuntimeTestData.CreateComponent(tile))
            .ToArray();
        TerrainLodView view = CreatePerspectiveView(
            fixture.Root.WorldPlacement,
            new WorldPosition(8.0, 100.0, 8.0));
        var settings = new TerrainLodSettings(4.0, 0.15, 64, false);

        TerrainPatchRecord[] first = planner.Plan(visible, view, settings).ToArray();
        TerrainPatchRecord[] second = planner.Plan(visible, view, settings).ToArray();

        Assert.Equal(8, first.Length);
        Assert.Equal(first.Select(Identity), second.Select(Identity));
        Assert.Equal(first.Select(item => item.LodLevel), second.Select(item => item.LodLevel));
        Assert.True(first.Zip(first.Skip(1), IsOrdered).All(value => value));
        AssertMixedEdgesAreCompatible(first);
        Assert.True(planner.Metrics.NeighborRefinementCount > 0);
        Assert.False(planner.Metrics.Overflowed);
    }

    [Fact]
    public void PlannerHysteresisKeepsPreviousLodInsideDeadBand()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            1,
            1,
            resolution: 17,
            height: (x, z) => checked((ushort)(1000 + (((x + z) & 1) * 30_000))));
        TerrainRuntimeDataStore runtimeData = Publish(fixture);
        Assert.True(runtimeData.TryGetTile(fixture.Tiles[0].Guid, out TerrainResidentTileData resident));
        TerrainTileAcceleration acceleration = resident.Acceleration;
        int coarsestLod = acceleration.LodLevelCount - 1;
        double coarseError = acceleration.GetGeometricError(0, coarsestLod);
        Assert.True(coarseError > 0.0);
        TerrainTileComponent[] visible = [TerrainRuntimeTestData.CreateComponent(fixture.Tiles[0])];
        var settings = new TerrainLodSettings(2.0, 0.15, 8, false);
        TerrainLodView scaleView = CreatePerspectiveView(
            fixture.Root.WorldPlacement,
            new WorldPosition(8.0, 100.0, 8.0));
        double farDistance = coarseError * scaleView.ProjectionScale / 1.0;
        double deadBandDistance = coarseError * scaleView.ProjectionScale / 2.1;
        double maximumWorldY = fixture.Tiles[0].WorldPlacement.Y +
                               acceleration.GetPatch(0).MaxHeight;
        TerrainLodView farView = CreatePerspectiveView(
            fixture.Root.WorldPlacement,
            new WorldPosition(8.0, maximumWorldY + farDistance, 8.0));
        TerrainLodView deadBandView = CreatePerspectiveView(
            fixture.Root.WorldPlacement,
            new WorldPosition(8.0, maximumWorldY + deadBandDistance, 8.0));
        var planner = new TerrainLodPlanner(runtimeData);

        TerrainPatchRecord far = Assert.Single(planner.Plan(visible, farView, settings).ToArray());
        TerrainPatchRecord retained = Assert.Single(
            planner.Plan(visible, deadBandView, settings).ToArray());
        var freshPlanner = new TerrainLodPlanner(runtimeData);
        TerrainPatchRecord fresh = Assert.Single(
            freshPlanner.Plan(visible, deadBandView, settings).ToArray());

        Assert.Equal(coarsestLod, far.LodLevel);
        Assert.Equal(coarsestLod, retained.LodLevel);
        Assert.True(fresh.LodLevel < retained.LodLevel);
    }

    [Fact]
    public void PlannerBoundsOverflowByNearestPatchAndReportsCulling()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            1,
            1,
            resolution: 33,
            worldPlacement: new WorldPosition(100.0, 0.0, 100.0),
            height: (_, _) => 1000);
        TerrainRuntimeDataStore runtimeData = Publish(fixture);
        var planner = new TerrainLodPlanner(runtimeData);
        TerrainTileComponent[] visible = [TerrainRuntimeTestData.CreateComponent(fixture.Tiles[0])];
        TerrainLodView view = CreatePerspectiveView(
            fixture.Root.WorldPlacement,
            new WorldPosition(101.0, 10.0, 101.0));
        var bounded = new TerrainLodSettings(2.0, 0.15, 2, false);

        TerrainPatchRecord[] selected = planner.Plan(visible, view, bounded).ToArray();

        Assert.Equal(2, selected.Length);
        Assert.Equal(4, planner.Metrics.CandidatePatchCount);
        Assert.Equal(2, planner.Metrics.OverflowPatchCount);
        Assert.Contains(selected, patch => patch.PatchKey == new TerrainPatchKey(0, 0));

        var culled = bounded with { MaximumPatchCount = 8, EnableFrustumCulling = true };
        Assert.Empty(planner.Plan(visible, view, culled).ToArray());
        Assert.Equal(4, planner.Metrics.CulledPatchCount);
    }

    [Fact]
    public void RebaseChangesOnlyOriginRelativeRepresentationAtLargeNegativeCoordinates()
    {
        var placement = new WorldPosition(-8_000_000.25, 40.0, 6_000_000.5);
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            1,
            1,
            resolution: 33,
            tileOrigin: new TerrainTileCoordinate(-4000, 3000),
            worldPlacement: placement,
            height: (x, z) => checked((ushort)(1000 + (x * 13) + (z * 7))));
        TerrainRuntimeDataStore runtimeData = Publish(fixture);
        var planner = new TerrainLodPlanner(runtimeData);
        TerrainTileComponent[] visible = [TerrainRuntimeTestData.CreateComponent(fixture.Tiles[0])];
        WorldPosition camera = new(placement.X + 8.0, placement.Y + 80.0, placement.Z + 8.0);
        var settings = new TerrainLodSettings(2.0, 0.15, 16, false);
        TerrainLodView beforeView = CreatePerspectiveView(default, camera);
        TerrainLodView afterView = CreatePerspectiveView(
            new WorldPosition(-8_000_000.0, 0.0, 6_000_000.0),
            camera);

        TerrainPatchRecord[] before = planner.Plan(visible, beforeView, settings).ToArray();
        TerrainPatchRecord[] after = planner.Plan(visible, afterView, settings).ToArray();

        Assert.Equal(before.Select(Identity), after.Select(Identity));
        Assert.Equal(before.Select(item => item.LodLevel), after.Select(item => item.LodLevel));
        Assert.Equal(before.Select(item => item.WorldBounds), after.Select(item => item.WorldBounds));
        Assert.NotEqual(before[0].OriginRelativeMin, after[0].OriginRelativeMin);
        Assert.InRange(Math.Abs(after[0].OriginRelativeMin.X), 0.0f, 1.0f);
        Assert.InRange(Math.Abs(after[0].OriginRelativeMin.Z), 0.0f, 1.0f);
    }

    [Fact]
    public void WarmPlannerAllocatesNothingPerPatchInSteadyState()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            2,
            2,
            resolution: 33,
            height: (x, z) => checked((ushort)(1000 + ((x * x + z * z) % 50_000))));
        TerrainRuntimeDataStore runtimeData = Publish(fixture);
        var planner = new TerrainLodPlanner(runtimeData);
        TerrainTileComponent[] visible = fixture.Tiles
            .Select(tile => TerrainRuntimeTestData.CreateComponent(tile))
            .ToArray();
        TerrainLodView view = CreatePerspectiveView(
            fixture.Root.WorldPlacement,
            new WorldPosition(16.0, 100.0, 16.0));
        var settings = new TerrainLodSettings(2.0, 0.15, 64, false);
        for (int index = 0; index < 8; index++)
        {
            planner.Plan(visible, view, settings);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        int observed = 0;
        for (int index = 0; index < 100; index++)
        {
            ReadOnlySpan<TerrainPatchRecord> patches = planner.Plan(visible, view, settings);
            observed += patches.Length + patches[0].LodLevel;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(observed > 0);
        Assert.Equal(0, allocated);
    }

    private static TerrainRuntimeDataStore Publish(TerrainRuntimeFixture fixture)
    {
        var runtimeData = new TerrainRuntimeDataStore();
        runtimeData.PublishRoot(fixture.Root);
        foreach (CookedTerrainTile tile in fixture.Tiles)
        {
            runtimeData.PublishTile(tile);
        }

        return runtimeData;
    }

    private static TerrainLodView CreatePerspectiveView(
        WorldPosition renderOrigin,
        WorldPosition cameraWorld) => new(
        cameraWorld,
        renderOrigin,
        Matrix4x4.Identity,
        TerrainLodProjection.Perspective,
        Math.PI / 3.0,
        0.0,
        1080);

    private static object Identity(TerrainPatchRecord patch) => new
    {
        patch.TerrainRootGuid,
        patch.TileGuid,
        patch.TileCoordinate,
        patch.TileGeneration,
        patch.PatchKey
    };

    private static bool IsOrdered(TerrainPatchRecord left, TerrainPatchRecord right)
    {
        int comparison = left.TerrainRootGuid.CompareTo(right.TerrainRootGuid);
        if (comparison != 0) return comparison < 0;
        comparison = left.TileCoordinate.CompareTo(right.TileCoordinate);
        if (comparison != 0) return comparison < 0;
        comparison = left.TileGuid.CompareTo(right.TileGuid);
        if (comparison != 0) return comparison < 0;
        return left.PatchKey.CompareTo(right.PatchKey) < 0;
    }

    private static void AssertMixedEdgesAreCompatible(TerrainPatchRecord[] patches)
    {
        var byLocation = patches.ToDictionary(
            patch => (patch.TileCoordinate.X, patch.TileCoordinate.Z, patch.PatchKey.X, patch.PatchKey.Z));
        bool foundMixedEdge = false;
        foreach (TerrainPatchRecord patch in patches)
        {
            CheckNeighbor(
                patch,
                ResolveNeighbor(patch, positiveX: true),
                TerrainPatchEdge.PositiveX,
                TerrainPatchEdge.NegativeX);
            CheckNeighbor(
                patch,
                ResolveNeighbor(patch, positiveX: false),
                TerrainPatchEdge.PositiveZ,
                TerrainPatchEdge.NegativeZ);
        }

        Assert.True(foundMixedEdge);
        return;

        TerrainPatchRecord? ResolveNeighbor(TerrainPatchRecord patch, bool positiveX)
        {
            int tileX = patch.TileCoordinate.X;
            int tileZ = patch.TileCoordinate.Z;
            int patchX = patch.PatchKey.X;
            int patchZ = patch.PatchKey.Z;
            if (positiveX)
            {
                if (patchX == 1)
                {
                    tileX++;
                    patchX = 0;
                }
                else patchX++;
            }
            else
            {
                if (patchZ == 1)
                {
                    tileZ++;
                    patchZ = 0;
                }
                else patchZ++;
            }

            return byLocation.TryGetValue((tileX, tileZ, patchX, patchZ), out TerrainPatchRecord value)
                ? value
                : null;
        }

        void CheckNeighbor(
            TerrainPatchRecord patch,
            TerrainPatchRecord? neighbor,
            TerrainPatchEdge patchEdge,
            TerrainPatchEdge neighborEdge)
        {
            if (!neighbor.HasValue)
            {
                return;
            }

            TerrainPatchRecord other = neighbor.Value;
            Assert.InRange(Math.Abs(patch.LodLevel - other.LodLevel), 0, 1);
            if (patch.LodLevel == other.LodLevel)
            {
                return;
            }

            foundMixedEdge = true;
            TerrainPatchRecord finer = patch.LodLevel < other.LodLevel ? patch : other;
            TerrainPatchRecord coarser = patch.LodLevel > other.LodLevel ? patch : other;
            TerrainPatchEdge finerEdge = patch.LodLevel < other.LodLevel
                ? patchEdge
                : neighborEdge;
            Assert.Equal(
                coarser.SampleStep,
                TerrainPatchTopology.GetEffectiveEdgeSampleStep(finer, finerEdge));
        }
    }
}
