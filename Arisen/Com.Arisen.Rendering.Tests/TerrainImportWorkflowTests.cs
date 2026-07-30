using System.Buffers.Binary;
using System.Text;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.Serialization;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainImportWorkflowTests
{
    private const string PackageId = "com.arisen.terrain-import-tests";
    private static readonly Guid s_RootGuid =
        Guid.Parse("a1111111-2222-3333-4444-555555555555");
    private static readonly Guid s_LayerSetGuid =
        Guid.Parse("b2222222-3333-4444-5555-666666666666");

    [Fact]
    public void Preview_IsReadOnlyDeterministicAndMapsTilesToWorldCells()
    {
        using var fixture = TerrainImportFixture.Create();
        AssetRecord layerSet = fixture.CreateLayerSet();
        string heightPath = fixture.WritePgm("Inputs/Valley.pgm", 5, 5, seed: 7);
        TerrainImportRequest request = fixture.CreateRequest(heightPath);
        var partition = new WorldPartitionSettings(
            new WorldPosition(0.0, 0.0, 0.0),
            new WorldPosition(4.0, 100.0, 4.0),
            LoadRadius: 1,
            UnloadHysteresis: 1,
            MaxActiveCells: 16);

        TerrainImportPlan first = TerrainImportPlanner.CreatePlan(request, layerSet, partition);
        TerrainImportPlan second = TerrainImportPlanner.CreatePlan(request, layerSet, partition);

        Assert.False(first.ReplacesExistingRoot);
        Assert.False(first.RequiresRegenerationConfirmation);
        Assert.Equal(s_RootGuid, first.RootGuid);
        Assert.Equal(5, first.SourceWidth);
        Assert.Equal(5, first.SourceHeight);
        Assert.Equal(new TerrainSampleSpacing(2.0, 2.0), first.SampleSpacing);
        Assert.Equal(4, first.Tiles.Count);
        Assert.Equal(
            [
                new TerrainTileCoordinate(0, 0),
                new TerrainTileCoordinate(1, 0),
                new TerrainTileCoordinate(0, 1),
                new TerrainTileCoordinate(1, 1)
            ],
            first.Tiles.Select(tile => tile.Coordinate));
        Assert.Equal(
            first.Tiles.Select(tile => tile.Guid),
            second.Tiles.Select(tile => tile.Guid));
        Assert.Equal(
            [
                new WorldCellCoordinate(0, 0, 0),
                new WorldCellCoordinate(1, 0, 0),
                new WorldCellCoordinate(0, 0, 1),
                new WorldCellCoordinate(1, 0, 1)
            ],
            first.Tiles.Select(tile => tile.OwnerCell!.Value));
        Assert.All(first.Tiles, tile => Assert.Single(tile.IntersectingCells));
        Assert.False(File.Exists(first.RootAssetPath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(first.RootAssetPath)));
    }

    [Fact]
    public void Commit_WritesLoadableRootAndStableGeneratedTileIdentities()
    {
        using var fixture = TerrainImportFixture.Create();
        AssetRecord layerSet = fixture.CreateLayerSet();
        string heightPath = fixture.WritePgm("Inputs/Valley.pgm", 5, 5, seed: 11);
        TerrainImportRequest request = fixture.CreateRequest(heightPath);
        TerrainImportPlan plan = TerrainImportPlanner.CreatePlan(request, layerSet);

        TerrainImportCommitResult result = TerrainImportEmitter.Commit(plan);

        Assert.Equal(s_RootGuid, result.RootGuid);
        Assert.Equal(4, result.TileGuids.Count);
        Assert.Empty(result.RemovedTileGuids);
        Assert.True(File.Exists(result.RootAssetPath));
        Assert.True(File.Exists(result.RootAssetPath + ".meta"));
        Assert.True(File.Exists(plan.HeightAssetPath));
        Assert.Equal(File.ReadAllBytes(heightPath), File.ReadAllBytes(plan.HeightAssetPath));
        var rootRecord = new AssetRecord(
            result.RootGuid,
            TerrainAssetTypes.Root,
            result.RootAssetPath,
            result.RootAssetPath + ".meta",
            PackageId);
        TerrainRootSourceDescriptor loaded = TerrainRootSourceAssetLoader.LoadSource(rootRecord);
        Assert.Equal(result.TileGuids, loaded.GeneratedTiles.Select(tile => tile.Guid));
        Assert.Equal(new WorldPosition(0.0, 0.0, 0.0), loaded.WorldPlacement);
        Assert.Equal(new TerrainHeightRange(0.0, 2.0), loaded.HeightRange);

        foreach (TerrainTileImportPreview tile in plan.Tiles)
        {
            Assert.True(File.Exists(tile.GeneratedAssetPath));
            AssetMetadata metadata = SerializationUtil.Deserialize<AssetMetadata>(
                tile.GeneratedAssetPath + ".meta",
                serializeIfNotExist: false);
            Assert.Equal(tile.Guid, metadata.Guid);
            Assert.NotNull(metadata.Generated);
            Assert.Equal(s_RootGuid, metadata.Generated.SourceGuid);
            Assert.Equal(
                TerrainTileIdentity.CreateChildKey(tile.Coordinate),
                metadata.Generated.ChildKey);
        }

        TerrainImportPlan unchanged = TerrainImportPlanner.CreatePlan(request, layerSet);
        Assert.True(unchanged.ReplacesExistingRoot);
        Assert.False(unchanged.RequiresRegenerationConfirmation);
        Assert.Equal(result.TileGuids, unchanged.PreviousTileGuids);
        Assert.Equal(
            result.TileGuids,
            unchanged.Tiles.Select(tile => tile.Guid));
    }

    [Fact]
    public void Commit_AdoptsCanonicalDestinationHeightSourceWithGeneratedIdentity()
    {
        using var fixture = TerrainImportFixture.Create();
        AssetRecord layerSet = fixture.CreateLayerSet();
        string destinationHeightPath = fixture.WritePgm(
            "Package/Assets/Terrain/Imported/Height/Valley.pgm",
            5,
            5,
            seed: 12);
        byte[] sourceBytes = File.ReadAllBytes(destinationHeightPath);
        TerrainImportPlan plan = TerrainImportPlanner.CreatePlan(
            fixture.CreateRequest(destinationHeightPath),
            layerSet);

        TerrainImportEmitter.Commit(plan);

        Assert.Equal(sourceBytes, File.ReadAllBytes(destinationHeightPath));
        AssetMetadata metadata = SerializationUtil.Deserialize<AssetMetadata>(
            destinationHeightPath + ".meta",
            serializeIfNotExist: false);
        Assert.NotNull(metadata.Generated);
        Assert.Equal(s_RootGuid, metadata.Generated.SourceGuid);
        Assert.Equal(TerrainImportPlanner.HeightChildKind, metadata.Generated.ChildKind);
        Assert.Equal(TerrainImportPlanner.HeightImporter, metadata.Importer);

        fixture.WriteTextAt(
            destinationHeightPath + ".meta",
            """
            Guid: c3333333-4444-5555-6666-777777777777
            AssetType: TerrainHeightSource
            Importer: Pgm16TerrainHeightImporter
            Generated:
              SourceGuid: d4444444-5555-6666-7777-888888888888
              SourcePackageId: com.arisen.foreign
              ChildKind: terrain-height-source
              ChildKey: height
              GeneratedByImporter: Pgm16TerrainHeightImporter
            """);
        InvalidOperationException foreign = Assert.Throws<InvalidOperationException>(() =>
            TerrainImportPlanner.CreatePlan(
                fixture.CreateRequest(destinationHeightPath),
                layerSet));
        Assert.Contains("foreign height output", foreign.Message);
    }

    [Fact]
    public void Commit_RequiresConfirmationBeforeDestructiveGridRegeneration()
    {
        using var fixture = TerrainImportFixture.Create();
        AssetRecord layerSet = fixture.CreateLayerSet();
        string heightPath = fixture.WritePgm("Inputs/Valley.pgm", 5, 5, seed: 13);
        TerrainImportRequest initialRequest = fixture.CreateRequest(heightPath);
        TerrainImportPlan initialPlan = TerrainImportPlanner.CreatePlan(initialRequest, layerSet);
        TerrainImportCommitResult initial = TerrainImportEmitter.Commit(initialPlan);
        byte[] sourceBeforeRejectedCommit = File.ReadAllBytes(initial.RootAssetPath);

        TerrainImportRequest reducedRequest = initialRequest with { TileResolution = 5 };
        TerrainImportPlan reducedPlan = TerrainImportPlanner.CreatePlan(reducedRequest, layerSet);

        Assert.Equal(
            TerrainImportDestructiveChange.TileGrid,
            reducedPlan.DestructiveChanges);
        InvalidOperationException rejection = Assert.Throws<InvalidOperationException>(() =>
            TerrainImportEmitter.Commit(reducedPlan));
        Assert.Contains("explicit regeneration confirmation", rejection.Message);
        Assert.Equal(sourceBeforeRejectedCommit, File.ReadAllBytes(initial.RootAssetPath));
        Assert.All(initialPlan.Tiles, tile => Assert.True(File.Exists(tile.GeneratedAssetPath)));

        TerrainImportCommitResult reduced = TerrainImportEmitter.Commit(
            reducedPlan,
            new TerrainImportCommitOptions(ConfirmDestructiveRegeneration: true));

        Assert.Single(reduced.TileGuids);
        Assert.Equal(3, reduced.RemovedTileGuids.Count);
        Assert.Equal(initial.TileGuids[0], reduced.TileGuids[0]);
        Assert.True(File.Exists(initialPlan.Tiles[0].GeneratedAssetPath));
        Assert.All(
            initialPlan.Tiles.Skip(1),
            tile => Assert.False(File.Exists(tile.GeneratedAssetPath)));
        TerrainRootSourceDescriptor loaded = TerrainRootSourceAssetLoader.LoadSource(
            new AssetRecord(
                reduced.RootGuid,
                TerrainAssetTypes.Root,
                reduced.RootAssetPath,
                reduced.RootAssetPath + ".meta",
                PackageId));
        Assert.Single(loaded.GeneratedTiles);
        Assert.Equal(5, loaded.TileResolution);
    }

    [Fact]
    public void Commit_RejectsStalePreviewWithoutWriting()
    {
        using var fixture = TerrainImportFixture.Create();
        AssetRecord layerSet = fixture.CreateLayerSet();
        string heightPath = fixture.WritePgm("Inputs/Valley.pgm", 5, 5, seed: 17);
        TerrainImportRequest request = fixture.CreateRequest(heightPath);
        TerrainImportPlan plan = TerrainImportPlanner.CreatePlan(request, layerSet);
        fixture.WritePgm("Inputs/Valley.pgm", 5, 5, seed: 19);

        InvalidOperationException rejection = Assert.Throws<InvalidOperationException>(() =>
            TerrainImportEmitter.Commit(plan));

        Assert.Contains("preview is stale", rejection.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(plan.RootAssetPath));
    }

    [Fact]
    public void Commit_MigratesOwnedFlatGeneratedOutputsWithoutChangingIdentity()
    {
        using var fixture = TerrainImportFixture.Create();
        AssetRecord layerSet = fixture.CreateLayerSet();
        string heightPath = fixture.WritePgm("Inputs/Valley.pgm", 5, 5, seed: 21);
        TerrainImportRequest request = fixture.CreateRequest(heightPath);
        TerrainImportPlan initialPlan = TerrainImportPlanner.CreatePlan(request, layerSet);
        TerrainImportEmitter.Commit(initialPlan);
        TerrainTileImportPreview tile = initialPlan.Tiles[0];
        string generatedRoot = Directory.GetParent(Path.GetDirectoryName(tile.GeneratedAssetPath)!)!.FullName;
        string flatPath = Path.Combine(generatedRoot, "Valley_0_0.ariterraingenerated");
        File.Move(tile.GeneratedAssetPath, flatPath);
        File.Move(tile.GeneratedAssetPath + ".meta", flatPath + ".meta");

        TerrainImportPlan migration = TerrainImportPlanner.CreatePlan(request, layerSet);
        TerrainImportCommitResult result = TerrainImportEmitter.Commit(migration);

        Assert.Empty(result.RemovedTileGuids);
        Assert.False(File.Exists(flatPath));
        Assert.False(File.Exists(flatPath + ".meta"));
        Assert.True(File.Exists(tile.GeneratedAssetPath));
        AssetMetadata metadata = SerializationUtil.Deserialize<AssetMetadata>(
            tile.GeneratedAssetPath + ".meta",
            serializeIfNotExist: false);
        Assert.Equal(tile.Guid, metadata.Guid);
    }

    [Fact]
    public void Commit_RestoresExistingTerrainWhenFileInstallationFails()
    {
        using var fixture = TerrainImportFixture.Create();
        AssetRecord layerSet = fixture.CreateLayerSet();
        string heightPath = fixture.WritePgm("Inputs/Valley.pgm", 5, 5, seed: 22);
        TerrainImportRequest expandedRequest = fixture.CreateRequest(heightPath);
        TerrainImportRequest initialRequest = expandedRequest with { TileResolution = 5 };
        TerrainImportCommitResult initial = TerrainImportEmitter.Commit(
            TerrainImportPlanner.CreatePlan(initialRequest, layerSet));
        Dictionary<string, byte[]> initialFiles = initial.WrittenPaths.ToDictionary(
            path => path,
            File.ReadAllBytes,
            StringComparer.OrdinalIgnoreCase);

        TerrainImportPlan pathProbe = TerrainImportPlanner.CreatePlan(expandedRequest, layerSet);
        string blockedTilePath = pathProbe.Tiles[^1].GeneratedAssetPath;
        Directory.CreateDirectory(blockedTilePath);
        TerrainImportPlan expandedPlan = TerrainImportPlanner.CreatePlan(expandedRequest, layerSet);

        Assert.Throws<IOException>(() => TerrainImportEmitter.Commit(
            expandedPlan,
            new TerrainImportCommitOptions(ConfirmDestructiveRegeneration: true)));

        foreach ((string path, byte[] bytes) in initialFiles)
        {
            Assert.True(File.Exists(path), $"Expected rollback to restore '{path}'.");
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }

        Assert.True(Directory.Exists(blockedTilePath));
        Assert.False(File.Exists(blockedTilePath + ".meta"));
        Assert.Empty(Directory.GetDirectories(
            fixture.AssetsRoot,
            ".terrain-import-*",
            SearchOption.TopDirectoryOnly));
        TerrainRootSourceDescriptor restored = TerrainRootSourceAssetLoader.LoadSource(
            new AssetRecord(
                initial.RootGuid,
                TerrainAssetTypes.Root,
                initial.RootAssetPath,
                initial.RootAssetPath + ".meta",
                PackageId));
        Assert.Equal(5, restored.TileResolution);
        Assert.Single(restored.GeneratedTiles);
    }

    [Fact]
    public void Preview_RejectsForeignGeneratedOutputAndEscapingPaths()
    {
        using var fixture = TerrainImportFixture.Create();
        AssetRecord layerSet = fixture.CreateLayerSet();
        string heightPath = fixture.WritePgm("Inputs/Valley.pgm", 5, 5, seed: 23);
        TerrainImportRequest request = fixture.CreateRequest(heightPath);
        string foreignSource = fixture.PathFor(
            "Package/Assets/Terrain/Imported/Generated/Valley/x_0_z_0.ariterraingenerated");
        fixture.WriteTextAt(foreignSource, "foreign terrain output\n");
        fixture.WriteTextAt(
            foreignSource + ".meta",
            """
            Guid: c3333333-4444-5555-6666-777777777777
            AssetType: TerrainTile
            Importer: ArisenTerrainTileImporter
            Generated:
              SourceGuid: d4444444-5555-6666-7777-888888888888
              SourcePackageId: com.arisen.foreign
              ChildKind: terrain-tile
              ChildKey: x=0;z=0
              GeneratedByImporter: ArisenTerrainTileImporter
            """);

        InvalidOperationException foreign = Assert.Throws<InvalidOperationException>(() =>
            TerrainImportPlanner.CreatePlan(request, layerSet));
        Assert.Contains("not owned by this terrain root", foreign.Message);

        TerrainImportRequest escaping = request with { OutputDirectory = "../Outside" };
        InvalidOperationException outside = Assert.Throws<InvalidOperationException>(() =>
            TerrainImportPlanner.CreatePlan(escaping, layerSet));
        Assert.Contains("must stay below", outside.Message);
    }

    [Fact]
    public void Preview_ClassifiesWorldAndIdentityChangesAsDestructive()
    {
        using var fixture = TerrainImportFixture.Create();
        AssetRecord layerSet = fixture.CreateLayerSet();
        string heightPath = fixture.WritePgm("Inputs/Valley.pgm", 5, 5, seed: 29);
        TerrainImportRequest request = fixture.CreateRequest(heightPath);
        TerrainImportEmitter.Commit(TerrainImportPlanner.CreatePlan(request, layerSet));

        TerrainImportRequest changed = request with
        {
            NewRootGuid = Guid.Parse("e5555555-6666-7777-8888-999999999999"),
            RegenerateRootIdentity = true,
            WorldBounds = new WorldBounds(
                new WorldPosition(32.0, -4.0, 64.0),
                new WorldPosition(48.0, 8.0, 80.0))
        };
        TerrainImportPlan plan = TerrainImportPlanner.CreatePlan(changed, layerSet);

        Assert.Equal(
            TerrainImportDestructiveChange.RootIdentity |
            TerrainImportDestructiveChange.WorldLayout,
            plan.DestructiveChanges);
        Assert.True(plan.RequiresRegenerationConfirmation);
        Assert.NotEqual(request.NewRootGuid, plan.RootGuid);
        Assert.All(
            plan.Tiles,
            tile => Assert.NotEqual(
                TerrainTileIdentity.CreateGuid(
                    request.NewRootGuid,
                    PackageId,
                    tile.Coordinate),
                tile.Guid));
    }

    [Fact]
    public void SourceIndex_RefreshesCreatedAndRemovedTerrainIdentitiesWithoutRestart()
    {
        using var fixture = TerrainImportFixture.Create();
        AssetRecord layerSet = fixture.CreateLayerSet();
        string heightPath = fixture.WritePgm("Inputs/Valley.pgm", 5, 5, seed: 31);
        TerrainImportRequest request = fixture.CreateRequest(heightPath);
        var database = new AssetDatabase();
        database.InitializeWorkspace(
            fixture.Root,
            [(PackageId, fixture.PackageRoot)],
            AssetSourceAccessMode.EditorAuthoring);

        TerrainImportPlan initialPlan = TerrainImportPlanner.CreatePlan(request, layerSet);
        TerrainImportCommitResult initial = TerrainImportEmitter.Commit(initialPlan);
        ((IAssetSourceIndex)database).RefreshSourceDirectory(
            Path.GetDirectoryName(initial.RootAssetPath)!,
            PackageId);

        Assert.True(database.TryGetAsset(initial.RootGuid, out AssetRecord indexedRoot));
        Assert.Equal(initial.RootAssetPath, indexedRoot.SourcePath);
        Assert.All(initial.TileGuids, guid => Assert.True(database.TryGetAsset(guid, out _)));

        TerrainImportPlan reducedPlan = TerrainImportPlanner.CreatePlan(
            request with { TileResolution = 5 },
            layerSet);
        TerrainImportCommitResult reduced = TerrainImportEmitter.Commit(
            reducedPlan,
            new TerrainImportCommitOptions(ConfirmDestructiveRegeneration: true));
        ((IAssetSourceIndex)database).RefreshSourceDirectory(
            Path.GetDirectoryName(reduced.RootAssetPath)!,
            PackageId);

        Assert.True(database.TryGetAsset(reduced.TileGuids[0], out _));
        Assert.All(reduced.RemovedTileGuids, guid => Assert.False(database.TryGetAsset(guid, out _)));

        Guid regeneratedRootGuid = Guid.Parse("f6666666-7777-8888-9999-aaaaaaaaaaaa");
        TerrainImportPlan regeneratedPlan = TerrainImportPlanner.CreatePlan(
            request with
            {
                TileResolution = 5,
                NewRootGuid = regeneratedRootGuid,
                RegenerateRootIdentity = true
            },
            layerSet);
        TerrainImportCommitResult regenerated = TerrainImportEmitter.Commit(
            regeneratedPlan,
            new TerrainImportCommitOptions(ConfirmDestructiveRegeneration: true));
        ((IAssetSourceIndex)database).RefreshSourceDirectory(
            Path.GetDirectoryName(regenerated.RootAssetPath)!,
            PackageId);

        Assert.Equal(reduced.RootGuid, regenerated.ReplacedRootGuid);
        Assert.False(database.TryGetAsset(reduced.RootGuid, out _));
        Assert.True(database.TryGetAsset(regenerated.RootGuid, out _));
        Assert.All(reduced.TileGuids, guid => Assert.False(database.TryGetAsset(guid, out _)));
        Assert.All(regenerated.TileGuids, guid => Assert.True(database.TryGetAsset(guid, out _)));
    }

    private sealed class TerrainImportFixture : IDisposable
    {
        private TerrainImportFixture(string root)
        {
            Root = root;
            AssetsRoot = PathFor("Package/Assets");
            Directory.CreateDirectory(AssetsRoot);
        }

        public string Root { get; }
        public string AssetsRoot { get; }
        public string PackageRoot => Path.GetDirectoryName(AssetsRoot)!;

        public static TerrainImportFixture Create()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "ArisenTerrainImportWorkflowTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TerrainImportFixture(root);
        }

        public AssetRecord CreateLayerSet()
        {
            string sourcePath = PathFor("Package/Assets/Terrain/Valley.ariterrainlayers");
            WriteTextAt(
                sourcePath,
                $$"""
                Version: 2
                LayerSetGuid: {{s_LayerSetGuid:D}}
                Name: Import Test Layers
                Layers:
                - Id: Ground
                  Albedo: { Guid: f1111111-1111-1111-1111-111111111111, PackageId: com.arisen.textures }
                  Normal: { Guid: f2222222-2222-2222-2222-222222222222, PackageId: com.arisen.textures }
                  Orm: { Guid: f3333333-3333-3333-3333-333333333333, PackageId: com.arisen.textures }
                """);
            WriteTextAt(
                sourcePath + ".meta",
                $$"""
                Guid: {{s_LayerSetGuid:D}}
                AssetType: TerrainLayerSet
                Importer: ArisenTerrainLayerSetImporter
                """);
            return new AssetRecord(
                s_LayerSetGuid,
                TerrainAssetTypes.LayerSet,
                sourcePath,
                sourcePath + ".meta",
                PackageId);
        }

        public TerrainImportRequest CreateRequest(string heightPath)
        {
            return new TerrainImportRequest(
                heightPath,
                AssetsRoot,
                PackageId,
                "Terrain/Imported",
                "Valley",
                "Valley Terrain",
                s_RootGuid,
                new WorldBounds(
                    new WorldPosition(0.0, 0.0, 0.0),
                    new WorldPosition(8.0, 2.0, 8.0)),
                TileResolution: 3,
                new TerrainTileCoordinate(0, 0),
                new AssetRef<TerrainLayerSetSourceAsset>(
                    s_LayerSetGuid,
                    TerrainAssetTypes.LayerSet,
                    PackageId));
        }

        public string WritePgm(
            string relativePath,
            int width,
            int height,
            int seed)
        {
            string path = PathFor(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            byte[] header = Encoding.ASCII.GetBytes($"P5\n{width} {height}\n65535\n");
            byte[] bytes = new byte[checked(header.Length + (width * height * sizeof(ushort)))];
            header.CopyTo(bytes, 0);
            for (int index = 0; index < width * height; index++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(
                    bytes.AsSpan(header.Length + (index * sizeof(ushort)), sizeof(ushort)),
                    (ushort)((index * 997 + seed) & ushort.MaxValue));
            }

            File.WriteAllBytes(path, bytes);
            return path;
        }

        public string PathFor(string relativePath)
        {
            return Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public void WriteTextAt(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
