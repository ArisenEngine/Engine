using System.Numerics;
using ArisenEditor.Terrain;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainDiagnosticsSnapshotTests
{
    [Fact]
    public void SnapshotJoinsCookedIdentityLodResidencyAndValidSeamsDeterministically()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            2,
            1,
            resolution: 17,
            height: (x, z) => checked((ushort)(1000 + (x * 11) + (z * 7))));
        var runtime = new TerrainRuntimeDataStore();
        runtime.PublishRoot(fixture.Root);
        foreach (CookedTerrainTile tile in fixture.Tiles) runtime.PublishTile(tile);
        var planner = new TerrainLodPlanner(runtime);
        TerrainTileComponent[] visible = fixture.Tiles
            .Select(tile => TerrainRuntimeTestData.CreateComponent(tile))
            .ToArray();
        var view = new TerrainLodView(
            new WorldPosition(8.0, 80.0, 8.0),
            default,
            Matrix4x4.Identity,
            TerrainLodProjection.Perspective,
            Math.PI / 3.0,
            0.0,
            1080);
        TerrainPatchRecord[] patches = planner.Plan(
            visible,
            view,
            new TerrainLodSettings(2.0, 0.15, 32, false)).ToArray();
        RuntimeAssetResidencyOwnerId owner = RuntimeAssetResidencyOwnerId.Cell(
            Guid.Parse("4fa73632-3669-4b55-b098-7912a4c9eb19"),
            new WorldCellId(Guid.Parse("c6dccb0e-2505-46d7-8a91-14cc643dded1")),
            2);
        TerrainDiagnosticRootInput[] roots =
        [
            new(
                fixture.Root,
                10,
                RuntimePreparedAssetState.Ready,
                512,
                256,
                false,
                string.Empty,
                [owner])
        ];
        TerrainDiagnosticTileInput[] tiles = fixture.Tiles
            .Reverse()
            .Select((tile, index) => CreateTileInput(
                fixture.Root,
                tile,
                checked((ulong)(20 + index)),
                [owner]))
            .ToArray();
        var metrics = new TerrainResidencyMetrics(
            1, 2, 1024, 2048, 128, 1024, 2048, 128, 256, 1, 0, 2, 0.25, 0);
        TerrainResidencyResourceSnapshot[] resources = fixture.Tiles
            .Select(tile => new TerrainResidencyResourceSnapshot(
                new RuntimeAssetResidencyKey(
                    tile.Guid,
                    tile.PackageId,
                    TerrainAssetTypes.Tile,
                    TerrainTileAssetCooker.RuntimeVariant),
                tile.RootGuid,
                tile.Guid,
                tile.Coordinate,
                RuntimePreparedAssetState.Ready,
                1600,
                1600,
                1,
                [owner],
                string.Empty))
            .ToArray();

        TerrainDiagnosticsSnapshot snapshot = TerrainDiagnosticsSnapshotBuilder.Build(
            42,
            metrics,
            planner.Metrics,
            view.CameraWorldPosition,
            default,
            roots,
            tiles,
            patches,
            resources);

        Assert.Equal(42u, snapshot.FrameIndex);
        Assert.Equal(fixture.Tiles.Select(tile => tile.Guid), snapshot.Tiles.Select(tile => tile.TileGuid));
        Assert.Equal(TerrainRootAssetCooker.CookedFormatVersion, Assert.Single(snapshot.Roots).CookedVersion);
        Assert.Equal(fixture.Root.LayerSetGuid, snapshot.Roots[0].LayerSetGuid);
        Assert.All(snapshot.Tiles, tile =>
        {
            Assert.True(tile.Generation > 0);
            Assert.True(tile.MinimumSelectedLod >= 0);
            Assert.NotEmpty(tile.Patches);
            Assert.Equal(RuntimePreparedAssetState.Ready, tile.ResidencyState);
            Assert.Equal(owner, Assert.Single(tile.Owners));
        });
        Assert.Equal(TerrainSeamDiagnosticState.Valid, snapshot.Tiles[0].Neighbors.PositiveX.State);
        Assert.Equal(TerrainSeamDiagnosticState.Valid, snapshot.Tiles[1].Neighbors.NegativeX.State);
        Assert.Equal(0, snapshot.SeamViolationCount);
        Assert.Equal(0, snapshot.DroppedPatchCount);
    }

    [Fact]
    public void SnapshotDistinguishesUnavailableNeighborFromCorruptSharedEdge()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            2,
            1,
            resolution: 17,
            height: (x, z) => checked((ushort)(2000 + x + z)));
        CookedTerrainTile left = fixture.Tiles[0];
        CookedTerrainTile corruptRight = WithCorruptNegativeX(fixture.Tiles[1]);
        TerrainDiagnosticTileInput leftInput = CreateTileInput(
            fixture.Root,
            left,
            1,
            Array.Empty<RuntimeAssetResidencyOwnerId>());
        TerrainDiagnosticTileInput corruptInput = CreateTileInput(
            fixture.Root,
            corruptRight,
            2,
            Array.Empty<RuntimeAssetResidencyOwnerId>());

        TerrainDiagnosticsSnapshot unavailable = Build(fixture.Root, [leftInput]);
        TerrainDiagnosticsSnapshot corrupt = Build(fixture.Root, [leftInput, corruptInput]);

        Assert.Equal(
            TerrainSeamDiagnosticState.NeighborUnavailable,
            Assert.Single(unavailable.Tiles).Neighbors.PositiveX.State);
        Assert.Equal(0, unavailable.SeamViolationCount);
        Assert.Equal(
            TerrainSeamDiagnosticState.HeightMismatch,
            corrupt.Tiles[0].Neighbors.PositiveX.State);
        Assert.True(corrupt.Tiles[0].Neighbors.PositiveX.HeightMismatchCount > 0);
        Assert.Equal(1, corrupt.SeamViolationCount);
    }

    [Fact]
    public void SmokeDiscoveryRequiresPublishedLodFrameBeforeSchedulingCapture()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(1, 1, resolution: 17);
        CookedTerrainTile tile = fixture.Tiles[0];
        TerrainDiagnosticTileInput input = CreateTileInput(
            fixture.Root,
            tile,
            1,
            Array.Empty<RuntimeAssetResidencyOwnerId>());
        TerrainDiagnosticsSnapshot preparedOnly = Build(fixture.Root, [input]);

        var runtime = new TerrainRuntimeDataStore();
        runtime.PublishRoot(fixture.Root);
        runtime.PublishTile(tile);
        var planner = new TerrainLodPlanner(runtime);
        var view = new TerrainLodView(
            new WorldPosition(8.0, 20.0, 8.0),
            default,
            Matrix4x4.Identity,
            TerrainLodProjection.Perspective,
            Math.PI / 3.0,
            0.0,
            1080);
        TerrainPatchRecord[] patches = planner.Plan(
            [TerrainRuntimeTestData.CreateComponent(tile)],
            view,
            new TerrainLodSettings(2.0, 0.15, 32, false)).ToArray();
        TerrainDiagnosticsSnapshot rendered = Build(
            fixture.Root,
            [input],
            planner.Metrics,
            patches);

        Assert.False(TerrainStreamingSmokeScenario.HasCompleteRenderSnapshot(
            preparedOnly,
            Assert.Single(preparedOnly.Roots),
            preparedOnly.Tiles));
        Assert.True(TerrainStreamingSmokeScenario.HasCompleteRenderSnapshot(
            rendered,
            Assert.Single(rendered.Roots),
            rendered.Tiles));
    }

    [Fact]
    public void EditorSelectionAndOverlayVisibilitySurviveEmptyRuntimeRefresh()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(1, 1, resolution: 17);
        TerrainDiagnosticTileInput input = CreateTileInput(
            fixture.Root,
            fixture.Tiles[0],
            1,
            Array.Empty<RuntimeAssetResidencyOwnerId>());
        TerrainDiagnosticsSnapshot populated = Build(fixture.Root, [input]);
        var session = new TerrainEditorDiagnosticsSession();
        session.Select(fixture.Root.Guid, fixture.Tiles[0].Guid);
        session.IsOverlayVisible = false;

        Assert.NotNull(session.ResolveTile(populated));
        Assert.Null(session.ResolveTile(TerrainDiagnosticsSnapshot.Empty));
        Assert.Equal(fixture.Root.Guid, session.SelectedRootGuid);
        Assert.Equal(fixture.Tiles[0].Guid, session.SelectedTileGuid);
        Assert.False(session.IsOverlayVisible);
        Assert.NotNull(session.ResolveTile(populated));
    }

    private static TerrainDiagnosticsSnapshot Build(
        CookedTerrainRoot root,
        TerrainDiagnosticTileInput[] tiles,
        TerrainLodMetrics lod = default,
        TerrainPatchRecord[]? patches = null)
    {
        TerrainDiagnosticRootInput[] roots =
        [
            new(
                root,
                1,
                RuntimePreparedAssetState.Ready,
                0,
                0,
                false,
                string.Empty,
                Array.Empty<RuntimeAssetResidencyOwnerId>())
        ];
        return TerrainDiagnosticsSnapshotBuilder.Build(
            1,
            new TerrainResidencyMetrics(1, tiles.Length, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            lod,
            default,
            default,
            roots,
            tiles,
            patches is null ? ReadOnlySpan<TerrainPatchRecord>.Empty : patches,
            Array.Empty<TerrainResidencyResourceSnapshot>());
    }

    private static TerrainDiagnosticTileInput CreateTileInput(
        CookedTerrainRoot root,
        CookedTerrainTile tile,
        ulong generation,
        IReadOnlyList<RuntimeAssetResidencyOwnerId> owners)
    {
        CookedTerrainTileReference reference = root.Tiles.Single(item => item.Guid == tile.Guid);
        return new TerrainDiagnosticTileInput(
            root,
            reference,
            tile,
            generation,
            RuntimePreparedAssetState.Ready,
            tile.Heights.Length * sizeof(ushort),
            tile.LayerWeights.Length,
            tile.GeometricErrors.Count * 16L,
            4096,
            true,
            false,
            string.Empty,
            owners);
    }

    private static CookedTerrainTile WithCorruptNegativeX(CookedTerrainTile tile)
    {
        ushort[] heights = tile.Heights.ToArray();
        heights[0] = heights[0] == ushort.MaxValue
            ? (ushort)(heights[0] - 1)
            : (ushort)(heights[0] + 1);
        return new CookedTerrainTile(
            tile.Guid,
            tile.RootGuid,
            tile.LayerSetGuid,
            tile.PackageId,
            tile.SourceSchemaVersion,
            tile.Coordinate,
            tile.Resolution,
            tile.LayerCount,
            tile.WorldPlacement,
            tile.SampleSpacing,
            tile.HeightRange,
            tile.MinHeight,
            tile.MaxHeight,
            tile.BorderPolicy,
            tile.SourceSampleOffsetX,
            tile.SourceSampleOffsetZ,
            heights,
            tile.LayerWeights.ToArray(),
            tile.GeometricErrors.ToArray());
    }
}
