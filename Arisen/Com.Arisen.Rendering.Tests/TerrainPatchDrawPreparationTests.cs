using System.Numerics;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using ArisenEngine.Terrain.GenericRenderPipeline;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainPatchDrawPreparationTests
{
    [Fact]
    public void PlannedMixedLodPatchesResolveToBoundedSharedGridDraws()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            2,
            1,
            resolution: 33,
            height: (x, z) => x < 16 && z < 16
                ? checked((ushort)(1000 + (((x + z) & 1) * 30_000)))
                : (ushort)1000);
        var runtimeData = new TerrainRuntimeDataStore();
        runtimeData.PublishRoot(fixture.Root);
        foreach (CookedTerrainTile tile in fixture.Tiles)
        {
            runtimeData.PublishTile(tile);
        }

        var planner = new TerrainLodPlanner(runtimeData);
        TerrainTileComponent[] visible = fixture.Tiles
            .Select(tile => TerrainRuntimeTestData.CreateComponent(tile))
            .ToArray();
        var view = new TerrainLodView(
            new WorldPosition(8.0, 100.0, 8.0),
            fixture.Root.WorldPlacement,
            Matrix4x4.Identity,
            TerrainLodProjection.Perspective,
            Math.PI / 3.0,
            0.0,
            1080);
        ReadOnlySpan<TerrainPatchRecord> patches = planner.Plan(
            visible,
            view,
            new TerrainLodSettings(4.0, 0.15, 64, false));
        TerrainGridIndexPatternSet patterns =
            TerrainSharedGridBuilder.CreateIndexPatterns(33);

        Assert.NotEmpty(patches.ToArray());
        Assert.Contains(patches.ToArray(), patch => patch.StitchMask != TerrainPatchStitchMask.None);
        for (int index = 0; index < patches.Length; index++)
        {
            ref readonly TerrainPatchRecord patch = ref patches[index];
            Assert.True(patterns.TryResolvePatchGeometry(
                patch,
                out TerrainGridIndexRange range,
                out int vertexOffset));
            Assert.True(range.IsValid);
            Assert.Equal(
                (patch.SampleZ * patterns.Resolution) + patch.SampleX,
                vertexOffset);

            ReadOnlySpan<uint> drawIndices = patterns.Indices.AsSpan(
                checked((int)range.FirstIndex),
                checked((int)range.IndexCount));
            uint maximumVertex = drawIndices.ToArray().Max() + checked((uint)vertexOffset);
            Assert.InRange(maximumVertex, 0u, checked((uint)(33 * 33 - 1)));
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(256, 1)]
    [InlineData(257, 2)]
    [InlineData(1025, 5)]
    public void DrawWorkPartitionProducesContiguousBoundedRanges(
        int drawCount,
        int expectedWorkItems)
    {
        int workItemCount = TerrainDrawWorkPartition.GetWorkItemCount(drawCount);

        Assert.Equal(expectedWorkItems, workItemCount);
        int consumed = 0;
        for (int index = 0; index < workItemCount; index++)
        {
            Assert.True(TerrainDrawWorkPartition.TryGetRange(
                drawCount,
                index,
                out TerrainDrawRange range));
            Assert.True(range.IsValid);
            Assert.Equal(consumed, range.Start);
            Assert.InRange(
                range.Count,
                1,
                TerrainDrawWorkPartition.MaximumDrawsPerWorkItem);
            consumed += range.Count;
        }

        Assert.Equal(drawCount, consumed);
        Assert.False(TerrainDrawWorkPartition.TryGetRange(
            drawCount,
            workItemCount,
            out _));
    }

    [Fact]
    public void PlannedPatchesPreserveTerrainShadowFlags()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            1,
            1,
            resolution: 33);
        var runtimeData = new TerrainRuntimeDataStore();
        runtimeData.PublishRoot(fixture.Root);
        runtimeData.PublishTile(fixture.Tiles[0]);
        TerrainTileComponent component = TerrainRuntimeTestData.CreateComponent(
            fixture.Tiles[0]);
        component.Flags = TerrainTileFlags.Visible |
            TerrainTileFlags.CastShadows |
            TerrainTileFlags.ReceiveShadows;
        var planner = new TerrainLodPlanner(runtimeData);
        var view = new TerrainLodView(
            new WorldPosition(8.0, 100.0, 8.0),
            fixture.Root.WorldPlacement,
            Matrix4x4.Identity,
            TerrainLodProjection.Perspective,
            Math.PI / 3.0,
            0.0,
            1080);

        ReadOnlySpan<TerrainPatchRecord> patches = planner.Plan(
            [component],
            view,
            new TerrainLodSettings(4.0, 0.15, 64, false));

        Assert.NotEmpty(patches.ToArray());
        Assert.All(
            patches.ToArray(),
            patch => Assert.Equal(component.Flags, patch.TileFlags));
    }
}
