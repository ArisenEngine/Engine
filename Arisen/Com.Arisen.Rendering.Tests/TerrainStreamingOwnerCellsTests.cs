using System.Text.Json;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using ArisenKernel.Lifecycle;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

/// <summary>
/// The fixture used to require exactly one world cell for the whole resident terrain root, so a
/// root whose tiles are owned by several cells could not be validated at all. These tests pin the
/// owner-cell set the fixture derives from the tiles' residency owners, the per-owner-cell
/// generation check the reload soak performs, the per-cell drain decision, and the artifact field
/// names the terrain gate script reads.
/// </summary>
public sealed class TerrainStreamingOwnerCellsTests
{
    private static readonly Guid s_WorldGuid = Guid.Parse("71000000-0000-0000-0000-000000000010");
    private static readonly Guid s_RootGuid = Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid s_LayerSetGuid = Guid.Parse("71000000-0000-0000-0000-000000000002");
    private const string PackageId = "com.arisen.tests.terrain";

    [Fact]
    public void TryCreate_CollectsEveryOwnerCellOfTheRootInStableOrder()
    {
        WorldCellId west = Cell("71000000-0000-0000-0000-000000000020");
        WorldCellId east = Cell("71000000-0000-0000-0000-000000000021");
        TerrainStreamingOwnerCells? candidates = TerrainStreamingOwnerCells.TryCreate(
            CreateWorld(west, east),
            [
                CreateTile(0, Owners(Owner(4, west))),
                CreateTile(1, Owners(Owner(5, east), Owner(5, west)))
            ],
            out string diagnostic);

        Assert.True(candidates != null, diagnostic);
        TerrainStreamingOwnerCells ownerCells = candidates!;
        Assert.Equal(string.Empty, diagnostic);
        Assert.Equal(2, ownerCells.Count);
        Assert.Equal(new[] { west, east }.Order().ToArray(), ownerCells.Ids());
        Assert.Equal(ownerCells.Ids().Select(id => id.ToString()).ToArray(), ownerCells.IdStrings());
        Assert.True(ownerCells.Contains(west));
        Assert.True(ownerCells.Contains(east));
    }

    [Fact]
    public void TryCreate_RejectsATileOwnerCellTheActiveWorldDoesNotDeclare()
    {
        WorldCellId west = Cell("71000000-0000-0000-0000-000000000020");
        WorldCellId stranger = Cell("71000000-0000-0000-0000-000000000022");
        TerrainStreamingOwnerCells? ownerCells = TerrainStreamingOwnerCells.TryCreate(
            CreateWorld(west),
            [CreateTile(0, Owners(Owner(4, west), Owner(4, stranger)))],
            out string diagnostic);

        Assert.Null(ownerCells);
        Assert.Contains(stranger.ToString(), diagnostic, StringComparison.Ordinal);
        Assert.Contains("does not declare", diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_RejectsARootWithNoWorldCellOwner()
    {
        WorldCellId west = Cell("71000000-0000-0000-0000-000000000020");
        TerrainStreamingOwnerCells? ownerCells = TerrainStreamingOwnerCells.TryCreate(
            CreateWorld(west),
            [CreateTile(0, [RuntimeAssetResidencyOwnerId.Persistent(s_WorldGuid, 3)])],
            out string diagnostic);

        Assert.Null(ownerCells);
        Assert.Contains("no world-cell residency owner", diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_RejectsATileThatNoOwnerCellOfTheSetOwns()
    {
        WorldCellId west = Cell("71000000-0000-0000-0000-000000000020");
        TerrainTileDiagnosticSnapshot unowned = CreateTile(1, []);
        TerrainStreamingOwnerCells? ownerCells = TerrainStreamingOwnerCells.TryCreate(
            CreateWorld(west),
            [CreateTile(0, Owners(Owner(4, west))), unowned],
            out string diagnostic);

        Assert.Null(ownerCells);
        Assert.Contains(unowned.TileGuid.ToString(), diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void IsTileOwnedBySet_IgnoresCellsOutsideTheSetAndThePersistentScene()
    {
        WorldCellId west = Cell("71000000-0000-0000-0000-000000000020");
        WorldCellId east = Cell("71000000-0000-0000-0000-000000000021");
        WorldCellId stranger = Cell("71000000-0000-0000-0000-000000000022");
        TerrainStreamingOwnerCells ownerCells = CreateOwnerCells(west, east);

        Assert.True(ownerCells.IsTileOwnedBySet(CreateTile(0, Owners(Owner(4, west)))));
        Assert.True(ownerCells.IsTileOwnedBySet(
            CreateTile(1, [RuntimeAssetResidencyOwnerId.Persistent(s_WorldGuid, 3), Owner(4, east)])));
        Assert.False(ownerCells.IsTileOwnedBySet(CreateTile(2, Owners(Owner(4, stranger)))));
        Assert.False(ownerCells.IsTileOwnedBySet(
            CreateTile(3, [RuntimeAssetResidencyOwnerId.Persistent(s_WorldGuid, 3)])));
    }

    [Fact]
    public void IsTileOwnedByCurrentGeneration_RequiresAnOwnerCellAtItsCurrentGeneration()
    {
        WorldCellId west = Cell("71000000-0000-0000-0000-000000000020");
        WorldCellId east = Cell("71000000-0000-0000-0000-000000000021");
        WorldCellId stranger = Cell("71000000-0000-0000-0000-000000000022");
        TerrainStreamingOwnerCells ownerCells = CreateOwnerCells(west, east);
        var current = new Dictionary<WorldCellId, long> { [west] = 4, [east] = 8 };

        // The west entry is stale, but the tile is still held by east under east's new generation.
        Assert.True(ownerCells.IsTileOwnedByCurrentGeneration(
            CreateTile(0, Owners(Owner(3, west), Owner(8, east))),
            current));
        Assert.False(ownerCells.IsTileOwnedByCurrentGeneration(
            CreateTile(1, Owners(Owner(3, west), Owner(7, east))),
            current));

        // A tile with one owner cell is only current once that cell re-acquired it.
        TerrainTileDiagnosticSnapshot westOnly = CreateTile(2, Owners(Owner(3, west)));
        Assert.False(ownerCells.IsTileOwnedByCurrentGeneration(westOnly, current));
        current[west] = 3;
        Assert.True(ownerCells.IsTileOwnedByCurrentGeneration(westOnly, current));

        // A tile held only by a cell outside the owner set never counts as current.
        Assert.False(ownerCells.IsTileOwnedByCurrentGeneration(
            CreateTile(3, Owners(Owner(3, stranger))),
            current));
    }

    [Fact]
    public void DrainSnapshot_RequiresEveryOwnerCellToBeUnloadedAndUnpinned()
    {
        var active = new TerrainStreamingCellDrainSnapshot(
            "west",
            Tracked: true,
            WorldCellStreamingState.Active,
            Desired: true,
            WorldCellDesiredSource.EditPin,
            Pinned: false);
        var unloaded = new TerrainStreamingCellDrainSnapshot(
            "east",
            Tracked: true,
            WorldCellStreamingState.Unloaded,
            Desired: false,
            WorldCellDesiredSource.None,
            Pinned: false);

        Assert.False(Drain([active, unloaded]).IsDrained);
        Assert.False(Drain([unloaded, active]).IsDrained);
        Assert.True(Drain([unloaded, unloaded]).IsDrained);
        Assert.False(Drain(
            [
                unloaded,
                TerrainStreamingCellDrainSnapshot.Untracked(
                    Cell("71000000-0000-0000-0000-000000000022"))
            ]).IsDrained);
        Assert.False(default(TerrainStreamingDrainSnapshot).IsDrained);
    }

    [Fact]
    public void Artifact_PublishesTheOwnerCellSetAndPerCellDrainRows()
    {
        WorldCellId west = Cell("71000000-0000-0000-0000-000000000020");
        WorldCellId east = Cell("71000000-0000-0000-0000-000000000021");
        string[] ownerCellIds = { west.ToString(), east.ToString() };
        WorldCellId[] ids = { west, east };
        TerrainStreamingTileSnapshot[] tiles =
        [
            new(
                s_RootGuid,
                new TerrainTileCoordinate(0, 0),
                Generation: 7,
                MinimumLod: 0,
                MaximumLod: 4,
                PatchCount: 3,
                CreateBounds(0),
                SeamViolationCount: 0,
                OwnerCellIds: ownerCellIds)
        ];
        var checkpoint = new TerrainStreamingSmokeCheckpoint(
            Name: "near",
            FrameIndex: 12,
            CameraWorldPosition: new WorldPosition(0.0, 8.0, 0.0),
            Origin: default,
            TerrainRootGuid: s_RootGuid,
            TerrainCellIds: ownerCellIds,
            Tiles: tiles,
            LodHistogram: [],
            Lod: default,
            SeamViolationCount: 0,
            EcsTileCount: tiles.Length,
            QuerySamples: [],
            Memory: new TerrainStreamingMemorySnapshot(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            Passed: true);
        TerrainStreamingDrainSnapshot drain = Drain(
        [
            new TerrainStreamingCellDrainSnapshot(
                west.ToString(),
                Tracked: true,
                WorldCellStreamingState.Unloaded,
                Desired: false,
                WorldCellDesiredSource.None,
                Pinned: false),
            new TerrainStreamingCellDrainSnapshot(
                east.ToString(),
                Tracked: true,
                WorldCellStreamingState.Cancelled,
                Desired: false,
                WorldCellDesiredSource.None,
                Pinned: false)
        ]);
        var artifact = new TerrainStreamingSmokeArtifact(
            SchemaVersion: 2,
            CapturedAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Mode: "terrain-streaming",
            Profile: "Development",
            WorldGuid: s_WorldGuid,
            TerrainRootGuid: s_RootGuid,
            TerrainCellIds: ownerCellIds,
            Passed: true,
            Failure: null,
            RequestedSoakCycles: 4,
            CompletedSoakCycles: 4,
            RebaseSequences: [1L],
            Checkpoints: [checkpoint],
            VisualCaptures: [],
            Peaks: new TerrainStreamingSmokePeaks(),
            ShutdownDrained: true,
            TerminalStage: TerrainStreamingSmokeStage.ReadyForShutdown,
            LastDrain: drain);

        string json = JsonSerializer.Serialize(
            artifact,
            TerrainStreamingSmokeScenario.ArtifactSerializerOptions);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            ownerCellIds,
            root.GetProperty("terrainCellIds").EnumerateArray()
                .Select(element => element.GetString()!).ToArray());
        JsonElement published = Assert.Single(root.GetProperty("checkpoints").EnumerateArray());
        Assert.Equal(
            ownerCellIds,
            published.GetProperty("terrainCellIds").EnumerateArray()
                .Select(element => element.GetString()!).ToArray());
        Assert.Equal(
            ownerCellIds,
            Assert.Single(published.GetProperty("tiles").EnumerateArray())
                .GetProperty("ownerCellIds").EnumerateArray()
                .Select(element => element.GetString()!).ToArray());
        JsonElement drainCells = root.GetProperty("lastDrain").GetProperty("cells");
        Assert.True(root.GetProperty("lastDrain").GetProperty("isDrained").GetBoolean());
        Assert.Equal(ids.Length, drainCells.GetArrayLength());
        for (int index = 0; index < ids.Length; index++)
        {
            JsonElement cell = drainCells[index];
            Assert.Equal(ids[index].ToString(), cell.GetProperty("cellId").GetString());
            Assert.True(cell.GetProperty("tracked").GetBoolean());
            Assert.NotEqual(JsonValueKind.Null, cell.GetProperty("state").ValueKind);
            Assert.False(cell.GetProperty("desired").GetBoolean());
            Assert.Equal("None", cell.GetProperty("desiredSources").GetString());
            Assert.False(cell.GetProperty("pinned").GetBoolean());
        }
    }

    private static TerrainStreamingDrainSnapshot Drain(
        IReadOnlyList<TerrainStreamingCellDrainSnapshot> cells) =>
        new(cells, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static TerrainStreamingOwnerCells CreateOwnerCells(params WorldCellId[] cells)
    {
        TerrainStreamingOwnerCells? ownerCells = TerrainStreamingOwnerCells.TryCreate(
            CreateWorld(cells),
            [CreateTile(0, cells.Select(cell => Owner(1, cell)).ToArray())],
            out string diagnostic);
        Assert.True(ownerCells != null, diagnostic);
        return ownerCells!;
    }

    private static WorldCellId Cell(string value) => new(Guid.Parse(value));

    private static RuntimeAssetResidencyOwnerId Owner(long generation, WorldCellId cell) =>
        RuntimeAssetResidencyOwnerId.Cell(s_WorldGuid, cell, generation);

    private static IReadOnlyList<RuntimeAssetResidencyOwnerId> Owners(
        params RuntimeAssetResidencyOwnerId[] owners) => owners;

    private static WorldDescriptor CreateWorld(params WorldCellId[] cells) => new(
        s_WorldGuid,
        SourceSchemaVersion: 2,
        Name: "terrain owner-cell test world",
        PersistentScene: new WorldSceneReference(Guid.NewGuid(), PackageId, string.Empty),
        PersistentSceneContentHash: [],
        PersistentScenePayloadBytes: 1024,
        Partition: new WorldPartitionSettings(
            new WorldPosition(0.0, 0.0, 0.0),
            new WorldPosition(256.0, 128.0, 256.0),
            LoadRadius: 1,
            UnloadHysteresis: 1,
            MaxActiveCells: 8),
        Policy: new WorldStreamingPolicy(
            WorldUnresolvedReferencePolicy.KeepUnresolved,
            WorldUnloadedTargetPolicy.ClearAndLateResolve,
            WorldDependencyCyclePolicy.Reject),
        Layers: [new WorldLayerDescriptor("surface", 0)],
        Cells: cells
            .Select(cell => new WorldCellDescriptor(
                cell,
                new WorldCellKey(new WorldCellCoordinate(0, 0, 0), "surface"),
                new WorldSceneReference(Guid.NewGuid(), PackageId, string.Empty),
                new WorldBounds(
                    new WorldPosition(0.0, -64.0, 0.0),
                    new WorldPosition(256.0, 64.0, 256.0)),
                SceneContentHash: [],
                ScenePayloadBytes: 1024,
                EstimatedCpuBytes: 1024,
                EstimatedGpuBytes: 4096,
                Neighbors: [],
                Dependencies: []))
            .ToArray(),
        EntityReferences: []);

    private static TerrainTileDiagnosticSnapshot CreateTile(
        int coordinateX,
        IReadOnlyList<RuntimeAssetResidencyOwnerId> owners)
    {
        var valid = new TerrainNeighborDiagnosticSnapshot(
            Guid.Empty,
            IsResident: false,
            TerrainSeamDiagnosticState.Valid,
            HeightMismatchCount: 0);
        return new TerrainTileDiagnosticSnapshot(
            TerrainRootGuid: s_RootGuid,
            TileGuid: Guid.Parse($"71000000-0000-0000-0000-0000000001{coordinateX:D2}"),
            LayerSetGuid: s_LayerSetGuid,
            PackageId: PackageId,
            Coordinate: new TerrainTileCoordinate(coordinateX, 0),
            CookedVersion: 1,
            SourceSchemaVersion: 1,
            Generation: 7,
            Resolution: 17,
            LayerCount: 1,
            WorldBounds: CreateBounds(coordinateX),
            MinHeight: 0.0,
            MaxHeight: 40.0,
            MaximumGeometricError: 1.0,
            MinimumSelectedLod: 0,
            MaximumSelectedLod: 4,
            CpuHeightBytes: 1024,
            CpuWeightBytes: 1024,
            CpuErrorBytes: 0,
            PreparedGpuBytes: 4096,
            ResidencyState: RuntimePreparedAssetState.Ready,
            IsVisible: true,
            IsDirty: false,
            IsFailed: false,
            Neighbors: new TerrainTileNeighborDiagnostics(valid, valid, valid, valid),
            Owners: owners,
            Patches: [],
            Diagnostic: string.Empty);
    }

    private static TerrainPatchWorldBounds CreateBounds(int coordinateX) => new(
        new WorldPosition(coordinateX * 256.0, 0.0, 0.0),
        new WorldPosition((coordinateX * 256.0) + 256.0, 40.0, 256.0));
}
