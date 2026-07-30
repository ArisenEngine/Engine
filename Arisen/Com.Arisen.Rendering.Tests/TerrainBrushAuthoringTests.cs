using ArisenEngine.Core.Assets;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainBrushAuthoringTests
{
    [Fact]
    public void HeightBrushProducesDeterministicQuantizedResults()
    {
        TerrainAuthoringDocument first = CreateDocument();
        TerrainAuthoringDocument second = CreateDocument();
        var brush = new TerrainHeightBrush(2.0, 2.0, 1.75, 3_000);

        TerrainBrushEdit firstEdit = first.CreateHeightBrushEdit(brush);
        TerrainBrushEdit secondEdit = second.CreateHeightBrushEdit(brush);
        TerrainAuthoringPreviewRevision firstRevision = first.ApplyEdit(firstEdit, true);
        TerrainAuthoringPreviewRevision secondRevision = second.ApplyEdit(secondEdit, true);

        Assert.Equal(firstEdit.HeightDeltas.ToArray(), secondEdit.HeightDeltas.ToArray());
        Assert.Equal(firstRevision.AffectedTiles.ToArray(), secondRevision.AffectedTiles.ToArray());
        for (int z = 0; z < first.Height; z++)
        {
            for (int x = 0; x < first.Width; x++)
            {
                Assert.Equal(first.GetHeightSample(x, z), second.GetHeightSample(x, z));
            }
        }
    }

    [Fact]
    public void FourLayerWeightBrushIsDeterministicAndNormalized()
    {
        TerrainAuthoringDocument first = CreateDocument(layerCount: 4);
        TerrainAuthoringDocument second = CreateDocument(layerCount: 4);
        var brush = new TerrainWeightBrush(2.0, 2.0, 1.75, LayerIndex: 2, Opacity: 160);

        TerrainBrushEdit firstEdit = first.CreateWeightBrushEdit(brush);
        TerrainBrushEdit secondEdit = second.CreateWeightBrushEdit(brush);
        first.ApplyEdit(firstEdit, true);
        second.ApplyEdit(secondEdit, true);

        Assert.Equal(firstEdit.WeightDeltas.ToArray(), secondEdit.WeightDeltas.ToArray());
        for (int z = 0; z < first.Height; z++)
        {
            for (int x = 0; x < first.Width; x++)
            {
                uint firstWeights = first.GetPackedWeights(x, z);
                Assert.Equal(firstWeights, second.GetPackedWeights(x, z));
                Assert.Equal(byte.MaxValue, SumWeights(firstWeights));
            }
        }
        Assert.True(GetWeight(first.GetPackedWeights(2, 2), 2) > 50);
    }

    [Fact]
    public void SharedEdgeSampleAffectsBothOwningTiles()
    {
        TerrainAuthoringDocument document = CreateDocument();

        TerrainBrushEdit edit = document.CreateHeightBrushEdit(
            new TerrainHeightBrush(2.0, 1.0, 0.25, 1_000));

        Assert.Equal(
            [new TerrainTileCoordinate(0, 0), new TerrainTileCoordinate(1, 0)],
            edit.AffectedTiles.ToArray());
    }

    [Fact]
    public void SharedCornerSampleAffectsFourOwningTiles()
    {
        TerrainAuthoringDocument document = CreateDocument();

        TerrainBrushEdit edit = document.CreateWeightBrushEdit(
            new TerrainWeightBrush(2.0, 2.0, 0.25, LayerIndex: 1, Opacity: 128));

        Assert.Equal(
            [
                new TerrainTileCoordinate(0, 0),
                new TerrainTileCoordinate(1, 0),
                new TerrainTileCoordinate(0, 1),
                new TerrainTileCoordinate(1, 1)
            ],
            edit.AffectedTiles.ToArray());
    }

    [Fact]
    public void CompactDeltaUndoAndRedoRestoreExactSamplesAndDirtyState()
    {
        TerrainAuthoringDocument document = CreateDocument();
        ushort[] original = SnapshotHeights(document);
        TerrainBrushEdit edit = document.CreateHeightBrushEdit(
            new TerrainHeightBrush(2.0, 2.0, 1.5, -4_000));

        document.ApplyEdit(edit, true);
        ushort[] edited = SnapshotHeights(document);
        Assert.False(original.SequenceEqual(edited));
        Assert.True(document.IsDirty);
        Assert.Equal(edit.ChangedSampleCount, document.DirtyHeightSampleCount);

        TerrainAuthoringPreviewRevision undone = document.ApplyEdit(edit, false);
        Assert.Equal(original, SnapshotHeights(document));
        Assert.False(document.IsDirty);
        Assert.False(undone.IsDirty);

        document.ApplyEdit(edit, true);
        Assert.Equal(edited, SnapshotHeights(document));
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void PreviewQueueCoalescesToTheLatestImmutableRevision()
    {
        TerrainAuthoringDocument document = CreateDocument();
        var previews = new TerrainAuthoringPreviewService();
        TerrainBrushEdit firstEdit = document.CreateHeightBrushEdit(
            new TerrainHeightBrush(1.0, 1.0, 0.5, 1_000));
        TerrainAuthoringPreviewRevision first = document.ApplyEdit(firstEdit, true);
        previews.Enqueue(first);
        TerrainBrushEdit secondEdit = document.CreateWeightBrushEdit(
            new TerrainWeightBrush(3.0, 3.0, 0.5, LayerIndex: 1, Opacity: 128));
        TerrainAuthoringPreviewRevision second = document.ApplyEdit(secondEdit, true);
        previews.Enqueue(second);

        TerrainAuthoringPreviewRevision[] pending = previews.DrainPending();

        TerrainAuthoringPreviewRevision latest = Assert.Single(pending);
        Assert.Equal(second.Revision, latest.Revision);
        Assert.True(previews.TryGetLatest(document.RootGuid, out TerrainAuthoringPreviewRevision retained));
        Assert.Same(second, retained);
        Assert.True(retained.TryGetChangedTile(
            first.ChangedTiles[0].Guid,
            out CookedTerrainTile retainedFirstTile));
        Assert.Equal(first.ChangedTiles[0].Guid, retainedFirstTile.Guid);
        Assert.Equal(2, retained.ChangedTiles.Count);
        Assert.Empty(previews.DrainPending());
    }

    [Fact]
    public void AuthoringBoundsRejectOversizedBrushesAndPreviewRootOverflow()
    {
        TerrainAuthoringDocument large = CreateDocument(
            tileCountX: 1,
            tileCountZ: 1,
            resolution: 513,
            rootSeed: 73);
        Assert.Throws<InvalidOperationException>(() => large.CreateHeightBrushEdit(
            new TerrainHeightBrush(256.0, 256.0, 1_000.0, 1_000)));

        TerrainAuthoringDocument firstDocument = CreateDocument(rootSeed: 74);
        TerrainAuthoringDocument secondDocument = CreateDocument(rootSeed: 75);
        TerrainAuthoringPreviewRevision first = ApplyCenterHeight(firstDocument);
        TerrainAuthoringPreviewRevision second = ApplyCenterHeight(secondDocument);
        var previews = new TerrainAuthoringPreviewService(maximumRoots: 1);
        previews.Enqueue(first);

        Assert.Throws<InvalidOperationException>(() => previews.Enqueue(second));
    }

    [Fact]
    public void PartialRuntimeReplacementPreservesUnrelatedTileGeneration()
    {
        TerrainRuntimeFixture original = TerrainRuntimeTestData.Create(
            2,
            1,
            resolution: 3,
            height: (_, _) => 1_000);
        TerrainRuntimeFixture changed = TerrainRuntimeTestData.Create(
            2,
            1,
            resolution: 3,
            height: (x, _) => x < 2 ? (ushort)50_000 : (ushort)1_000);
        var runtime = new TerrainRuntimeDataStore();
        runtime.PublishRoot(original.Root);
        TerrainResidentResourceHandle changedHandle = runtime.PublishTile(original.Tiles[0]);
        TerrainResidentResourceHandle unchangedHandle = runtime.PublishTile(original.Tiles[1]);

        TerrainRuntimePublication publication = runtime.PublishReplacement(
            changed.Root,
            [changed.Tiles[0]]);

        Assert.Single(publication.Tiles);
        Assert.True(publication.Tiles[0].Generation > changedHandle.Generation);
        Assert.True(runtime.TryGetTile(original.Tiles[1].Guid, out TerrainResidentTileData unchanged));
        Assert.Equal(unchangedHandle.Generation, unchanged.Generation);
        Assert.Same(original.Tiles[1], unchanged.Tile);
    }

    [Fact]
    public void InvalidPartialReplacementRetainsEveryPreviousGeneration()
    {
        TerrainRuntimeFixture original = TerrainRuntimeTestData.Create(2, 1, resolution: 3);
        var runtime = new TerrainRuntimeDataStore();
        TerrainResidentResourceHandle rootHandle = runtime.PublishRoot(original.Root);
        TerrainResidentResourceHandle firstHandle = runtime.PublishTile(original.Tiles[0]);
        TerrainResidentResourceHandle secondHandle = runtime.PublishTile(original.Tiles[1]);
        CookedTerrainRoot invalidRoot = original.Root with
        {
            Layers = [original.Root.Layers[0], original.Root.Layers[0] with { Id = "invalid" }]
        };

        Assert.Throws<InvalidOperationException>(() =>
            runtime.PublishReplacement(invalidRoot, [original.Tiles[0]]));

        Assert.True(runtime.TryGetTile(original.Tiles[0].Guid, out TerrainResidentTileData first));
        Assert.True(runtime.TryGetTile(original.Tiles[1].Guid, out TerrainResidentTileData second));
        Assert.Equal(firstHandle.Generation, first.Generation);
        Assert.Equal(secondHandle.Generation, second.Generation);
        Assert.True(runtime.Remove(rootHandle));
    }

    private static TerrainAuthoringPreviewRevision ApplyCenterHeight(
        TerrainAuthoringDocument document)
    {
        TerrainBrushEdit edit = document.CreateHeightBrushEdit(
            new TerrainHeightBrush(2.0, 2.0, 0.5, 1_000));
        return document.ApplyEdit(edit, true);
    }

    private static TerrainAuthoringDocument CreateDocument(
        int tileCountX = 2,
        int tileCountZ = 2,
        int resolution = 3,
        int layerCount = 4,
        int rootSeed = 71)
    {
        Guid rootGuid = CreateGuid(rootSeed, 1);
        Guid layerSetGuid = CreateGuid(rootSeed, 2);
        const string packageId = "com.arisen.tests.terrain-authoring";
        int intervals = resolution - 1;
        int width = checked((tileCountX * intervals) + 1);
        int height = checked((tileCountZ * intervals) + 1);
        TerrainGeneratedTileRecord[] records = TerrainTileIdentity.CreateRecords(
            rootGuid,
            packageId,
            new TerrainTileCoordinate(0, 0),
            tileCountX,
            tileCountZ);
        var root = new TerrainRootSourceDescriptor(
            rootGuid,
            packageId,
            TerrainRootSourceAssetLoader.CurrentSourceSchemaVersion,
            "Authoring fixture",
            new WorldPosition(0.0, 0.0, 0.0),
            new TerrainSampleSpacing(1.0, 1.0),
            new TerrainHeightRange(0.0, 100.0),
            new TerrainHeightSourceDescriptor(
                "Height/Fixture.pgm",
                "Fixture.pgm",
                TerrainHeightSourceFormat.Pgm16BigEndianScalar,
                width,
                height),
            resolution,
            TerrainBorderPolicy.SharedEdgeSamples,
            new TerrainTileCoordinate(0, 0),
            new AssetRef<TerrainLayerSetSourceAsset>(
                layerSetGuid,
                TerrainAssetTypes.LayerSet,
                packageId),
            records)
        {
            WeightSource = new TerrainWeightSourceDescriptor(
                "Fixture.ariweights",
                "Fixture.ariweights",
                TerrainWeightSourceFormat.Rgba8Hex,
                width,
                height)
        };
        var layers = new TerrainLayerDescriptor[layerCount];
        for (int index = 0; index < layers.Length; index++)
        {
            layers[index] = new TerrainLayerDescriptor(
                $"layer-{index}",
                Texture(CreateGuid(rootSeed, 10 + (index * 3)), packageId),
                Texture(CreateGuid(rootSeed, 11 + (index * 3)), packageId),
                Texture(CreateGuid(rootSeed, 12 + (index * 3)), packageId),
                TerrainLayerTint.White,
                1.0f,
                0.0f,
                1.0f,
                TerrainLayerWorldTiling.Default);
        }
        var layerSet = new TerrainLayerSetSourceDescriptor(
            layerSetGuid,
            packageId,
            TerrainLayerSetSourceAssetLoader.CurrentSourceSchemaVersion,
            "Authoring layers",
            layers);
        var heights = new ushort[checked(width * height)];
        var weights = new byte[checked(heights.Length * TerrainCookedFormat.WeightChannelCount)];
        for (int sample = 0; sample < heights.Length; sample++)
        {
            heights[sample] = 30_000;
            int offset = sample * TerrainCookedFormat.WeightChannelCount;
            if (layerCount == 4)
            {
                weights[offset] = 100;
                weights[offset + 1] = 80;
                weights[offset + 2] = 50;
                weights[offset + 3] = 25;
            }
            else
            {
                weights[offset] = byte.MaxValue;
            }
        }

        return new TerrainAuthoringDocument(
            root,
            layerSet,
            new TerrainHeightField(width, height, heights),
            new TerrainWeightField(width, height, weights));
    }

    private static AssetRef<Texture2DSourceAsset> Texture(Guid guid, string packageId) =>
        new(guid, "Texture2D", packageId);

    private static Guid CreateGuid(int group, int item) => new(
        group,
        checked((short)item),
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        1);

    private static ushort[] SnapshotHeights(TerrainAuthoringDocument document)
    {
        var result = new ushort[checked(document.Width * document.Height)];
        for (int z = 0; z < document.Height; z++)
        {
            for (int x = 0; x < document.Width; x++)
            {
                result[(z * document.Width) + x] = document.GetHeightSample(x, z);
            }
        }
        return result;
    }

    private static int SumWeights(uint packed) =>
        GetWeight(packed, 0) +
        GetWeight(packed, 1) +
        GetWeight(packed, 2) +
        GetWeight(packed, 3);

    private static byte GetWeight(uint packed, int channel) =>
        checked((byte)((packed >> (channel * 8)) & byte.MaxValue));
}
