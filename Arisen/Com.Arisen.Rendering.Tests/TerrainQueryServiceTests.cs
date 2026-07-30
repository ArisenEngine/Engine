using System.Numerics;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainQueryServiceTests
{
    [Fact]
    public void QueryReturnsHeightNormalWeightsAndPositiveBorderOwner()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            tileCountX: 2,
            tileCountZ: 1,
            resolution: 17,
            worldPlacement: new WorldPosition(10_000.25, 7.0, -20_000.5),
            sampleSpacing: new TerrainSampleSpacing(2.0, 3.0),
            height: (x, z) => checked((ushort)(1000 + (x * 100) + (z * 25))),
            weights: (_, _) => (64, 64, 64, 63));
        var world = new EntityManager();
        foreach (CookedTerrainTile tile in fixture.Tiles)
        {
            Entity entity = world.CreateEntity();
            world.AddComponent(entity, TerrainRuntimeTestData.CreateComponent(tile));
        }

        var runtimeData = new TerrainRuntimeDataStore();
        runtimeData.PublishRoot(fixture.Root);
        foreach (CookedTerrainTile tile in fixture.Tiles)
        {
            runtimeData.PublishTile(tile);
        }
        var queries = new TerrainQueryService(runtimeData, () => world);
        CookedTerrainTile positiveTile = fixture.Tiles[1];
        double sharedBorderX = positiveTile.WorldPlacement.X;
        double sampleZ = positiveTile.WorldPlacement.Z + (5.5 * positiveTile.SampleSpacing.Z);

        TerrainQueryResult result = queries.Query(
            new WorldPosition(sharedBorderX, 5000.0, sampleZ));

        Assert.Equal(TerrainQueryStatus.Available, result.Status);
        Assert.Equal(positiveTile.Guid, result.TileGuid);
        Assert.Equal(positiveTile.Coordinate, result.Coordinate);
        Assert.True(result.TileGeneration > 0);
        Assert.Equal(sharedBorderX, result.SurfacePosition.X);
        Assert.Equal(sampleZ, result.SurfacePosition.Z);
        Assert.True(double.IsFinite(result.SurfacePosition.Y));
        Assert.InRange(result.Normal.Length(), 0.99999f, 1.00001f);
        Assert.True(result.Normal.Y > 0.0f);
        Assert.InRange(
            result.LayerWeights.X + result.LayerWeights.Y +
            result.LayerWeights.Z + result.LayerWeights.W,
            0.99999f,
            1.00001f);
        Assert.Equal(new Vector4(64 / 255.0f, 64 / 255.0f, 64 / 255.0f, 63 / 255.0f),
            result.LayerWeights);
    }

    [Fact]
    public void QueryNeverLoadsAndDistinguishesInvalidOutsideAndInactiveResidency()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(1, 1, resolution: 3);
        var world = new EntityManager();
        var runtimeData = new TerrainRuntimeDataStore();
        runtimeData.PublishRoot(fixture.Root);
        var queries = new TerrainQueryService(runtimeData, () => world);
        WorldPosition inside = fixture.Root.WorldPlacement;

        Assert.Equal(
            TerrainQueryStatus.InvalidPosition,
            queries.Query(new WorldPosition(double.NaN, 0.0, 0.0)).Status);
        Assert.Equal(
            TerrainQueryStatus.OutsideTerrain,
            queries.Query(new WorldPosition(inside.X - 1.0, 0.0, inside.Z)).Status);
        TerrainQueryResult missing = queries.Query(inside);
        Assert.Equal(TerrainQueryStatus.Unavailable, missing.Status);
        Assert.Equal(fixture.Tiles[0].Guid, missing.TileGuid);

        runtimeData.PublishTile(fixture.Tiles[0]);
        Assert.Equal(TerrainQueryStatus.Unavailable, queries.Query(inside).Status);
        Entity entity = world.CreateEntity();
        world.AddComponent(entity, TerrainRuntimeTestData.CreateComponent(fixture.Tiles[0]));
        Assert.Equal(TerrainQueryStatus.Available, queries.Query(inside).Status);
        world.DestroyEntity(entity);
        Assert.Equal(TerrainQueryStatus.Unavailable, queries.Query(inside).Status);
    }

    [Fact]
    public void StaleGenerationCannotRemoveReplacementTile()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(1, 1, resolution: 3);
        var world = new EntityManager();
        Entity entity = world.CreateEntity();
        world.AddComponent(entity, TerrainRuntimeTestData.CreateComponent(fixture.Tiles[0]));
        var runtimeData = new TerrainRuntimeDataStore();
        runtimeData.PublishRoot(fixture.Root);
        TerrainResidentResourceHandle first = runtimeData.PublishTile(fixture.Tiles[0]);
        TerrainResidentResourceHandle replacement = runtimeData.PublishTile(fixture.Tiles[0]);
        var queries = new TerrainQueryService(runtimeData, () => world);

        Assert.True(replacement.Generation > first.Generation);
        Assert.False(runtimeData.Remove(first));
        TerrainQueryResult current = queries.Query(fixture.Root.WorldPlacement);
        Assert.Equal(TerrainQueryStatus.Available, current.Status);
        Assert.Equal(replacement.Generation, current.TileGeneration);
        Assert.True(runtimeData.Remove(replacement));
        Assert.Equal(
            TerrainQueryStatus.Unavailable,
            queries.Query(fixture.Root.WorldPlacement).Status);
    }

    [Fact]
    public void RootAndTileReplacementPublishesOneValidatedGenerationSet()
    {
        TerrainRuntimeFixture first = TerrainRuntimeTestData.Create(
            1,
            1,
            resolution: 3,
            height: (_, _) => 1000);
        TerrainRuntimeFixture second = TerrainRuntimeTestData.Create(
            1,
            1,
            resolution: 3,
            height: (_, _) => 50_000);
        var world = new EntityManager();
        Entity entity = world.CreateEntity();
        world.AddComponent(entity, TerrainRuntimeTestData.CreateComponent(first.Tiles[0]));
        var runtimeData = new TerrainRuntimeDataStore();
        TerrainResidentResourceHandle firstRoot = runtimeData.PublishRoot(first.Root);
        TerrainResidentResourceHandle firstTile = runtimeData.PublishTile(first.Tiles[0]);
        var queries = new TerrainQueryService(runtimeData, () => world);
        TerrainQueryResult before = queries.Query(first.Root.WorldPlacement);

        TerrainRuntimePublication publication = runtimeData.PublishReplacement(
            second.Root,
            second.Tiles);
        TerrainQueryResult after = queries.Query(second.Root.WorldPlacement);

        Assert.True(publication.Root.Generation > firstRoot.Generation);
        Assert.Single(publication.Tiles);
        Assert.True(publication.Tiles[0].Generation > firstTile.Generation);
        Assert.False(runtimeData.Remove(firstRoot));
        Assert.False(runtimeData.Remove(firstTile));
        Assert.Equal(TerrainQueryStatus.Available, after.Status);
        Assert.Equal(publication.Tiles[0].Generation, after.TileGeneration);
        Assert.True(after.SurfacePosition.Y > before.SurfacePosition.Y);
    }

    [Fact]
    public void RootAndTilePublicationRejectsMismatchedLayerGenerations()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(1, 1, resolution: 3);
        CookedTerrainLayer firstLayer = fixture.Root.Layers[0];
        CookedTerrainRoot replacementRoot = fixture.Root with
        {
            Layers = [firstLayer, firstLayer with { Id = "replacement" }]
        };
        var runtimeData = new TerrainRuntimeDataStore();

        runtimeData.PublishRoot(fixture.Root);
        runtimeData.PublishTile(fixture.Tiles[0]);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            runtimeData.PublishReplacement(replacementRoot, []));
        Assert.Contains("does not match resident root", error.Message, StringComparison.Ordinal);

        var world = new EntityManager();
        Entity entity = world.CreateEntity();
        world.AddComponent(entity, TerrainRuntimeTestData.CreateComponent(fixture.Tiles[0]));
        var queries = new TerrainQueryService(runtimeData, () => world);
        Assert.Equal(
            TerrainQueryStatus.Available,
            queries.Query(fixture.Root.WorldPlacement).Status);
    }

    [Fact]
    public void InvalidRootAndTileReplacementRetainsPreviousQueryableState()
    {
        TerrainRuntimeFixture first = TerrainRuntimeTestData.Create(
            1,
            1,
            resolution: 3,
            height: (_, _) => 1000);
        TerrainRuntimeFixture incompatible = TerrainRuntimeTestData.Create(
            1,
            1,
            resolution: 3,
            height: (_, _) => 50_000);
        var world = new EntityManager();
        Entity entity = world.CreateEntity();
        world.AddComponent(entity, TerrainRuntimeTestData.CreateComponent(first.Tiles[0]));
        var runtimeData = new TerrainRuntimeDataStore();
        runtimeData.PublishRoot(first.Root);
        TerrainResidentResourceHandle firstTile = runtimeData.PublishTile(first.Tiles[0]);
        var queries = new TerrainQueryService(runtimeData, () => world);
        TerrainQueryResult before = queries.Query(first.Root.WorldPlacement);

        Assert.Throws<InvalidOperationException>(() =>
            runtimeData.PublishReplacement(incompatible.Root, first.Tiles));
        TerrainQueryResult after = queries.Query(first.Root.WorldPlacement);

        Assert.Equal(TerrainQueryStatus.Available, after.Status);
        Assert.Equal(firstTile.Generation, after.TileGeneration);
        Assert.Equal(before.SurfacePosition, after.SurfacePosition);
    }
}
