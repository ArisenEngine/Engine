using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.Serialization;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainCookedAssetTests
{
    private static readonly Guid s_RootGuid =
        Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid s_LayerSetGuid =
        Guid.Parse("22222222-3333-4444-5555-666666666666");
    private const string PackageId = "com.arisen.test";

    [Fact]
    public void CookedTileV1_RoundTripsDeterministicallyWithSharedBordersAndErrors()
    {
        TerrainCookedFixture fixture = CreateCookedFixture();
        CookedTerrainTile first = fixture.Tiles[0];
        byte[] firstBytes = TerrainTileAssetCooker.WritePayload(first);
        byte[] repeatedBytes = TerrainTileAssetCooker.WritePayload(first);

        Assert.Equal(firstBytes, repeatedBytes);
        Assert.True(
            TerrainTileAssetCooker.TryReadPayload(
                first.Guid,
                fixture.RootSource.Guid,
                fixture.LayerSet.Guid,
                PackageId,
                firstBytes,
                "tile-memory",
                out CookedTerrainTile loaded,
                out string diagnostic),
            diagnostic);
        Assert.Equal(new TerrainTileCoordinate(-1, -1), loaded.Coordinate);
        Assert.Equal(new WorldPosition(-256.0, -32.0, 128.0), loaded.WorldPlacement);
        Assert.Equal(new TerrainSampleSpacing(2.0, 4.0), loaded.SampleSpacing);
        Assert.Equal(2, loaded.LayerCount);
        Assert.Equal(first.Heights.ToArray(), loaded.Heights.ToArray());
        Assert.Equal(255, loaded.GetLayerWeight(1, 1, 0));
        Assert.Equal(0, loaded.GetLayerWeight(1, 1, 1));
        Assert.Equal([1, 2], loaded.GeometricErrors.Select(error => error.SampleStep));
        Assert.Equal(0.0, loaded.GeometricErrors[0].MaxError);
        Assert.True(loaded.GeometricErrors[1].MaxError > 0.0);

        CookedTerrainTile positiveX = fixture.Tiles[1];
        CookedTerrainTile positiveZ = fixture.Tiles[2];
        TerrainTileAssetCooker.ValidateSharedBorders(fixture.Tiles);
        for (int sample = 0; sample < first.Resolution; sample++)
        {
            Assert.Equal(
                first.GetHeightSample(first.Resolution - 1, sample),
                positiveX.GetHeightSample(0, sample));
            Assert.Equal(
                first.GetHeightSample(sample, first.Resolution - 1),
                positiveZ.GetHeightSample(sample, 0));
        }
    }

    [Fact]
    public void CookedRootV2_RoundTripsCanonicalTopologyHashesAndDependencies()
    {
        TerrainCookedFixture fixture = CreateCookedFixture();
        byte[] first = TerrainRootAssetCooker.WritePayload(fixture.Root);
        byte[] second = TerrainRootAssetCooker.WritePayload(fixture.Root);

        Assert.Equal(first, second);
        Assert.True(
            TerrainRootAssetCooker.TryReadPayload(
                s_RootGuid,
                PackageId,
                first,
                "root-memory",
                out CookedTerrainRoot loaded,
                out string diagnostic),
            diagnostic);
        Assert.Equal(4, loaded.Tiles.Count);
        Assert.Equal(2, loaded.Layers.Count);
        Assert.Equal(["rock", "grass"], loaded.Layers.Select(layer => layer.Id));
        Assert.Equal(
            [
                new TerrainTileCoordinate(-1, -1),
                new TerrainTileCoordinate(0, -1),
                new TerrainTileCoordinate(-1, 0),
                new TerrainTileCoordinate(0, 0)
            ],
            loaded.Tiles.Select(tile => tile.Coordinate));
        Assert.Equal(Guid.Empty, loaded.Tiles[0].Neighbors.NegativeX);
        Assert.Equal(loaded.Tiles[1].Guid, loaded.Tiles[0].Neighbors.PositiveX);
        Assert.Equal(Guid.Empty, loaded.Tiles[0].Neighbors.NegativeZ);
        Assert.Equal(loaded.Tiles[2].Guid, loaded.Tiles[0].Neighbors.PositiveZ);
        Assert.All(loaded.Tiles, tile => Assert.Equal(32, tile.ContentHash.Length));

        TerrainCookedAssetDependency[] dependencies =
            TerrainRootAssetCooker.BuildDependencies(loaded);
        Assert.Equal(10, dependencies.Length);
        Assert.Equal(4, dependencies.Count(dependency => dependency.AssetType == TerrainAssetTypes.Tile));
        Assert.Equal(6, dependencies.Count(dependency => dependency.AssetType == "Texture2D"));
        Assert.All(
            dependencies.Where(dependency => dependency.AssetType == TerrainAssetTypes.Tile),
            dependency => Assert.Equal(TerrainTileAssetCooker.RuntimeVariant, dependency.Variant));
    }

    [Fact]
    public void CookedTileV1_RejectsHeaderHashDirectoryWeightAndErrorCorruption()
    {
        TerrainCookedFixture fixture = CreateCookedFixture();
        CookedTerrainTile tile = fixture.Tiles[0];
        byte[] valid = TerrainTileAssetCooker.WritePayload(tile);

        byte[] wrongMagic = valid.ToArray();
        wrongMagic[0] ^= 0xff;
        AssertTileRejected(fixture, tile, wrongMagic, "magic");

        byte[] wrongVersion = valid.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(wrongVersion.AsSpan(12), 99);
        AssertTileRejected(fixture, tile, wrongVersion, "header");

        byte[] wrongHash = valid.ToArray();
        wrongHash[^1] ^= 0x01;
        AssertTileRejected(fixture, tile, wrongHash, "content hash");

        byte[] malformedResolution = valid.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(malformedResolution.AsSpan(80), 4);
        AssertTileRejected(fixture, tile, malformedResolution, "resolution");

        byte[] unknownRequired = valid.ToArray();
        int errorDescriptor = FindSectionDescriptor(
            unknownRequired,
            TerrainTileAssetCooker.HeaderSize,
            sectionCountOffset: 88,
            (uint)CookedTerrainTileSectionType.GeometricErrors);
        BinaryPrimitives.WriteUInt32LittleEndian(unknownRequired.AsSpan(errorDescriptor), 99);
        RehashTile(unknownRequired);
        AssertTileRejected(fixture, tile, unknownRequired, "unknown required section");

        byte[] overlapping = valid.ToArray();
        int heightsDescriptor = FindSectionDescriptor(
            overlapping,
            TerrainTileAssetCooker.HeaderSize,
            88,
            (uint)CookedTerrainTileSectionType.Heights);
        int weightsDescriptor = FindSectionDescriptor(
            overlapping,
            TerrainTileAssetCooker.HeaderSize,
            88,
            (uint)CookedTerrainTileSectionType.LayerWeights);
        ulong heightOffset = BinaryPrimitives.ReadUInt64LittleEndian(overlapping.AsSpan(heightsDescriptor + 8));
        BinaryPrimitives.WriteUInt64LittleEndian(overlapping.AsSpan(weightsDescriptor + 8), heightOffset);
        RehashTile(overlapping);
        AssertTileRejected(fixture, tile, overlapping, "overlap");

        byte[] oversizedSection = valid.ToArray();
        BinaryPrimitives.WriteUInt64LittleEndian(
            oversizedSection.AsSpan(heightsDescriptor + 16),
            ulong.MaxValue);
        RehashTile(oversizedSection);
        AssertTileRejected(fixture, tile, oversizedSection, "range overflows");

        byte[] invalidWeights = valid.ToArray();
        int weightOffset = checked((int)ReadSectionOffset(
            invalidWeights,
            TerrainTileAssetCooker.HeaderSize,
            88,
            (uint)CookedTerrainTileSectionType.LayerWeights));
        invalidWeights[weightOffset + 1] = 1;
        RehashTile(invalidWeights);
        AssertTileRejected(fixture, tile, invalidWeights, "weights sum");

        byte[] invalidErrors = valid.ToArray();
        int errorsOffset = checked((int)ReadSectionOffset(
            invalidErrors,
            TerrainTileAssetCooker.HeaderSize,
            88,
            (uint)CookedTerrainTileSectionType.GeometricErrors));
        BinaryPrimitives.WriteDoubleLittleEndian(
            invalidErrors.AsSpan(errorsOffset + TerrainTileAssetCooker.GeometricErrorStride + 8),
            -1.0);
        RehashTile(invalidErrors);
        AssertTileRejected(fixture, tile, invalidErrors, "geometric-error");

        AssertTileRejected(fixture, tile, valid[..^1], "size");
    }

    [Fact]
    public void CookedRootV2_RejectsStaleIdentityInvalidNeighborsAndMalformedGrid()
    {
        TerrainCookedFixture fixture = CreateCookedFixture();
        byte[] valid = TerrainRootAssetCooker.WritePayload(fixture.Root);

        byte[] staleIdentity = valid.ToArray();
        int tileOffset = checked((int)ReadSectionOffset(
            staleIdentity,
            TerrainRootAssetCooker.HeaderSize,
            sectionCountOffset: 56,
            (uint)CookedTerrainRootSectionType.Tiles));
        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee").TryWriteBytes(
            staleIdentity.AsSpan(tileOffset + 8, 16));
        RehashRoot(staleIdentity);
        AssertRootRejected(staleIdentity, "stale coordinate or deterministic identity");

        byte[] invalidNeighbor = valid.ToArray();
        Guid.Empty.TryWriteBytes(invalidNeighbor.AsSpan(tileOffset + 40, 16));
        RehashRoot(invalidNeighbor);
        AssertRootRejected(invalidNeighbor, "invalid neighbor identities");

        byte[] malformedGrid = valid.ToArray();
        int metadataOffset = checked((int)ReadSectionOffset(
            malformedGrid,
            TerrainRootAssetCooker.HeaderSize,
            56,
            (uint)CookedTerrainRootSectionType.Metadata));
        BinaryPrimitives.WriteUInt32LittleEndian(malformedGrid.AsSpan(metadataOffset + 76), 4);
        RehashRoot(malformedGrid);
        AssertRootRejected(malformedGrid, "source dimensions");

        byte[] oversizedSection = valid.ToArray();
        int tilesDescriptor = FindSectionDescriptor(
            oversizedSection,
            TerrainRootAssetCooker.HeaderSize,
            56,
            (uint)CookedTerrainRootSectionType.Tiles);
        BinaryPrimitives.WriteUInt64LittleEndian(
            oversizedSection.AsSpan(tilesDescriptor + 16),
            ulong.MaxValue);
        RehashRoot(oversizedSection);
        AssertRootRejected(oversizedSection, "range overflows");

        byte[] wrongHash = valid.ToArray();
        wrongHash[^1] ^= 0x80;
        AssertRootRejected(wrongHash, "content hash");
    }

    [Fact]
    public void TerrainCooker_CooksIndexedSourceAndReusesByteIdenticalArtifacts()
    {
        using var fixture = TerrainDiskFixture.Create();
        AssetDatabase database = fixture.CreateDatabase();
        CookedTerrainRootArtifact first = TerrainRootAssetCooker.Cook(
            database,
            new AssetRef<TerrainRootSourceAsset>(s_RootGuid, TerrainAssetTypes.Root, PackageId));
        DateTime preservedTimestamp = new(2024, 1, 2, 3, 4, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(first.Path, preservedTimestamp);

        CookedTerrainRootArtifact second = TerrainRootAssetCooker.Cook(
            database,
            new AssetRef<TerrainRootSourceAsset>(s_RootGuid, TerrainAssetTypes.Root, PackageId));

        Assert.Equal(File.ReadAllBytes(first.Path), File.ReadAllBytes(second.Path));
        Assert.Equal(preservedTimestamp, File.GetLastWriteTimeUtc(second.Path));
        Assert.Equal(4, second.TileCount);
        Assert.Equal(10, second.Dependencies.Count);
        Assert.True(
            TerrainRootAssetCooker.TryLoadCooked(
                database,
                new AssetRef<TerrainRootSourceAsset>(s_RootGuid, TerrainAssetTypes.Root, PackageId),
                out CookedTerrainRoot loaded,
                out string diagnostic),
            diagnostic);
        Assert.Equal(4, loaded.Tiles.Count);
        Assert.All(
            loaded.Tiles,
            tile => Assert.True(database.TryGetCookedArtifact(
                tile.Guid,
                TerrainTileAssetCooker.RuntimeVariant,
                out _)));
    }

    [Fact]
    public void TerrainRuntimeCooker_RegistersRootAndGeneratedTileVariants()
    {
        using var fixture = TerrainDiskFixture.Create();
        AssetDatabase database = fixture.CreateDatabase();
        var cooker = new TerrainRuntimeAssetCooker(database);
        var registry = new RuntimeAssetCookerRegistry();
        registry.RegisterCooker(cooker);

        Assert.True(registry.TryGetCooker(TerrainAssetTypes.Root, out IRuntimeAssetCooker rootCooker));
        Assert.Same(cooker, rootCooker);
        Assert.True(registry.TryGetCooker(TerrainAssetTypes.Tile, out IRuntimeAssetCooker tileCooker));
        Assert.Same(cooker, tileCooker);

        var context = new RuntimeAssetCookContext(
            fixture.WorkspaceRoot,
            "Development",
            "Debug",
            "win-x64",
            Path.Combine(fixture.WorkspaceRoot, "Staging"),
            ForceRebuild: false);
        RuntimeAssetCookerOutput rootOutput = cooker.Cook(
            context,
            new RuntimeAssetCookRequest(
                s_RootGuid,
                PackageId,
                TerrainAssetTypes.Root));
        Assert.Equal(TerrainRootAssetCooker.RuntimeVariant, rootOutput.Artifact.Variant);
        Assert.Equal(10, rootOutput.Dependencies.Count);

        Guid tileGuid = TerrainTileIdentity.CreateGuid(
            s_RootGuid,
            PackageId,
            new TerrainTileCoordinate(-1, -1));
        RuntimeAssetCookerOutput tileOutput = cooker.Cook(
            context,
            new RuntimeAssetCookRequest(
                tileGuid,
                PackageId,
                TerrainAssetTypes.Tile,
                TerrainTileAssetCooker.RuntimeVariant));
        Assert.Equal(TerrainTileAssetCooker.RuntimeVariant, tileOutput.Artifact.Variant);
        Assert.Empty(tileOutput.Dependencies);
        Assert.Throws<InvalidOperationException>(() => cooker.Cook(
            context,
            new RuntimeAssetCookRequest(
                tileGuid,
                PackageId,
                TerrainAssetTypes.Tile,
                "runtime.terrain-tile.invalid")));
    }

    [Fact]
    public void TerrainRuntimeCooker_ClosesAndReconcilesShrunkDeployment()
    {
        using var fixture = TerrainDiskFixture.Create();
        AssetDatabase database = fixture.CreateDatabase();
        var registry = new RuntimeAssetCookerRegistry();
        registry.RegisterCooker(new TerrainRuntimeAssetCooker(database));
        registry.RegisterCooker(new DeterministicTextureCooker(fixture.Root));
        var context = new RuntimeAssetCookContext(
            fixture.WorkspaceRoot,
            "Production",
            "Release",
            "win-x64",
            Path.Combine(fixture.WorkspaceRoot, "Staging"),
            ForceRebuild: false);
        RuntimeAssetCookRootRequest[] roots =
        [
            new RuntimeAssetCookRootRequest(
                "terrain",
                s_RootGuid,
                PackageId,
                TerrainAssetTypes.Root)
        ];

        RuntimeAssetCookResult first = RuntimeAssetCookCoordinator.Cook(
            context,
            roots,
            registry);
        RuntimeAssetCookResult repeated = RuntimeAssetCookCoordinator.Cook(
            context,
            roots.Reverse(),
            registry);

        Assert.Equal(first.Catalog.Serialize(), repeated.Catalog.Serialize());
        AssertTerrainClosure(first.Catalog, expectedTileCount: 4);
        Guid retainedTileGuid = TerrainTileIdentity.CreateGuid(
            s_RootGuid,
            PackageId,
            new TerrainTileCoordinate(-1, -1));
        Guid[] staleTileGuids = first.Catalog.Artifacts
            .Where(artifact => artifact.AssetType == TerrainAssetTypes.Tile)
            .Select(artifact => artifact.Guid)
            .Where(guid => guid != retainedTileGuid)
            .ToArray();
        Assert.Equal(3, staleTileGuids.Length);
        Dictionary<Guid, string> staleCachePaths = staleTileGuids.ToDictionary(
            guid => guid,
            guid =>
            {
                Assert.True(database.TryGetCookedArtifact(
                    guid,
                    TerrainTileAssetCooker.RuntimeVariant,
                    out CookedAssetRecord staleTile));
                return staleTile.Path;
            });
        Assert.True(database.TryGetCookedArtifact(
            retainedTileGuid,
            TerrainTileAssetCooker.RuntimeVariant,
            out CookedAssetRecord retainedTile));
        DateTime retainedTimestamp = new(2024, 4, 5, 6, 7, 8, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(retainedTile.Path, retainedTimestamp);

        string outputRoot = Path.Combine(fixture.Root, "Player");
        RuntimeAssetDeploymentResult firstDeployment = RuntimeAssetDeployment.Deploy(
            first,
            outputRoot);
        first.Catalog.ValidateDeployment(firstDeployment.ContentRoot);

        fixture.RewriteAsSingleTile();
        RuntimeAssetCookResult shrunk = RuntimeAssetCookCoordinator.Cook(
            context,
            roots,
            registry);
        AssertTerrainClosure(shrunk.Catalog, expectedTileCount: 1);
        Assert.True(database.TryGetCookedArtifact(
            retainedTileGuid,
            TerrainTileAssetCooker.RuntimeVariant,
            out CookedAssetRecord recookedRetainedTile));
        Assert.Equal(retainedTimestamp, File.GetLastWriteTimeUtc(recookedRetainedTile.Path));
        foreach (Guid staleTileGuid in staleTileGuids)
        {
            Assert.False(database.TryGetCookedArtifact(
                staleTileGuid,
                TerrainTileAssetCooker.RuntimeVariant,
                out _));
            Assert.False(File.Exists(staleCachePaths[staleTileGuid]));
        }

        AssetDatabase reopenedDatabase = fixture.CreateDatabase();
        foreach (Guid staleTileGuid in staleTileGuids)
        {
            Assert.False(reopenedDatabase.TryGetCookedArtifact(
                staleTileGuid,
                TerrainTileAssetCooker.RuntimeVariant,
                out _));
        }

        RuntimeAssetDeploymentResult shrunkDeployment = RuntimeAssetDeployment.Deploy(
            shrunk,
            outputRoot);
        RuntimeAssetCatalog deployedCatalog = RuntimeAssetCatalog.Parse(
            File.ReadAllBytes(shrunkDeployment.CatalogPath));
        AssertTerrainClosure(deployedCatalog, expectedTileCount: 1);
        deployedCatalog.ValidateDeployment(shrunkDeployment.ContentRoot);
        foreach (Guid staleTileGuid in staleTileGuids)
        {
            Assert.DoesNotContain(
                deployedCatalog.Artifacts,
                artifact => artifact.Guid == staleTileGuid);
            string staleRelativePath = first.Catalog.Artifacts.Single(
                artifact => artifact.Guid == staleTileGuid).OutputRelativePath;
            Assert.False(File.Exists(Path.Combine(
                shrunkDeployment.ContentRoot,
                staleRelativePath.Replace('/', Path.DirectorySeparatorChar))));
        }
    }

    [Fact]
    public void TerrainRuntimeCooker_ReportsMissingTextureDependencyChain()
    {
        using var fixture = TerrainDiskFixture.Create();
        AssetDatabase database = fixture.CreateDatabase();
        var registry = new RuntimeAssetCookerRegistry();
        registry.RegisterCooker(new TerrainRuntimeAssetCooker(database));
        var context = new RuntimeAssetCookContext(
            fixture.WorkspaceRoot,
            "Production",
            "Release",
            "win-x64",
            Path.Combine(fixture.WorkspaceRoot, "Staging"),
            ForceRebuild: false);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            RuntimeAssetCookCoordinator.Cook(
                context,
                [
                    new RuntimeAssetCookRootRequest(
                        "terrain",
                        s_RootGuid,
                        PackageId,
                        TerrainAssetTypes.Root)
                ],
                registry));

        Assert.Contains("root 'terrain'", error.Message, StringComparison.Ordinal);
        Assert.Contains(s_RootGuid.ToString("D"), error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Texture2D", error.Message, StringComparison.Ordinal);
        Assert.Contains("No package-owned cooker", error.Message, StringComparison.Ordinal);
    }

    private static void AssertTerrainClosure(
        RuntimeAssetCatalog catalog,
        int expectedTileCount)
    {
        RuntimeAssetCatalogArtifact root = Assert.Single(
            catalog.Artifacts,
            artifact => artifact.Guid == s_RootGuid &&
                        artifact.AssetType == TerrainAssetTypes.Root);
        RuntimeAssetCatalogDependency[] tileDependencies = root.Dependencies
            .Where(dependency => dependency.AssetType == TerrainAssetTypes.Tile)
            .ToArray();
        RuntimeAssetCatalogDependency[] textureDependencies = root.Dependencies
            .Where(dependency => dependency.AssetType == "Texture2D")
            .ToArray();
        Assert.Equal(expectedTileCount, tileDependencies.Length);
        Assert.Equal(6, textureDependencies.Length);
        Assert.All(root.Dependencies, dependency => Assert.True(dependency.Required));
        Assert.Equal(
            expectedTileCount,
            catalog.Artifacts.Count(artifact => artifact.AssetType == TerrainAssetTypes.Tile));
        Assert.Equal(
            6,
            catalog.Artifacts.Count(artifact => artifact.AssetType == "Texture2D"));
        foreach (RuntimeAssetCatalogDependency dependency in root.Dependencies)
        {
            Assert.True(catalog.TryGetArtifact(
                dependency.Guid,
                dependency.Variant,
                out RuntimeAssetCatalogArtifact resolved));
            Assert.Equal(dependency.PackageId, resolved.PackageId);
            Assert.Equal(dependency.AssetType, resolved.AssetType);
        }
    }

    private static TerrainCookedFixture CreateCookedFixture()
    {
        TerrainGeneratedTileRecord[] records = TerrainTileIdentity.CreateRecords(
            s_RootGuid,
            PackageId,
            new TerrainTileCoordinate(-1, -1),
            tileCountX: 2,
            tileCountZ: 2);
        var root = new TerrainRootSourceDescriptor(
            s_RootGuid,
            PackageId,
            TerrainRootSourceAssetLoader.CurrentSourceSchemaVersion,
            "Cooked Terrain Fixture",
            new WorldPosition(-256.0, -32.0, 128.0),
            new TerrainSampleSpacing(2.0, 4.0),
            new TerrainHeightRange(-64.0, 192.0),
            new TerrainHeightSourceDescriptor(
                "Height/Fixture.pgm",
                "fixture.pgm",
                TerrainHeightSourceFormat.Pgm16BigEndianScalar,
                5,
                5),
            3,
            TerrainBorderPolicy.SharedEdgeSamples,
            new TerrainTileCoordinate(-1, -1),
            new AssetRef<TerrainLayerSetSourceAsset>(
                s_LayerSetGuid,
                TerrainAssetTypes.LayerSet,
                PackageId),
            records);
        TerrainLayerSetSourceDescriptor layerSet = CreateLayerSetDescriptor();
        ushort[] samples =
        [
            0, 4_000, 8_000, 12_000, 16_000,
            2_000, 18_000, 10_000, 26_000, 18_000,
            4_000, 8_000, 32_000, 16_000, 20_000,
            6_000, 22_000, 14_000, 30_000, 22_000,
            8_000, 12_000, 16_000, 20_000, 24_000
        ];
        var heightField = new TerrainHeightField(5, 5, samples);
        var tiles = new CookedTerrainTile[records.Length];
        var artifacts = new CookedTerrainTileArtifact[records.Length];
        for (int index = 0; index < records.Length; index++)
        {
            CookedTerrainTile tile = TerrainTileAssetCooker.BuildTile(
                root,
                layerSet,
                heightField,
                records[index]);
            byte[] bytes = TerrainTileAssetCooker.WritePayload(tile);
            tiles[index] = tile;
            artifacts[index] = new CookedTerrainTileArtifact(
                tile.Guid,
                tile.RootGuid,
                tile.Coordinate,
                TerrainTileAssetCooker.RuntimeVariant,
                $"tile-{index}.ariterraintile",
                bytes.Length,
                tile.MinHeight,
                tile.MaxHeight,
                SHA256.HashData(bytes));
        }

        CookedTerrainRoot cookedRoot = TerrainRootAssetCooker.BuildRoot(root, layerSet, artifacts);
        return new TerrainCookedFixture(root, layerSet, heightField, tiles, cookedRoot);
    }

    private static TerrainLayerSetSourceDescriptor CreateLayerSetDescriptor()
    {
        return new TerrainLayerSetSourceDescriptor(
            s_LayerSetGuid,
            PackageId,
            TerrainLayerSetSourceAssetLoader.CurrentSourceSchemaVersion,
            "Fixture Layers",
            [
                new TerrainLayerDescriptor(
                    "rock",
                    Texture("aaaaaaaa-0000-0000-0000-000000000001"),
                    Texture("aaaaaaaa-0000-0000-0000-000000000002"),
                    Texture("aaaaaaaa-0000-0000-0000-000000000003"),
                    TerrainLayerTint.White,
                    1.0f,
                    0.0f,
                    1.0f,
                    TerrainLayerWorldTiling.Default),
                new TerrainLayerDescriptor(
                    "grass",
                    Texture("bbbbbbbb-0000-0000-0000-000000000001"),
                    Texture("bbbbbbbb-0000-0000-0000-000000000002"),
                    Texture("bbbbbbbb-0000-0000-0000-000000000003"),
                    new TerrainLayerTint(0.8f, 0.95f, 0.75f, 1.0f),
                    0.85f,
                    0.0f,
                    1.25f,
                    new TerrainLayerWorldTiling(1.5f, 2.0f))
            ]);
    }

    private static AssetRef<Texture2DSourceAsset> Texture(string guid)
    {
        return new AssetRef<Texture2DSourceAsset>(
            Guid.Parse(guid),
            "Texture2D",
            "com.arisen.textures");
    }

    private static void AssertTileRejected(
        TerrainCookedFixture fixture,
        CookedTerrainTile tile,
        byte[] bytes,
        string expectedDiagnostic)
    {
        Assert.False(
            TerrainTileAssetCooker.TryReadPayload(
                tile.Guid,
                fixture.RootSource.Guid,
                fixture.LayerSet.Guid,
                PackageId,
                bytes,
                "corrupt-tile",
                out _,
                out string diagnostic));
        Assert.Contains(expectedDiagnostic, diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertRootRejected(byte[] bytes, string expectedDiagnostic)
    {
        Assert.False(
            TerrainRootAssetCooker.TryReadPayload(
                s_RootGuid,
                PackageId,
                bytes,
                "corrupt-root",
                out _,
                out string diagnostic));
        Assert.Contains(expectedDiagnostic, diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static int FindSectionDescriptor(
        byte[] bytes,
        int headerSize,
        int sectionCountOffset,
        uint type)
    {
        int sectionCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(sectionCountOffset));
        for (int index = 0; index < sectionCount; index++)
        {
            int offset = headerSize + (index * TerrainCookedContainer.SectionDirectoryEntrySize);
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) == type)
            {
                return offset;
            }
        }

        throw new InvalidOperationException($"Section '{type}' was not found.");
    }

    private static ulong ReadSectionOffset(
        byte[] bytes,
        int headerSize,
        int sectionCountOffset,
        uint type)
    {
        int descriptor = FindSectionDescriptor(bytes, headerSize, sectionCountOffset, type);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(descriptor + 8));
    }

    private static void RehashTile(byte[] bytes)
    {
        SHA256.HashData(bytes.AsSpan(TerrainTileAssetCooker.HeaderSize)).CopyTo(
            bytes.AsSpan(TerrainTileAssetCooker.HashOffset, TerrainCookedContainer.HashSize));
    }

    private static void RehashRoot(byte[] bytes)
    {
        SHA256.HashData(bytes.AsSpan(TerrainRootAssetCooker.HeaderSize)).CopyTo(
            bytes.AsSpan(TerrainRootAssetCooker.HashOffset, TerrainCookedContainer.HashSize));
    }

    private sealed record TerrainCookedFixture(
        TerrainRootSourceDescriptor RootSource,
        TerrainLayerSetSourceDescriptor LayerSet,
        TerrainHeightField HeightField,
        CookedTerrainTile[] Tiles,
        CookedTerrainRoot Root);

    private sealed class DeterministicTextureCooker : IRuntimeAssetCooker
    {
        private const string RuntimeVariant = "fixture.texture.v1";
        private readonly string m_OutputRoot;

        public DeterministicTextureCooker(string outputRoot)
        {
            m_OutputRoot = outputRoot;
        }

        public string ProviderId => "com.arisen.test.texture-cooker";

        public IReadOnlyCollection<string> AssetTypes { get; } = ["Texture2D"];

        public RuntimeAssetCookerOutput Cook(
            RuntimeAssetCookContext context,
            RuntimeAssetCookRequest request)
        {
            ArgumentNullException.ThrowIfNull(context);
            string outputVariant = request.Variant.Length == 0
                ? RuntimeVariant
                : request.Variant;
            if (!string.Equals(request.AssetType, "Texture2D", StringComparison.Ordinal) ||
                (request.Variant.Length > 0 &&
                 !string.Equals(request.Variant, RuntimeVariant, StringComparison.Ordinal) &&
                 !string.Equals(request.Variant, TerrainTextureCookVariants.Albedo, StringComparison.Ordinal) &&
                 !string.Equals(request.Variant, TerrainTextureCookVariants.Normal, StringComparison.Ordinal) &&
                 !string.Equals(request.Variant, TerrainTextureCookVariants.Orm, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Unsupported fixture texture request '{request.AssetType}:{request.Variant}'.");
            }

            byte[] payload = Encoding.UTF8.GetBytes(
                $"{request.Guid:N}|{request.PackageId}|{outputVariant}");
            string sourcePath = Path.Combine(
                m_OutputRoot,
                "TextureCook",
                request.Guid.ToString("N") + ".bin");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllBytes(sourcePath, payload);
            return RuntimeAssetCookerOutput.FromFile(
                request,
                outputVariant,
                $"{request.PackageId}/{request.Guid:N}/{outputVariant}.aritex",
                sourcePath,
                formatVersion: 1);
        }
    }

    private sealed class TerrainDiskFixture : IDisposable
    {
        private TerrainDiskFixture(string root)
        {
            Root = root;
            WorkspaceRoot = Path.Combine(root, "Workspace");
            PackageRoot = Path.Combine(root, "Package");
            Directory.CreateDirectory(WorkspaceRoot);
            WriteSources();
        }

        public string Root { get; }

        public string WorkspaceRoot { get; }

        public string PackageRoot { get; }

        public static TerrainDiskFixture Create()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "ArisenTerrainCookedTests",
                Guid.NewGuid().ToString("N"));
            return new TerrainDiskFixture(root);
        }

        public AssetDatabase CreateDatabase()
        {
            var database = new AssetDatabase();
            database.InitializeWorkspace(
                WorkspaceRoot,
                [(PackageId, PackageRoot)],
                AssetSourceAccessMode.RuntimeAssetCook);
            return database;
        }

        public void RewriteAsSingleTile()
        {
            ushort[] source = CreateCookedFixture().HeightField.Samples.ToArray();
            ushort[] singleTile =
            [
                source[0], source[1], source[2],
                source[5], source[6], source[7],
                source[10], source[11], source[12]
            ];
            WriteHeightSource(3, 3, singleTile);
            WriteRootSource(tileCountX: 1, tileCountZ: 1);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private void WriteSources()
        {
            string assets = Path.Combine(PackageRoot, "Assets");
            ushort[] samples = CreateCookedFixture().HeightField.Samples.ToArray();
            WriteHeightSource(5, 5, samples);
            WriteRootSource(tileCountX: 2, tileCountZ: 2);

            string layerPath = Path.Combine(assets, "Fixture.ariterrainlayers");
            File.WriteAllText(
                layerPath,
                $$"""
                Version: 1
                LayerSetGuid: {{s_LayerSetGuid:D}}
                Name: Fixture Layers
                Layers:
                - Id: Rock
                  Albedo: { Guid: aaaaaaaa-0000-0000-0000-000000000001, PackageId: com.arisen.textures }
                  Normal: { Guid: aaaaaaaa-0000-0000-0000-000000000002, PackageId: com.arisen.textures }
                  Orm: { Guid: aaaaaaaa-0000-0000-0000-000000000003, PackageId: com.arisen.textures }
                - Id: Grass
                  Albedo: { Guid: bbbbbbbb-0000-0000-0000-000000000001, PackageId: com.arisen.textures }
                  Normal: { Guid: bbbbbbbb-0000-0000-0000-000000000002, PackageId: com.arisen.textures }
                  Orm: { Guid: bbbbbbbb-0000-0000-0000-000000000003, PackageId: com.arisen.textures }
                """);
            SerializationUtil.Serialize(
                new AssetMetadata
                {
                    Guid = s_LayerSetGuid,
                    AssetType = TerrainAssetTypes.LayerSet,
                    Importer = "ArisenTerrainLayerSetImporter"
                },
                layerPath + ".meta");
        }

        private void WriteHeightSource(
            int width,
            int height,
            IReadOnlyList<ushort> samples)
        {
            string heightPath = Path.Combine(
                PackageRoot,
                "Assets",
                "Height",
                "Fixture.pgm");
            Directory.CreateDirectory(Path.GetDirectoryName(heightPath)!);
            File.WriteAllBytes(heightPath, CreatePgm(width, height, samples));
        }

        private void WriteRootSource(int tileCountX, int tileCountZ)
        {
            string assets = Path.Combine(PackageRoot, "Assets");
            TerrainGeneratedTileRecord[] records = TerrainTileIdentity.CreateRecords(
                s_RootGuid,
                PackageId,
                new TerrainTileCoordinate(-1, -1),
                tileCountX,
                tileCountZ);
            string generatedTiles = string.Join(
                Environment.NewLine,
                records.Select(record =>
                    $"- Coordinate: {{ X: {record.Coordinate.X}, Z: {record.Coordinate.Z} }}{Environment.NewLine}" +
                    $"  Guid: {record.Guid:D}"));
            string rootPath = Path.Combine(assets, "Fixture.aristerrain");
            File.WriteAllText(
                rootPath,
                $$"""
                Version: 1
                TerrainGuid: {{s_RootGuid:D}}
                Name: Cooked Terrain Fixture
                WorldPlacement: { X: -256.0, Y: -32.0, Z: 128.0 }
                SampleSpacing: { X: 2.0, Z: 4.0 }
                HeightRange: { Min: -64.0, Max: 192.0 }
                HeightSource:
                  Path: Height/Fixture.pgm
                  Format: Pgm16BigEndianScalar
                TileResolution: 3
                BorderPolicy: SharedEdgeSamples
                TileOrigin: { X: -1, Z: -1 }
                LayerSet:
                  Guid: {{s_LayerSetGuid:D}}
                  PackageId: {{PackageId}}
                GeneratedTiles:
                {{generatedTiles}}
                """);
            SerializationUtil.Serialize(
                new AssetMetadata
                {
                    Guid = s_RootGuid,
                    AssetType = TerrainAssetTypes.Root,
                    Importer = "ArisenTerrainImporter"
                },
                rootPath + ".meta");
        }

        private static byte[] CreatePgm(
            int width,
            int height,
            IReadOnlyList<ushort> samples)
        {
            byte[] header = Encoding.ASCII.GetBytes($"P5\n{width} {height}\n65535\n");
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
    }
}
