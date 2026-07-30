using ArisenEngine.Core.ECS;
using ArisenEngine.Terrain;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainRenderExtractionTests
{
    [Fact]
    public void VisibleTileExtractionIsContiguousDeterministicAndReusable()
    {
        var world = new EntityManager();
        Guid root = Guid.Parse("51000000-0000-0000-0000-000000000001");
        AddTile(world, root, tileX: 1, tileZ: 0, visible: true);
        AddTile(world, root, tileX: 0, tileZ: 1, visible: true);
        AddTile(world, root, tileX: 0, tileZ: 0, visible: false);
        AddTile(world, root, tileX: -1, tileZ: 0, visible: true);
        var source = new TerrainTileRenderSource(() => world);

        TerrainTileComponent[] first = source.ExtractVisibleTiles().ToArray();
        TerrainTileComponent[] second = source.ExtractVisibleTiles().ToArray();

        Assert.Equal(3, first.Length);
        Assert.Equal(first, second);
        Assert.Equal(
            [(0, -1), (0, 1), (1, 0)],
            first.Select(tile => (tile.TileZ, tile.TileX)));
    }

    [Fact]
    public void ExtractionFromAWorldWithoutTerrainReturnsEmpty()
    {
        var source = new TerrainTileRenderSource(() => new EntityManager());

        Assert.Empty(source.ExtractVisibleTiles().ToArray());
    }

    private static void AddTile(
        EntityManager world,
        Guid root,
        int tileX,
        int tileZ,
        bool visible)
    {
        Entity entity = world.CreateEntity();
        Guid tileGuid = new(
            tileX + 100,
            checked((short)(tileZ + 100)),
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8);
        world.AddComponent(
            entity,
            new TerrainTileComponent
            {
                TerrainRootGuid = root,
                TileGuid = tileGuid,
                LayerSetGuid = Guid.Parse("51000000-0000-0000-0000-000000000002"),
                TileX = tileX,
                TileZ = tileZ,
                Flags = visible ? TerrainTileFlags.Visible : TerrainTileFlags.None
            });
    }
}
