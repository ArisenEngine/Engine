using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

[Collection(SceneComponentExtensionRegistryCollection.Name)]
public sealed class TerrainSceneComponentTests : IDisposable
{
    private readonly TerrainTileSceneComponentCodec m_Codec = new();

    public TerrainSceneComponentTests()
    {
        SceneComponentExtensionRegistry.Shared.Register(m_Codec);
    }

    public void Dispose()
    {
        SceneComponentExtensionRegistry.Shared.Unregister(m_Codec);
    }

    [Fact]
    public void TerrainTileComponent_RoundTripsSourceAndCookedOnlyWithExplicitVariants()
    {
        using var fixture = new TerrainSceneFixture();
        SceneFixture scene = fixture.AddScene("TerrainCell");
        var sourceWorld = new EntityManager();

        SceneLoadResult sourceLoad = SceneAssetLoader.LoadScene(
            fixture.Database,
            scene.Reference,
            sourceWorld);

        Assert.True(sourceLoad.Success, sourceLoad.Diagnostic);
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<TerrainTileComponent>());
        Assert.True(scene.TryGetComponent(sourceWorld, sourceLoad, out TerrainTileComponent source));
        AssertComponent(source, fixture);

        TerrainRootAssetCooker.Cook(fixture.Database, fixture.RootReference);
        CookedSceneArtifact first = SceneAssetCooker.Cook(fixture.Database, scene.Reference);
        byte[] firstBytes = File.ReadAllBytes(first.Path);
        CookedSceneArtifact second = SceneAssetCooker.Cook(fixture.Database, scene.Reference);
        Assert.Equal(firstBytes, File.ReadAllBytes(second.Path));
        Assert.Contains(first.Dependencies, dependency =>
            dependency.Guid == fixture.RootGuid &&
            dependency.Variant == TerrainRootAssetCooker.RuntimeVariant &&
            dependency.Required);
        Assert.Contains(first.Dependencies, dependency =>
            dependency.Guid == fixture.TileGuid &&
            dependency.Variant == TerrainTileAssetCooker.RuntimeVariant &&
            dependency.Required);

        fixture.Database.UseReadOnlyRuntime();
        var cookedWorld = new EntityManager();
        SceneLoadResult cookedLoad = SceneAssetCooker.LoadCooked(
            fixture.Database,
            scene.Reference,
            cookedWorld);

        Assert.True(cookedLoad.Success, cookedLoad.Diagnostic);
        Assert.True(scene.TryGetComponent(cookedWorld, cookedLoad, out TerrainTileComponent cooked));
        Assert.Equal(source, cooked);
    }

    [Fact]
    public void CookedTerrainComponent_RejectsMissingRequiredProviderCodec()
    {
        using var fixture = new TerrainSceneFixture();
        SceneFixture scene = fixture.AddScene("MissingTerrainCodec");
        TerrainRootAssetCooker.Cook(fixture.Database, fixture.RootReference);
        SceneAssetCooker.Cook(fixture.Database, scene.Reference);
        fixture.Database.UseReadOnlyRuntime();
        Assert.True(SceneComponentExtensionRegistry.Shared.Unregister(m_Codec));

        try
        {
            SceneLoadResult result = SceneAssetCooker.LoadCooked(
                fixture.Database,
                scene.Reference,
                new EntityManager());

            Assert.False(result.Success);
            Assert.Contains(
                $"required component TypeId '{TerrainTileSceneComponentCodec.TypeId}' is unknown",
                result.Diagnostic,
                StringComparison.Ordinal);
        }
        finally
        {
            SceneComponentExtensionRegistry.Shared.Register(m_Codec);
        }
    }

    [Fact]
    public void TerrainTileComponent_RejectsDuplicateOwnershipAndInvalidRootPairing()
    {
        using var fixture = new TerrainSceneFixture();
        SceneFixture duplicate = fixture.AddScene("DuplicateTerrain", entityCount: 2);
        SceneLoadResult duplicateLoad = SceneAssetLoader.LoadScene(
            fixture.Database,
            duplicate.Reference,
            new EntityManager());

        Assert.False(duplicateLoad.Success);
        Assert.Contains("exclusive identity", duplicateLoad.Diagnostic, StringComparison.Ordinal);

        SceneFixture mismatched = fixture.AddScene(
            "MismatchedTerrain",
            coordinateX: 1);
        SceneLoadResult mismatchedLoad = SceneAssetLoader.LoadScene(
            fixture.Database,
            mismatched.Reference,
            new EntityManager());

        Assert.False(mismatchedLoad.Success);
        Assert.Contains("does not belong to root coordinate", mismatchedLoad.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void TerrainTileOwnership_FollowsWorldCellUnloadReloadAndOriginPlacement()
    {
        using var fixture = new TerrainSceneFixture();
        SceneFixture firstScene = fixture.AddScene("FirstTerrainCell");
        SceneFixture secondScene = fixture.AddScene("SecondTerrainCell");
        SceneStagingData firstStaging = fixture.BuildStaging(firstScene);
        SceneStagingData secondStaging = fixture.BuildStaging(secondScene);
        WorldPosition tileOrigin = fixture.WorldPlacement;
        WorldPosition firstRenderOrigin = new(0.0, 0.0, 0.0);
        SceneStagingData firstPlaced = SceneStagingPlacement.PlaceCell(
            firstStaging,
            tileOrigin,
            firstRenderOrigin);
        var world = new EntityManager();
        var service = new RuntimeSceneService(fixture.Database, world);
        var firstCell = new WorldCellId(Guid.Parse("71000000-0000-0000-0000-000000000001"));
        var secondCell = new WorldCellId(Guid.Parse("71000000-0000-0000-0000-000000000002"));

        var first = service.ActivatePreparedAdditiveAtFrameBoundary(
            firstScene.Reference,
            firstPlaced,
            "test-source",
            firstCell);

        Assert.True(first.Result.Success, first.Result.Diagnostic);
        Assert.True(service.TryResolveEntity(first.InstanceId, firstScene.EntityGuid, out Entity firstEntity));
        Assert.True(service.TryGetEntityWorldCellOwner(firstEntity, out WorldCellId firstOwner));
        Assert.Equal(firstCell, firstOwner);
        Assert.Equal(
            new System.Numerics.Vector3(10.0f, 20.0f, 30.0f),
            world.GetComponent<TransformComponent>(firstEntity).Position);
        TerrainTileComponent firstComponent = world.GetComponent<TerrainTileComponent>(firstEntity);
        Assert.Equal(fixture.WorldPlacement, firstComponent.WorldPlacement);

        var duplicate = service.ActivatePreparedAdditiveAtFrameBoundary(
            secondScene.Reference,
            SceneStagingPlacement.PlaceCell(secondStaging, tileOrigin, firstRenderOrigin),
            "test-source",
            secondCell);
        Assert.False(duplicate.Result.Success);
        Assert.Contains("already active", duplicate.Result.Diagnostic, StringComparison.Ordinal);

        Assert.True(service.UnloadSceneAtFrameBoundary(first.InstanceId, out string unloadDiagnostic), unloadDiagnostic);
        WorldPosition rebasedOrigin = new(8.0, 16.0, 24.0);
        var reloaded = service.ActivatePreparedAdditiveAtFrameBoundary(
            secondScene.Reference,
            SceneStagingPlacement.PlaceCell(secondStaging, tileOrigin, rebasedOrigin),
            "test-source",
            secondCell);

        Assert.True(reloaded.Result.Success, reloaded.Result.Diagnostic);
        Assert.True(service.TryResolveEntity(
            reloaded.InstanceId,
            secondScene.EntityGuid,
            out Entity reloadedEntity));
        Assert.Equal(
            new System.Numerics.Vector3(2.0f, 4.0f, 6.0f),
            world.GetComponent<TransformComponent>(reloadedEntity).Position);
        TerrainTileComponent reloadedComponent =
            world.GetComponent<TerrainTileComponent>(reloadedEntity);
        Assert.Equal(firstComponent.TileGuid, reloadedComponent.TileGuid);
        Assert.Equal(firstComponent.WorldPlacement, reloadedComponent.WorldPlacement);
        Assert.True(service.TryGetEntityWorldCellOwner(reloadedEntity, out WorldCellId reloadedOwner));
        Assert.Equal(secondCell, reloadedOwner);
    }

    [Fact]
    public void TerrainTileBorderOwnership_IsHalfOpenAcrossPositiveNeighbors()
    {
        Guid positiveX = Guid.Parse("81000000-0000-0000-0000-000000000001");
        Guid positiveZ = Guid.Parse("81000000-0000-0000-0000-000000000002");
        var interiorNeighbors = new TerrainTileNeighborSet(
            Guid.Empty,
            positiveX,
            Guid.Empty,
            positiveZ);

        Assert.True(TerrainTileBorderOwnership.OwnsSample(0, 0, 3, interiorNeighbors));
        Assert.False(TerrainTileBorderOwnership.OwnsSample(2, 1, 3, interiorNeighbors));
        Assert.False(TerrainTileBorderOwnership.OwnsSample(1, 2, 3, interiorNeighbors));
        Assert.False(TerrainTileBorderOwnership.OwnsSample(2, 2, 3, interiorNeighbors));
        Assert.True(TerrainTileBorderOwnership.OwnsSample(
            2,
            2,
            3,
            new TerrainTileNeighborSet(Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty)));
    }

    private static void AssertComponent(
        TerrainTileComponent component,
        TerrainSceneFixture fixture)
    {
        Assert.Equal(fixture.RootGuid, component.TerrainRootGuid);
        Assert.Equal(fixture.TileGuid, component.TileGuid);
        Assert.Equal(fixture.LayerSetGuid, component.LayerSetGuid);
        Assert.Equal(0, component.TileX);
        Assert.Equal(0, component.TileZ);
        Assert.Equal(fixture.WorldPlacement, component.WorldPlacement);
        Assert.Equal(
            TerrainTileFlags.Visible |
            TerrainTileFlags.CastShadows |
            TerrainTileFlags.ReceiveShadows,
            component.Flags);
    }

    private sealed class TerrainSceneFixture : IDisposable
    {
        private const string PackageId = "com.arisen.test";
        private readonly string m_Root;
        private int m_SceneIndex;

        public TerrainSceneFixture()
        {
            m_Root = Path.Combine(
                Path.GetTempPath(),
                "ArisenTerrainSceneComponentTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Root);
            Database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(m_Root, "Cooked"));
            RootGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
            LayerSetGuid = Guid.Parse("22222222-3333-4444-5555-666666666666");
            TileGuid = TerrainTileIdentity.CreateGuid(
                RootGuid,
                PackageId,
                new TerrainTileCoordinate(0, 0));
            WorldPlacement = new WorldPosition(10.0, 20.0, 30.0);

            string heightPath = WritePgm("Height.pgm", 3, 3);
            string layerPath = WriteText("Valley.ariterrainlayers", $$"""
                Version: 1
                LayerSetGuid: {{LayerSetGuid:D}}
                Name: Valley Layers
                Layers:
                - Id: Ground
                  Albedo: { Guid: aaaaaaaa-0000-0000-0000-000000000001, PackageId: com.arisen.textures }
                  Normal: { Guid: aaaaaaaa-0000-0000-0000-000000000002, PackageId: com.arisen.textures }
                  Orm: { Guid: aaaaaaaa-0000-0000-0000-000000000003, PackageId: com.arisen.textures }
                """);
            string rootPath = WriteText("Valley.aristerrain", $$"""
                Version: 1
                TerrainGuid: {{RootGuid:D}}
                Name: Valley
                WorldPlacement: { X: 10, Y: 20, Z: 30 }
                SampleSpacing: { X: 2, Z: 2 }
                HeightRange: { Min: -10, Max: 50 }
                HeightSource:
                  Path: {{Path.GetFileName(heightPath)}}
                  Format: Pgm16BigEndianScalar
                TileResolution: 3
                BorderPolicy: SharedEdgeSamples
                TileOrigin: { X: 0, Z: 0 }
                LayerSet: { Guid: {{LayerSetGuid:D}}, PackageId: {{PackageId}} }
                GeneratedTiles:
                - Coordinate: { X: 0, Z: 0 }
                  Guid: {{TileGuid:D}}
                """);
            Database.AddAsset(RootGuid, TerrainAssetTypes.Root, rootPath, PackageId);
            Database.AddAsset(LayerSetGuid, TerrainAssetTypes.LayerSet, layerPath, PackageId);
            Database.AddAsset(
                TileGuid,
                TerrainAssetTypes.Tile,
                Path.Combine(m_Root, "Generated.tile"),
                PackageId);
            RootReference = new AssetRef<TerrainRootSourceAsset>(
                RootGuid,
                TerrainAssetTypes.Root,
                PackageId);
        }

        public TestAssetDatabase Database { get; }
        public Guid RootGuid { get; }
        public Guid LayerSetGuid { get; }
        public Guid TileGuid { get; }
        public WorldPosition WorldPlacement { get; }
        public AssetRef<TerrainRootSourceAsset> RootReference { get; }

        public SceneFixture AddScene(
            string name,
            int entityCount = 1,
            int coordinateX = 0)
        {
            Guid sceneGuid = new(
                m_SceneIndex + 1,
                0x1234,
                0x5678,
                0x90,
                0xab,
                0xcd,
                0xef,
                0x01,
                0x23,
                0x45,
                0x67);
            m_SceneIndex++;
            var entityGuids = Enumerable.Range(0, entityCount)
                .Select(index => TerrainTileEntityIdentity.Create(
                    sceneGuid,
                    index == 0
                        ? TileGuid
                        : new Guid(TileGuid.ToByteArray().Select((value, byteIndex) =>
                            byteIndex == 15 ? (byte)(value ^ index) : value).ToArray())))
                .ToArray();
            if (entityCount > 1)
            {
                entityGuids[1] = TerrainTileEntityIdentity.Create(
                    sceneGuid,
                    Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
            }

            string source = CreateSceneSource(name, entityGuids, coordinateX);
            string path = WriteText($"{name}.arisenscene", source);
            Database.AddAsset(sceneGuid, "Scene", path, PackageId);
            return new SceneFixture(
                new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", PackageId),
                entityGuids[0],
                path,
                source);
        }

        public SceneStagingData BuildStaging(SceneFixture scene)
        {
            Assert.True(SceneAssetLoader.TryBuildSceneStaging(
                Database,
                scene.Reference.Guid,
                scene.SourcePath,
                scene.Source,
                out SceneStagingData staging,
                out string diagnostic), diagnostic);
            return staging;
        }

        public void Dispose()
        {
            Database.ReleaseAllLoadedCookedAssets();
            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, recursive: true);
            }
        }

        private string CreateSceneSource(
            string name,
            IReadOnlyList<Guid> entityGuids,
            int coordinateX)
        {
            var source = new StringBuilder();
            source.AppendLine("Version: 2");
            source.AppendLine($"Name: {name}");
            source.AppendLine("ComponentSchemas:");
            source.AppendLine("- TypeId: 1");
            source.AppendLine("  Name: Transform");
            source.AppendLine("  Version: 1");
            source.AppendLine("  Required: true");
            source.AppendLine($"- TypeId: {TerrainTileSceneComponentCodec.TypeId}");
            source.AppendLine("  Name: TerrainTile");
            source.AppendLine("  Version: 1");
            source.AppendLine("  Required: true");
            source.AppendLine("Entities:");
            for (int index = 0; index < entityGuids.Count; index++)
            {
                source.AppendLine($"- Guid: {entityGuids[index]:D}");
                source.AppendLine($"  Name: Terrain Tile {index}");
                source.AppendLine("  Transform:");
                source.AppendLine("    Position: { X: 0, Y: 0, Z: 0 }");
                source.AppendLine("    Rotation: { X: 0, Y: 0, Z: 0, W: 1 }");
                source.AppendLine("    Scale: { X: 1, Y: 1, Z: 1 }");
                source.AppendLine("  TerrainTile:");
                source.AppendLine($"    TerrainRoot: {{ Guid: {RootGuid:D}, PackageId: {PackageId} }}");
                source.AppendLine($"    TileGuid: {TileGuid:D}");
                source.AppendLine($"    LayerSet: {{ Guid: {LayerSetGuid:D}, PackageId: {PackageId} }}");
                source.AppendLine($"    Coordinate: {{ X: {coordinateX}, Z: 0 }}");
                source.AppendLine("    WorldPlacement: { X: 10, Y: 20, Z: 30 }");
                source.AppendLine("    Visible: true");
                source.AppendLine("    CastShadows: true");
                source.AppendLine("    ReceiveShadows: true");
                source.AppendLine("    PreferHighQuality: false");
            }
            return source.ToString();
        }

        private string WriteText(string relativePath, string contents)
        {
            string path = Path.Combine(m_Root, relativePath);
            File.WriteAllText(path, contents);
            return path;
        }

        private string WritePgm(string relativePath, int width, int height)
        {
            string path = Path.Combine(m_Root, relativePath);
            byte[] header = Encoding.ASCII.GetBytes($"P5\n{width} {height}\n65535\n");
            byte[] bytes = new byte[checked(header.Length + width * height * sizeof(ushort))];
            header.CopyTo(bytes, 0);
            for (int index = 0; index < width * height; index++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(
                    bytes.AsSpan(header.Length + index * sizeof(ushort), sizeof(ushort)),
                    checked((ushort)(index * 4_000)));
            }
            File.WriteAllBytes(path, bytes);
            return path;
        }
    }

    private sealed record SceneFixture(
        AssetRef<SceneSourceAsset> Reference,
        Guid EntityGuid,
        string SourcePath,
        string Source)
    {
        public bool TryGetComponent(
            EntityManager world,
            SceneLoadResult load,
            out TerrainTileComponent component)
        {
            if (load.AuthoringEntities != null &&
                load.AuthoringEntities.TryGetEntity(EntityGuid, out Entity entity) &&
                world.HasComponent<TerrainTileComponent>(entity))
            {
                component = world.GetComponent<TerrainTileComponent>(entity);
                return true;
            }

            component = default;
            return false;
        }
    }
}
