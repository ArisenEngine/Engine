using System.Text;
using ArisenEditor.Terrain;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.Automation;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainAuthoringTransactionTests
{
    [Fact]
    public void SourceEncodersRoundTripCanonicalHeightAndWeights()
    {
        ushort[] heights = [0, 1, 32_768, ushort.MaxValue];
        byte[] weights =
        [
            255, 0, 0, 0,
            100, 80, 50, 25,
            0, 255, 0, 0,
            1, 2, 3, 249
        ];

        byte[] encodedHeights = TerrainHeightSourceEncoder.Encode(2, 2, heights);
        byte[] encodedWeights = TerrainWeightSourceEncoder.Encode(2, 2, weights);
        TerrainHeightField decodedHeights = TerrainHeightSourceDecoder.Decode(
            encodedHeights,
            "roundtrip.pgm");
        TerrainWeightField decodedWeights = TerrainWeightSourceDecoder.Decode(
            encodedWeights,
            "roundtrip.ariweights");

        Assert.Equal(heights, decodedHeights.Samples.ToArray());
        Assert.Equal(weights, decodedWeights.Weights.ToArray());
        Assert.Equal(encodedHeights, TerrainHeightSourceEncoder.Encode(
            decodedHeights.Width,
            decodedHeights.Height,
            decodedHeights.Samples.Span));
        Assert.Equal(encodedWeights, TerrainWeightSourceEncoder.Encode(
            decodedWeights.Width,
            decodedWeights.Height,
            decodedWeights.Weights.Span));
    }

    [Fact]
    public void SourceTransactionRollsBackEveryFileWhenCommitFails()
    {
        string root = CreateTemporaryDirectory();
        string assets = Path.Combine(root, "Assets");
        Directory.CreateDirectory(assets);
        string first = Path.Combine(assets, "first.bin");
        string blocked = Path.Combine(assets, "blocked");
        File.WriteAllText(first, "before", Encoding.ASCII);
        Directory.CreateDirectory(blocked);

        try
        {
            Assert.ThrowsAny<Exception>(() => TerrainImportEmitter.ExecuteTransaction(
                assets,
                [
                    new TerrainImportFileWrite(first, "after"u8.ToArray()),
                    new TerrainImportFileWrite(blocked, "cannot-replace-directory"u8.ToArray())
                ],
                Array.Empty<string>()));

            Assert.Equal("before", File.ReadAllText(first, Encoding.ASCII));
            Assert.True(Directory.Exists(blocked));
            Assert.Empty(Directory.EnumerateDirectories(assets, ".terrain-import-*"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void SaveSourcesPersistsHeightAndWeightAndReopensClean()
    {
        using var fixture = TerrainAuthoringFixture.Create();
        TerrainAuthoringDocument document = fixture.OpenDocument();
        ApplyHeight(document, 1.0, 1.0, 1_500);
        ApplyWeight(document, 1.0, 1.0, layerIndex: 1, opacity: 96);
        ushort expectedHeight = document.GetHeightSample(1, 1);
        uint expectedWeights = document.GetPackedWeights(1, 1);

        TerrainAuthoringSourceSaveResult result = document.SaveSources();

        Assert.True(result.Saved);
        Assert.Equal(2, result.WrittenPaths.Count);
        Assert.False(document.IsDirty);
        Assert.NotNull(result.PreviewRevision);
        Assert.False(result.PreviewRevision.IsDirty);
        Assert.Equal(expectedHeight, TerrainHeightSourceDecoder
            .DecodeFile(fixture.HeightAssetPath)
            .GetSample(1, 1));
        Assert.Equal(expectedWeights, Pack(TerrainWeightSourceDecoder
            .DecodeFile(fixture.WeightAssetPath)
            .GetSample(1, 1)));

        TerrainAuthoringDocument reopened = fixture.OpenDocument();
        Assert.False(reopened.IsDirty);
        Assert.Equal(expectedHeight, reopened.GetHeightSample(1, 1));
        Assert.Equal(expectedWeights, reopened.GetPackedWeights(1, 1));
    }

    [Fact]
    public void SaveSourcesBlocksWhenHeightChangedExternally()
    {
        using var fixture = TerrainAuthoringFixture.Create();
        TerrainAuthoringDocument document = fixture.OpenDocument();
        ApplyHeight(document, 1.0, 1.0, 1_000);
        fixture.RewriteHeight(samples => samples[0] += 77);
        byte[] externalBytes = File.ReadAllBytes(fixture.HeightAssetPath);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            document.SaveSources);

        Assert.Contains("Save was blocked", error.Message);
        Assert.Equal(TerrainAuthoringExternalChanges.Height, document.ExternalChanges);
        Assert.True(document.IsDirty);
        Assert.Equal(externalBytes, File.ReadAllBytes(fixture.HeightAssetPath));
    }

    [Fact]
    public void ReloadExternalReplacesWorkingSamplesAndClearsDirtyState()
    {
        using var fixture = TerrainAuthoringFixture.Create();
        TerrainAuthoringDocument document = fixture.OpenDocument();
        ushort expected = fixture.RewriteHeight(samples => samples[0] += 321)[0];

        TerrainAuthoringSourceReimportResult result = document.ReimportSources(
            TerrainAuthoringReimportConflictResolution.ReloadExternal);

        Assert.True(result.Reimported);
        Assert.False(result.HadLocalConflict);
        Assert.Equal(expected, document.GetHeightSample(0, 0));
        Assert.False(document.IsDirty);
        Assert.False(document.HasExternalChanges);
        Assert.NotNull(result.PreviewRevision);
        Assert.False(result.PreviewRevision.IsDirty);
        Assert.Equal(
            [new TerrainTileCoordinate(0, 0)],
            result.ExternallyChangedTiles);
    }

    [Fact]
    public void MergeExternalKeepsLocalSamplesAndAppliesDisjointDiskChanges()
    {
        using var fixture = TerrainAuthoringFixture.Create();
        TerrainAuthoringDocument document = fixture.OpenDocument();
        ApplyHeight(document, 1.0, 1.0, 1_000);
        ushort local = document.GetHeightSample(1, 1);
        ushort external = fixture.RewriteHeight(samples => samples[(3 * 5) + 3] += 555)[(3 * 5) + 3];

        TerrainAuthoringSourceReimportResult result = document.ReimportSources(
            TerrainAuthoringReimportConflictResolution.MergeLocalChanges);

        Assert.True(result.Reimported);
        Assert.True(result.HadLocalConflict);
        Assert.Equal(local, document.GetHeightSample(1, 1));
        Assert.Equal(external, document.GetHeightSample(3, 3));
        Assert.True(document.IsDirty);
        Assert.NotNull(result.PreviewRevision);
        Assert.True(result.PreviewRevision.IsDirty);
        Assert.Equal(2, result.PreviewRevision.ChangedTiles.Count);
        Assert.Equal(
            [new TerrainTileCoordinate(1, 1)],
            result.ExternallyChangedTiles);
    }

    [Fact]
    public void MergeMatchingExternalSamplePublishesCleanRevision()
    {
        using var fixture = TerrainAuthoringFixture.Create();
        TerrainAuthoringDocument document = fixture.OpenDocument();
        ApplyHeight(document, 1.0, 1.0, 1_000);
        ushort local = document.GetHeightSample(1, 1);
        fixture.RewriteHeight(samples => samples[(1 * 5) + 1] = local);

        TerrainAuthoringSourceReimportResult result = document.ReimportSources(
            TerrainAuthoringReimportConflictResolution.MergeLocalChanges);

        Assert.True(result.HadLocalConflict);
        Assert.False(document.IsDirty);
        Assert.NotNull(result.PreviewRevision);
        Assert.False(result.PreviewRevision.IsDirty);
        Assert.Single(result.PreviewRevision.ChangedTiles);
    }

    [Fact]
    public void IncrementalCookReusesValidTilesAndRecooksMissingDependency()
    {
        using var fixture = TerrainAuthoringFixture.Create();
        TerrainRootSourceDescriptor root = fixture.LoadRoot();
        TerrainGeneratedTileRecord retained = root.GeneratedTiles.Single(
            tile => tile.Coordinate == new TerrainTileCoordinate(1, 1));
        TerrainGeneratedTileRecord missing = root.GeneratedTiles.Single(
            tile => tile.Coordinate == new TerrainTileCoordinate(1, 0));
        Assert.True(fixture.Database.TryGetCookedArtifact(
            retained.Guid,
            TerrainTileAssetCooker.RuntimeVariant,
            out CookedAssetRecord retainedArtifact));
        Assert.True(fixture.Database.TryGetCookedArtifact(
            missing.Guid,
            TerrainTileAssetCooker.RuntimeVariant,
            out CookedAssetRecord missingArtifact));
        DateTime sentinel = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(retainedArtifact.Path, sentinel);
        File.Delete(missingArtifact.Path);

        TerrainAuthoringDocument document = fixture.OpenDocument();
        ApplyHeight(document, 1.0, 1.0, 1_000);
        TerrainAuthoringSourceSaveResult save = document.SaveSources();
        TerrainIncrementalCookResult cook = TerrainRootAssetCooker.CookChangedTiles(
            fixture.Database,
            fixture.RootReference,
            save.ChangedTiles);

        Assert.Equal(
            [new TerrainTileCoordinate(0, 0)],
            cook.RequestedTiles);
        Assert.Contains(new TerrainTileCoordinate(1, 0), cook.DependencyRecookedTiles);
        Assert.Contains(new TerrainTileCoordinate(1, 1), cook.ReusedTiles);
        Assert.Equal(sentinel, File.GetLastWriteTimeUtc(retainedArtifact.Path));
        Assert.False(File.Exists(missingArtifact.Path));
        Assert.True(fixture.Database.TryGetCookedArtifact(
            missing.Guid,
            TerrainTileAssetCooker.RuntimeVariant,
            out CookedAssetRecord recookedMissingArtifact));
        Assert.NotEqual(missingArtifact.Path, recookedMissingArtifact.Path);
        Assert.True(File.Exists(recookedMissingArtifact.Path));
    }

    [Fact]
    public void ImportAdoptsLegacyCanonicalHeightAndWeightMetadata()
    {
        using var fixture = TerrainAuthoringFixture.Create();
        fixture.WriteText(
            fixture.HeightAssetPath + ".meta",
            $$"""
            Guid: {{Guid.Parse("a1000000-0000-0000-0000-000000000001"):D}}
            AssetType: TerrainHeightSource
            Importer: {{TerrainImportPlanner.HeightImporter}}
            """);
        fixture.WriteText(
            fixture.WeightAssetPath + ".meta",
            $$"""
            Guid: {{Guid.Parse("a1000000-0000-0000-0000-000000000002"):D}}
            AssetType: TerrainWeightSource
            Importer: {{TerrainImportPlanner.WeightImporter}}
            """);

        TerrainImportPlan plan = TerrainImportPlanner.CreatePlan(
            fixture.Request,
            fixture.LayerSetAsset);
        TerrainImportEmitter.Commit(plan);

        Assert.Contains("Generated:", File.ReadAllText(fixture.HeightAssetPath + ".meta"));
        Assert.Contains("Generated:", File.ReadAllText(fixture.WeightAssetPath + ".meta"));
        Assert.NotNull(fixture.LoadRoot().WeightSource);
    }

    [Fact]
    public void EditorControllerSavesCooksAndReportsExternalSourceChanges()
    {
        using var fixture = TerrainAuthoringFixture.Create();
        var previews = new TerrainAuthoringPreviewService();
        var commands = new CommandManager();
        using var controller = new TerrainEditorAuthoringController(
            fixture.Database,
            commands,
            previews);
        var changes = new List<AssetChangeEvent>();
        fixture.Database.AssetChanged += changes.Add;

        Assert.True(controller.Apply(fixture.RootGuid, 1.0, 1.0));
        Assert.True(controller.Save(fixture.RootGuid));
        Assert.True(controller.TryGetDocumentState(
            fixture.RootGuid,
            out TerrainAuthoringDocumentState saved));
        Assert.False(saved.IsDirty);
        Assert.False(saved.HasPendingCook);
        Assert.Contains(changes, change =>
            change.Guid == fixture.RootGuid && change.AssetType == TerrainAssetTypes.Root);
        Assert.Contains(changes, change => change.AssetType == TerrainAssetTypes.Tile);
        Assert.True(previews.TryGetLatest(
            fixture.RootGuid,
            out TerrainAuthoringPreviewRevision clean));
        Assert.False(clean.IsDirty);

        fixture.RewriteHeight(samples => samples[^1] += 99);
        AssetRecord heightAsset = fixture.Database.Assets.Single(asset =>
            PathsEqual(asset.SourcePath, fixture.HeightAssetPath));
        fixture.Database.NotifyAssetChanged(new AssetChangeEvent(
            AssetChangeKind.Changed,
            heightAsset.Guid,
            heightAsset.AssetType,
            heightAsset.SourcePath,
            string.Empty,
            heightAsset.PackageId));

        Assert.True(controller.TryGetDocumentState(
            fixture.RootGuid,
            out TerrainAuthoringDocumentState conflicted));
        Assert.Equal(TerrainAuthoringExternalChanges.Height, conflicted.ExternalChanges);
        Assert.Contains("Choose Reload External or Merge Local", controller.LastDiagnostic);
        Assert.True(controller.Reimport(
            fixture.RootGuid,
            TerrainAuthoringReimportConflictResolution.ReloadExternal));
        Assert.True(controller.TryGetDocumentState(
            fixture.RootGuid,
            out TerrainAuthoringDocumentState reimported));
        Assert.Equal(TerrainAuthoringExternalChanges.None, reimported.ExternalChanges);
        Assert.False(reimported.HasPendingCook);
    }

    private static TerrainAuthoringPreviewRevision ApplyHeight(
        TerrainAuthoringDocument document,
        double x,
        double z,
        int delta)
    {
        TerrainBrushEdit edit = document.CreateHeightBrushEdit(
            new TerrainHeightBrush(x, z, 0.25, delta));
        Assert.True(edit.HasChanges);
        return document.ApplyEdit(edit, true);
    }

    private static TerrainAuthoringPreviewRevision ApplyWeight(
        TerrainAuthoringDocument document,
        double x,
        double z,
        int layerIndex,
        byte opacity)
    {
        TerrainBrushEdit edit = document.CreateWeightBrushEdit(
            new TerrainWeightBrush(x, z, 0.25, layerIndex, opacity));
        Assert.True(edit.HasChanges);
        return document.ApplyEdit(edit, true);
    }

    private static uint Pack(ReadOnlySpan<byte> weights) =>
        (uint)(weights[0] |
               (weights[1] << 8) |
               (weights[2] << 16) |
               (weights[3] << 24));

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "ArisenTerrainAuthoringTransactionTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class TerrainAuthoringFixture : IDisposable
    {
        private const string PackageId = "com.arisen.tests.terrain-transactions";
        private static readonly Guid s_RootGuid =
            Guid.Parse("a2000000-0000-0000-0000-000000000001");
        private static readonly Guid s_LayerSetGuid =
            Guid.Parse("a2000000-0000-0000-0000-000000000002");

        private TerrainAuthoringFixture(string root)
        {
            Root = root;
            PackageRoot = Path.Combine(root, "Package");
            AssetsRoot = Path.Combine(PackageRoot, "Assets");
            Directory.CreateDirectory(AssetsRoot);
            LayerSetAsset = CreateLayerSet();
            string input = Path.Combine(root, "Inputs", "Valley.pgm");
            WriteHeight(input, CreateInitialHeights());
            Request = new TerrainImportRequest(
                input,
                AssetsRoot,
                PackageId,
                "Terrain/Imported",
                "Valley",
                "Transaction Valley",
                s_RootGuid,
                new WorldBounds(
                    new WorldPosition(0.0, 0.0, 0.0),
                    new WorldPosition(4.0, 10.0, 4.0)),
                TileResolution: 3,
                new TerrainTileCoordinate(0, 0),
                new AssetRef<TerrainLayerSetSourceAsset>(
                    s_LayerSetGuid,
                    TerrainAssetTypes.LayerSet,
                    PackageId));
            TerrainImportPlan plan = TerrainImportPlanner.CreatePlan(Request, LayerSetAsset);
            TerrainImportCommitResult commit = TerrainImportEmitter.Commit(plan);
            HeightAssetPath = plan.HeightAssetPath;
            WeightAssetPath = plan.WeightAssetPath;
            RootAssetPath = commit.RootAssetPath;
            Database = new AssetDatabase();
            Database.InitializeWorkspace(
                Root,
                [(PackageId, PackageRoot)],
                AssetSourceAccessMode.EditorAuthoring);
            RootReference = new AssetRef<TerrainRootSourceAsset>(
                s_RootGuid,
                TerrainAssetTypes.Root,
                PackageId);
            TerrainRootAssetCooker.Cook(Database, RootReference);
        }

        public string Root { get; }
        public string PackageRoot { get; }
        public string AssetsRoot { get; }
        public string HeightAssetPath { get; }
        public string WeightAssetPath { get; }
        public string RootAssetPath { get; }
        public Guid RootGuid => s_RootGuid;
        public AssetRecord LayerSetAsset { get; }
        public TerrainImportRequest Request { get; }
        public AssetDatabase Database { get; }
        public AssetRef<TerrainRootSourceAsset> RootReference { get; }

        public static TerrainAuthoringFixture Create() =>
            new(CreateTemporaryDirectory());

        public TerrainAuthoringDocument OpenDocument() =>
            TerrainAuthoringDocument.Load(Database, RootReference);

        public TerrainRootSourceDescriptor LoadRoot() =>
            TerrainRootSourceAssetLoader.LoadSource(Database, RootReference);

        public ushort[] RewriteHeight(Action<ushort[]> edit)
        {
            ushort[] samples = TerrainHeightSourceDecoder
                .DecodeFile(HeightAssetPath)
                .Samples
                .ToArray();
            edit(samples);
            WriteHeight(HeightAssetPath, samples);
            return samples;
        }

        public void WriteText(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            Database.ReleaseAllLoadedCookedAssets();
            DeleteDirectory(Root);
        }

        private AssetRecord CreateLayerSet()
        {
            string sourcePath = Path.Combine(
                AssetsRoot,
                "Terrain",
                "TransactionLayers.ariterrainlayers");
            WriteText(
                sourcePath,
                $$"""
                Version: 2
                LayerSetGuid: {{s_LayerSetGuid:D}}
                Name: Transaction Layers
                Layers:
                - Id: Ground
                  Albedo: { Guid: a3000000-0000-0000-0000-000000000001, PackageId: {{PackageId}} }
                  Normal: { Guid: a3000000-0000-0000-0000-000000000002, PackageId: {{PackageId}} }
                  Orm: { Guid: a3000000-0000-0000-0000-000000000003, PackageId: {{PackageId}} }
                - Id: Rock
                  Albedo: { Guid: a3000000-0000-0000-0000-000000000004, PackageId: {{PackageId}} }
                  Normal: { Guid: a3000000-0000-0000-0000-000000000005, PackageId: {{PackageId}} }
                  Orm: { Guid: a3000000-0000-0000-0000-000000000006, PackageId: {{PackageId}} }
                """);
            WriteText(
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

        private static ushort[] CreateInitialHeights()
        {
            var samples = new ushort[25];
            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] = checked((ushort)(10_000 + (index * 500)));
            }
            return samples;
        }

        private static void WriteHeight(string path, ushort[] samples)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, TerrainHeightSourceEncoder.Encode(5, 5, samples));
        }
    }
}
