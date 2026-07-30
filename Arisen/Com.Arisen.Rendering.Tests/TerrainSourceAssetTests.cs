using System.Buffers.Binary;
using System.Text;
using ArisenEngine.Core.Assets;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainSourceAssetTests
{
    private static readonly Guid s_TerrainGuid =
        Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid s_LayerSetGuid =
        Guid.Parse("22222222-3333-4444-5555-666666666666");

    [Fact]
    public void TerrainTileIdentity_DerivesStableSignedCoordinateMetadata()
    {
        var negative = new TerrainTileCoordinate(-1, 0);
        var zero = new TerrainTileCoordinate(0, 0);

        Assert.Equal("x=-1;z=0", TerrainTileIdentity.CreateChildKey(negative));
        Assert.Equal(
            Guid.Parse("bdf7512b-8012-9cd3-909a-da247c7a65aa"),
            TerrainTileIdentity.CreateGuid(s_TerrainGuid, "Com.Arisen.Test", negative));
        Assert.Equal(
            Guid.Parse("d5cd3ec1-4b54-e79e-ec5d-7de7d9ca1f44"),
            TerrainTileIdentity.CreateGuid(s_TerrainGuid, "com.arisen.test", zero));
        Assert.NotEqual(
            TerrainTileIdentity.CreateGuid(s_TerrainGuid, "com.arisen.test", negative),
            TerrainTileIdentity.CreateGuid(s_TerrainGuid, "com.arisen.test", zero));

        AssetMetadata metadata = TerrainTileIdentity.CreateMetadata(
            s_TerrainGuid,
            "Com.Arisen.Test",
            negative);
        Assert.Equal(TerrainAssetTypes.Tile, metadata.AssetType);
        Assert.Equal(TerrainTileIdentity.Importer, metadata.Importer);
        Assert.NotNull(metadata.Generated);
        Assert.Equal(s_TerrainGuid, metadata.Generated.SourceGuid);
        Assert.Equal("com.arisen.test", metadata.Generated.SourcePackageId);
        Assert.Equal(TerrainTileIdentity.ChildKind, metadata.Generated.ChildKind);
        Assert.Equal("x=-1;z=0", metadata.Generated.ChildKey);

        TerrainGeneratedTileRecord[] records = TerrainTileIdentity.CreateRecords(
            s_TerrainGuid,
            "com.arisen.test",
            new TerrainTileCoordinate(-1, -1),
            tileCountX: 2,
            tileCountZ: 2);
        Assert.Equal(
            [
                new TerrainTileCoordinate(-1, -1),
                new TerrainTileCoordinate(0, -1),
                new TerrainTileCoordinate(-1, 0),
                new TerrainTileCoordinate(0, 0)
            ],
            records.Select(record => record.Coordinate));
    }

    [Fact]
    public void TerrainHeightSourceDecoder_DecodesLosslessBigEndianScalarSamples()
    {
        ushort[] expected =
        [
            0, 1, 255, 256, 1_024,
            16_384, 32_767, 32_768, 49_152, 65_534,
            65_535, 42, 4_096, 12_345, 54_321
        ];
        byte[] source = CreatePgm(
            width: 5,
            height: 3,
            expected,
            magic: "P5",
            maxValue: 65_535,
            includeComment: true,
            newline: "\r\n");

        TerrainHeightField decoded = TerrainHeightSourceDecoder.Decode(source, "fixture.pgm");

        Assert.Equal(5, decoded.Width);
        Assert.Equal(3, decoded.Height);
        Assert.Equal(expected, decoded.Samples.ToArray());
        Assert.Equal((ushort)32_768, decoded.GetSample(2, 1));
    }

    [Fact]
    public void TerrainHeightSourceDecoder_RejectsAmbiguousOrMalformedInputs()
    {
        ushort[] sample = [12_345];
        byte[] valid = CreatePgm(1, 1, sample);
        byte[] truncated = valid[..^1];
        byte[] trailing = [.. valid, 0x7f];
        byte[] extraHeaderSeparator = CreatePgm(
            1,
            1,
            sample,
            separatorAfterMaxValue: "\n\n");
        byte[] nonAsciiHeader = CreatePgm(1, 1, sample, includeComment: true);
        nonAsciiHeader[Array.IndexOf(nonAsciiHeader, (byte)'#') + 2] = 0xff;

        Assert.Contains(
            "expected binary grayscale P5",
            Assert.Throws<InvalidDataException>(() =>
                TerrainHeightSourceDecoder.Decode(CreatePgm(1, 1, sample, magic: "P2"))).Message);
        Assert.Contains(
            "expected binary grayscale P5",
            Assert.Throws<InvalidDataException>(() =>
                TerrainHeightSourceDecoder.Decode(CreatePgm(1, 1, sample, magic: "P6"))).Message);
        Assert.Contains(
            "expected exactly 65535",
            Assert.Throws<InvalidDataException>(() =>
                TerrainHeightSourceDecoder.Decode(CreatePgm(1, 1, sample, maxValue: 255))).Message);
        Assert.Contains(
            "truncated",
            Assert.Throws<InvalidDataException>(() =>
                TerrainHeightSourceDecoder.Decode(truncated)).Message);
        Assert.Contains(
            "trailing data",
            Assert.Throws<InvalidDataException>(() =>
                TerrainHeightSourceDecoder.Decode(trailing)).Message);
        Assert.Contains(
            "trailing data",
            Assert.Throws<InvalidDataException>(() =>
                TerrainHeightSourceDecoder.Decode(extraHeaderSeparator)).Message);
        Assert.Contains(
            "non-ASCII byte",
            Assert.Throws<InvalidDataException>(() =>
                TerrainHeightSourceDecoder.Decode(nonAsciiHeader)).Message);
    }

    [Fact]
    public void TerrainWeightSourceDecoder_DecodesExplicitRgbaSamples()
    {
        TerrainWeightField decoded = TerrainWeightSourceDecoder.Decode(
            "ARIWEIGHTS\n1\n2 2\nff000000 80402000\n00000000 01020304\n"u8,
            "fixture.ariweights");

        Assert.Equal(2, decoded.Width);
        Assert.Equal(2, decoded.Height);
        Assert.Equal([255, 0, 0, 0], decoded.GetSample(0, 0).ToArray());
        Assert.Equal([128, 64, 32, 0], decoded.GetSample(1, 0).ToArray());
        Assert.Equal([1, 2, 3, 4], decoded.GetSample(1, 1).ToArray());
        Assert.Contains(
            "trailing sample data",
            Assert.Throws<InvalidDataException>(() => TerrainWeightSourceDecoder.Decode(
                "ARIWEIGHTS 1 1 1 ff000000 extra"u8)).Message);
    }

    [Fact]
    public void TerrainWeightNormalizationIsExactAndUsesStableZeroFallback()
    {
        var normalized = new byte[TerrainCookedFormat.WeightChannelCount];
        TerrainTileAssetCooker.NormalizeLayerWeights(
            [1, 1, 0, 0],
            2,
            normalized,
            "test weights");
        Assert.Equal([128, 127, 0, 0], normalized.ToArray());

        TerrainTileAssetCooker.NormalizeLayerWeights(
            [0, 0, 0, 0],
            4,
            normalized,
            "test weights");
        Assert.Equal([255, 0, 0, 0], normalized.ToArray());
        Assert.Throws<InvalidDataException>(() =>
            TerrainTileAssetCooker.NormalizeLayerWeights(
                [1, 1, 1, 0],
                2,
                normalized,
                "test weights"));
    }

    [Fact]
    public void TerrainLayerSetLoader_PreservesBoundedOrderAndTypedTextureReferences()
    {
        using var fixture = TerrainFixture.Create();
        string sourcePath = fixture.WriteText("Assets/Valley.ariterrainlayers", CreateLayerSetSource());
        var sourceAsset = new AssetRecord(
            s_LayerSetGuid,
            TerrainAssetTypes.LayerSet,
            sourcePath,
            sourcePath + ".meta",
            "Com.Arisen.Test");

        TerrainLayerSetSourceDescriptor descriptor =
            TerrainLayerSetSourceAssetLoader.LoadSource(sourceAsset);

        Assert.Equal(s_LayerSetGuid, descriptor.Guid);
        Assert.Equal("com.arisen.test", descriptor.PackageId);
        Assert.Equal("Valley Layers", descriptor.Name);
        Assert.Equal(["rock", "grass"], descriptor.Layers.Select(layer => layer.Id));
        Assert.Equal("Texture2D", descriptor.Layers[0].Albedo.AssetType);
        Assert.Equal("com.arisen.textures", descriptor.Layers[0].Albedo.PackageId);
        Assert.Equal(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            descriptor.Layers[0].Albedo.Guid);
        Assert.Equal(
            Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003"),
            descriptor.Layers[1].Orm.Guid);
        Assert.Equal(
            new TerrainLayerTint(0.72f, 0.84f, 0.61f, 1.0f),
            descriptor.Layers[0].Tint);
        Assert.Equal(0.82f, descriptor.Layers[0].RoughnessMultiplier);
        Assert.Equal(0.05f, descriptor.Layers[0].MetallicMultiplier);
        Assert.Equal(1.35f, descriptor.Layers[0].NormalStrength);
        Assert.Equal(
            new TerrainLayerWorldTiling(1.5f, 2.25f),
            descriptor.Layers[0].WorldTiling);
        Assert.Equal(TerrainLayerTint.White, descriptor.Layers[1].Tint);
        Assert.Equal(TerrainLayerWorldTiling.Default, descriptor.Layers[1].WorldTiling);
    }

    [Fact]
    public void TerrainLayerSetLoader_RejectsDuplicateLayersAndUnknownFields()
    {
        string duplicate = CreateLayerSetSource().Replace("Id: Grass", "Id: Rock");
        string unknown = CreateLayerSetSource().Replace(
            "Name: Valley Layers",
            "Name: Valley Layers\nUnexpected: true");

        Assert.Contains(
            "duplicate layer id 'rock'",
            Assert.Throws<InvalidDataException>(() =>
                TerrainLayerSetSourceAssetLoader.LoadSourceText(
                    s_LayerSetGuid,
                    "com.arisen.test",
                    "duplicate.ariterrainlayers",
                    duplicate)).Message);
        Assert.Contains(
            "unknown field 'Unexpected'",
            Assert.Throws<InvalidDataException>(() =>
                TerrainLayerSetSourceAssetLoader.LoadSourceText(
                    s_LayerSetGuid,
                    "com.arisen.test",
                    "unknown.ariterrainlayers",
                    unknown)).Message);
    }

    [Fact]
    public void TerrainLayerSetLoader_RejectsInvalidMaterialParameters()
    {
        string invalidTint = CreateLayerSetSource().Replace("R: 0.72", "R: 1.25");
        string invalidTiling = CreateLayerSetSource().Replace("X: 1.5", "X: 0.0");
        string invalidNormalStrength = CreateLayerSetSource().Replace(
            "NormalStrength: 1.35",
            "NormalStrength: 8.0");

        Assert.Contains(
            "material parameters",
            Assert.Throws<InvalidDataException>(() =>
                TerrainLayerSetSourceAssetLoader.LoadSourceText(
                    s_LayerSetGuid,
                    "com.arisen.test",
                    "invalid-tint.ariterrainlayers",
                    invalidTint)).Message);
        Assert.Contains(
            "material parameters",
            Assert.Throws<InvalidDataException>(() =>
                TerrainLayerSetSourceAssetLoader.LoadSourceText(
                    s_LayerSetGuid,
                    "com.arisen.test",
                    "invalid-tiling.ariterrainlayers",
                    invalidTiling)).Message);
        Assert.Contains(
            "material parameters",
            Assert.Throws<InvalidDataException>(() =>
                TerrainLayerSetSourceAssetLoader.LoadSourceText(
                    s_LayerSetGuid,
                    "com.arisen.test",
                    "invalid-normal.ariterrainlayers",
                    invalidNormalStrength)).Message);
    }

    [Fact]
    public void TerrainRootLoader_ValidatesFixtureAndCanonicalizesGeneratedTileOrder()
    {
        using var fixture = TerrainFixture.Create();
        ushort[] samples = Enumerable.Range(0, 15)
            .Select(value => checked((ushort)(value * 4_000)))
            .ToArray();
        fixture.WritePgm("Assets/Height/Valley.pgm", 5, 3, samples);
        string sourcePath = fixture.WriteText(
            "Assets/Valley.aristerrain",
            CreateTerrainRootSource());
        var sourceAsset = new AssetRecord(
            s_TerrainGuid,
            TerrainAssetTypes.Root,
            sourcePath,
            sourcePath + ".meta",
            "Com.Arisen.Test");

        TerrainRootSourceDescriptor descriptor = TerrainRootSourceAssetLoader.LoadSource(sourceAsset);

        Assert.Equal(s_TerrainGuid, descriptor.Guid);
        Assert.Equal("com.arisen.test", descriptor.PackageId);
        Assert.Equal("Signed Valley Fixture", descriptor.Name);
        Assert.Equal(new WorldPosition(-256.0, -32.0, 128.0), descriptor.WorldPlacement);
        Assert.Equal(new TerrainSampleSpacing(2.0, 4.0), descriptor.SampleSpacing);
        Assert.Equal(new TerrainHeightRange(-64.0, 192.0), descriptor.HeightRange);
        Assert.Equal(256.0, descriptor.HeightRange.Scale);
        Assert.Equal(5, descriptor.HeightSource.Width);
        Assert.Equal(3, descriptor.HeightSource.Height);
        Assert.Equal(TerrainHeightSourceFormat.Pgm16BigEndianScalar, descriptor.HeightSource.Format);
        Assert.Equal(3, descriptor.TileResolution);
        Assert.Equal(TerrainBorderPolicy.SharedEdgeSamples, descriptor.BorderPolicy);
        Assert.Equal(new TerrainTileCoordinate(-1, 0), descriptor.TileOrigin);
        Assert.Equal(s_LayerSetGuid, descriptor.LayerSet.Guid);
        Assert.Equal("com.arisen.test", descriptor.LayerSet.PackageId);
        Assert.Equal(
            [new TerrainTileCoordinate(-1, 0), new TerrainTileCoordinate(0, 0)],
            descriptor.GeneratedTiles.Select(tile => tile.Coordinate));
        Assert.Equal(
            [
                Guid.Parse("bdf7512b-8012-9cd3-909a-da247c7a65aa"),
                Guid.Parse("d5cd3ec1-4b54-e79e-ec5d-7de7d9ca1f44")
            ],
            descriptor.GeneratedTiles.Select(tile => tile.Guid));

        fixture.WritePgm(
            "Assets/Height/Valley.pgm",
            5,
            3,
            samples.Select(value => checked((ushort)(ushort.MaxValue - value))).ToArray());
        TerrainRootSourceDescriptor reimported = TerrainRootSourceAssetLoader.LoadSource(sourceAsset);
        Assert.Equal(
            descriptor.GeneratedTiles.Select(tile => tile.Guid),
            reimported.GeneratedTiles.Select(tile => tile.Guid));
    }

    [Fact]
    public void TerrainRootLoader_RejectsStaleIdentityAndNonTileableHeightDimensions()
    {
        using var fixture = TerrainFixture.Create();
        fixture.WritePgm("Assets/Height/Valley.pgm", 5, 3, new ushort[15]);
        string valid = CreateTerrainRootSource();
        string stale = valid.Replace(
            "bdf7512b-8012-9cd3-909a-da247c7a65aa",
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        Assert.Contains(
            "expected 'bdf7512b-8012-9cd3-909a-da247c7a65aa'",
            Assert.Throws<InvalidDataException>(() =>
                TerrainRootSourceAssetLoader.LoadSourceText(
                    s_TerrainGuid,
                    "com.arisen.test",
                    fixture.PathFor("Assets/Valley.aristerrain"),
                    stale)).Message);

        fixture.WritePgm("Assets/Height/Valley.pgm", 4, 3, new ushort[12]);
        Assert.Contains(
            "must be exact shared-edge multiples",
            Assert.Throws<InvalidDataException>(() =>
                TerrainRootSourceAssetLoader.LoadSourceText(
                    s_TerrainGuid,
                    "com.arisen.test",
                    fixture.PathFor("Assets/Valley.aristerrain"),
                    valid)).Message);
    }

    private static string CreateTerrainRootSource()
    {
        return $$"""
            Version: 1
            TerrainGuid: {{s_TerrainGuid:D}}
            Name: Signed Valley Fixture
            WorldPlacement: { X: -256.0, Y: -32.0, Z: 128.0 }
            SampleSpacing: { X: 2.0, Z: 4.0 }
            HeightRange: { Min: -64.0, Max: 192.0 }
            HeightSource:
              Path: Height/Valley.pgm
              Format: Pgm16BigEndianScalar
            TileResolution: 3
            BorderPolicy: SharedEdgeSamples
            TileOrigin: { X: -1, Z: 0 }
            LayerSet:
              Guid: {{s_LayerSetGuid:D}}
              PackageId: com.arisen.test
            GeneratedTiles:
            - Coordinate: { X: 0, Z: 0 }
              Guid: d5cd3ec1-4b54-e79e-ec5d-7de7d9ca1f44
            - Coordinate: { X: -1, Z: 0 }
              Guid: bdf7512b-8012-9cd3-909a-da247c7a65aa
            """;
    }

    private static string CreateLayerSetSource()
    {
        return $$"""
            Version: 2
            LayerSetGuid: {{s_LayerSetGuid:D}}
            Name: Valley Layers
            Layers:
            - Id: Rock
              Albedo: { Guid: aaaaaaaa-0000-0000-0000-000000000001, PackageId: Com.Arisen.Textures }
              Normal: { Guid: aaaaaaaa-0000-0000-0000-000000000002, PackageId: Com.Arisen.Textures }
              Orm: { Guid: aaaaaaaa-0000-0000-0000-000000000003, PackageId: Com.Arisen.Textures }
              Tint: { R: 0.72, G: 0.84, B: 0.61, A: 1.0 }
              RoughnessMultiplier: 0.82
              MetallicMultiplier: 0.05
              NormalStrength: 1.35
              WorldTiling: { X: 1.5, Z: 2.25 }
            - Id: Grass
              Albedo: { Guid: bbbbbbbb-0000-0000-0000-000000000001, PackageId: Com.Arisen.Textures }
              Normal: { Guid: bbbbbbbb-0000-0000-0000-000000000002, PackageId: Com.Arisen.Textures }
              Orm: { Guid: bbbbbbbb-0000-0000-0000-000000000003, PackageId: Com.Arisen.Textures }
            """;
    }

    private static byte[] CreatePgm(
        int width,
        int height,
        IReadOnlyList<ushort> samples,
        string magic = "P5",
        int maxValue = 65_535,
        bool includeComment = false,
        string newline = "\n",
        string? separatorAfterMaxValue = null)
    {
        string comment = includeComment ? $"# scalar height fixture{newline}" : string.Empty;
        string separator = separatorAfterMaxValue ?? newline;
        byte[] header = Encoding.ASCII.GetBytes(
            $"{magic}{newline}{comment}{width} {height}{newline}{maxValue}{separator}");
        byte[] output = new byte[checked(header.Length + (samples.Count * sizeof(ushort)))];
        header.CopyTo(output, 0);
        for (int index = 0; index < samples.Count; index++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(
                output.AsSpan(header.Length + (index * sizeof(ushort)), sizeof(ushort)),
                samples[index]);
        }

        return output;
    }

    private sealed class TerrainFixture : IDisposable
    {
        private TerrainFixture(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TerrainFixture Create()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "ArisenTerrainSourceTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TerrainFixture(root);
        }

        public string PathFor(string relativePath)
        {
            return Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public string WriteText(string relativePath, string text)
        {
            string path = PathFor(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
            return path;
        }

        public string WritePgm(
            string relativePath,
            int width,
            int height,
            IReadOnlyList<ushort> samples)
        {
            string path = PathFor(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, CreatePgm(width, height, samples));
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
