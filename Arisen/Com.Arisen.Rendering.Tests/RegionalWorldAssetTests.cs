using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using ArisenEngine.Vegetation.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

// Pins the authored MistfallValley regional world: one 1 km terrain root whose eight-by-eight tiles
// are split across the sixteen cell scenes that own them, and the cell-scoped scatter recipes that
// bake from that same root. The showcase valley stays a separate, byte-identical world; the regional
// world is authored beside it and must not inherit the showcase closure by accident.
[Collection(SceneComponentExtensionRegistryCollection.Name)]
public sealed class RegionalWorldAssetTests : IDisposable
{
    private const string PackageId = "com.arisen.packagegame";


    // The generator authors four cells per axis, each holding two-by-two tiles of 128 m, so the
    // single terrain root spans eight tiles per axis over one kilometre.
    private const int CellsPerAxis = 4;
    private const int TilesPerCellAxis = 2;
    private const int TilesPerAxis = CellsPerAxis * TilesPerCellAxis;
    private const int CellCount = CellsPerAxis * CellsPerAxis;
    private const int TileCount = TilesPerAxis * TilesPerAxis;
    private const int TilesPerCell = TilesPerCellAxis * TilesPerCellAxis;
    private const double CellSize = 256.0;
    private const double WorldOrigin = -512.0;

    private static readonly Guid s_WorldGuid = Guid.Parse("38637cf8-f4a1-91f0-90b5-bf63752b4b82");
    private static readonly Guid s_PersistentSceneGuid = Guid.Parse("e176b519-9d88-527e-a841-c5c5d73b7477");
    private static readonly Guid s_TerrainRootGuid = Guid.Parse("40ad1b8e-7be7-7f2c-2454-fb97874ab169");
    private static readonly Guid s_LayerSetGuid = Guid.Parse("5dcaa6bd-2b51-498d-9fa9-bd68f4642761");
    private static readonly Guid s_BiomeGuid = Guid.Parse("c0a92f10-0eb9-4d24-b729-7d0f38313001");

    // Authored biome entry order in ShowcaseValley.arivegetationbiome, which the regional recipes
    // reuse: rock, grass, shrub, tree.
    private static readonly string[] s_EntryIds = ["valley-rock", "valley-grass", "valley-shrub", "valley-tree"];

    private readonly TerrainTileSceneComponentCodec m_Codec = new();

    public RegionalWorldAssetTests()
    {
        SceneComponentExtensionRegistry.Shared.Register(m_Codec);
    }

    public void Dispose()
    {
        SceneComponentExtensionRegistry.Shared.Unregister(m_Codec);
    }

    [Fact]
    public void PackageMistfallValleyWorld_SplitsItsTilesAcrossTheCellsThatOwnThem()
    {
        string packageRoot = GetRepositoryFile(
            "Arisen", "Development", "PackageGame", "Local", PackageId);
        string cookedRoot = CreateCookedRoot("ArisenMistfallValleyWorldTests");

        try
        {
            var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, cookedRoot);
            AddRegionalTerrainAssets(db, packageRoot);
            AddRegionalWorldAssets(db, packageRoot);

            WorldDescriptorLoadResult loaded = WorldDescriptorLoader.LoadSource(
                db,
                new AssetRef<WorldSourceAsset>(s_WorldGuid, "World", PackageId));

            Assert.True(loaded.Success, loaded.Diagnostic);
            WorldDescriptor descriptor = Assert.IsType<WorldDescriptor>(loaded.Descriptor);
            Assert.Equal(s_WorldGuid, descriptor.WorldGuid);
            Assert.Equal(s_PersistentSceneGuid, descriptor.PersistentScene.Guid);
            Assert.Equal(CellCount, descriptor.Cells.Count);
            Assert.Equal(WorldOrigin, descriptor.Partition.Origin.X);
            Assert.Equal(CellSize, descriptor.Partition.CellSize.X);

            var owners = new Dictionary<TerrainTileCoordinate, Guid>();
            foreach (WorldCellDescriptor cell in descriptor.Cells)
            {
                int cellX = cell.Key.Coordinate.X;
                int cellZ = cell.Key.Coordinate.Z;
                Assert.Equal("surface", cell.Key.Layer);
                Assert.InRange(cellX, 0, CellsPerAxis - 1);
                Assert.InRange(cellZ, 0, CellsPerAxis - 1);
                Assert.Equal(WorldOrigin + (cellX * CellSize), cell.Bounds.Min.X);
                Assert.Equal(WorldOrigin + (cellZ * CellSize), cell.Bounds.Min.Z);

                AssertCellOwnsExactlyItsOwnTiles(db, cell, owners);
            }

            Assert.Equal(TileCount, owners.Count);
            for (int tileZ = 0; tileZ < TilesPerAxis; tileZ++)
            {
                for (int tileX = 0; tileX < TilesPerAxis; tileX++)
                {
                    Assert.True(
                        owners.TryGetValue(new TerrainTileCoordinate(tileX, tileZ), out Guid tileGuid),
                        $"Tile {tileX},{tileZ} is owned by no cell scene.");
                    Assert.Equal(
                        TerrainTileIdentity.CreateGuid(
                            s_TerrainRootGuid,
                            PackageId,
                            new TerrainTileCoordinate(tileX, tileZ)),
                        tileGuid);
                }
            }
        }
        finally
        {
            DeleteCookedRoot(cookedRoot);
        }
    }

    [Fact]
    public void PackageMistfallValleyPersistentScene_KeepsGlobalComponentsOutOfItsCells()
    {
        string packageRoot = GetRepositoryFile(
            "Arisen", "Development", "PackageGame", "Local", PackageId);
        string cookedRoot = CreateCookedRoot("ArisenMistfallValleyPersistentTests");

        try
        {
            var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, cookedRoot);
            AddRegionalTerrainAssets(db, packageRoot);
            AddRegionalWorldAssets(db, packageRoot);

            SceneInspectionResult persistent = SceneAssetLoader.InspectScene(
                db,
                new AssetRef<SceneSourceAsset>(s_PersistentSceneGuid, "Scene", PackageId));

            Assert.True(persistent.Success, persistent.Diagnostic);
            Assert.Equal(1, persistent.CameraCount);
            Assert.Equal(1, persistent.DirectionalLightCount);
            Assert.Equal(1, persistent.EnvironmentCount);
            Assert.Equal(0, persistent.PointLightCount);
            Assert.Equal(0, persistent.SpotLightCount);
            Assert.Equal(3, persistent.MeshRendererCount);
        }
        finally
        {
            DeleteCookedRoot(cookedRoot);
        }
    }

    [Fact]
    public void PackageMistfallValleyRecipes_AreCellScopedAndIdentityStable()
    {
        string packageRoot = GetRepositoryFile(
            "Arisen", "Development", "PackageGame", "Local", PackageId);
        string cookedRoot = CreateCookedRoot("ArisenMistfallValleyRecipeTests");

        try
        {
            var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, cookedRoot);
            AddRegionalWorldAssets(db, packageRoot);
            AddRegionalTerrainAssets(db, packageRoot);
            db.AddAsset(
                s_BiomeGuid,
                VegetationAssetTypes.Biome,
                Path.Combine(packageRoot, "Assets", "Vegetation", "ShowcaseValley.arivegetationbiome"),
                PackageId);

            string recipeDirectory = Path.Combine(packageRoot, "Assets", "Vegetation", "MistfallValley");
            string[] recipePaths = Directory.GetFiles(recipeDirectory, "*.arivegetationscatter");
            Assert.Equal(CellCount * s_EntryIds.Length, recipePaths.Length);

            var authored = new HashSet<(int CellX, int CellZ, string EntryId)>();
            foreach (string recipePath in recipePaths)
            {
                Guid recipeGuid = ReadSourceGuid(recipePath + ".meta");
                db.AddAsset(recipeGuid, VegetationAssetTypes.ScatterRecipe, recipePath, PackageId);

                VegetationScatterRecipeDescriptor recipe =
                    VegetationScatterRecipeSourceAssetLoader.LoadSource(
                        db,
                        new AssetRef<VegetationScatterRecipeSourceAsset>(
                            recipeGuid,
                            VegetationAssetTypes.ScatterRecipe,
                            PackageId));

                Assert.Equal(recipeGuid, recipe.Guid);
                Assert.Equal(s_WorldGuid, recipe.World.Guid);
                Assert.Equal(s_BiomeGuid, recipe.Biome.Guid);
                Assert.Equal(s_TerrainRootGuid, recipe.TerrainRoot.Guid);

                Assert.Contains(recipe.EntryId, s_EntryIds);
                Assert.InRange(recipe.Cell.Coordinate.X, 0, CellsPerAxis - 1);
                Assert.InRange(recipe.Cell.Coordinate.Z, 0, CellsPerAxis - 1);
                Assert.True(
                    authored.Add((recipe.Cell.Coordinate.X, recipe.Cell.Coordinate.Z, recipe.EntryId)),
                    $"Recipe '{recipePath}' duplicates another authored cell/entry pair.");
            }

            Assert.Equal(CellCount * s_EntryIds.Length, authored.Count);
        }
        finally
        {
            DeleteCookedRoot(cookedRoot);
        }
    }

    [Fact]
    public void PackageMistfallValleyRegion_BakesScatterInsideTheCellItBelongsTo()
    {
        string packageRoot = GetRepositoryFile(
            "Arisen", "Development", "PackageGame", "Local", PackageId);
        string cookedRoot = CreateCookedRoot("ArisenMistfallValleyBakeTests");

        try
        {
            var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, cookedRoot);
            AddRegionalWorldAssets(db, packageRoot);
            AddRegionalTerrainAssets(db, packageRoot);
            AddRegionalVegetationAssets(db, packageRoot);

            Guid recipeGuid = ReadSourceGuid(
                Path.Combine(
                    packageRoot,
                    "Assets",
                    "Vegetation",
                    "MistfallValley",
                    "valley-grass_cell_1_1.arivegetationscatter.meta"));
            db.AddAsset(
                recipeGuid,
                VegetationAssetTypes.ScatterRecipe,
                Path.Combine(
                    packageRoot,
                    "Assets",
                    "Vegetation",
                    "MistfallValley",
                    "valley-grass_cell_1_1.arivegetationscatter"),
                PackageId);

            VegetationScatterRecipeDescriptor recipe =
                VegetationScatterRecipeSourceAssetLoader.LoadSource(
                    db,
                    new AssetRef<VegetationScatterRecipeSourceAsset>(
                        recipeGuid,
                        VegetationAssetTypes.ScatterRecipe,
                        PackageId));
            Assert.Equal(1, recipe.Cell.Coordinate.X);
            Assert.Equal(1, recipe.Cell.Coordinate.Z);

            TerrainRootAssetCooker.Cook(db, recipe.TerrainRoot);
            VegetationBiomeSourceDescriptor biome =
                VegetationBiomeSourceAssetLoader.LoadSource(db, recipe.Biome);
            VegetationSpeciesAssetCooker.Cook(
                db,
                biome.Entries.Single(entry => entry.EntryId == recipe.EntryId).Species);
            VegetationBiomeAssetCooker.Cook(db, recipe.Biome);

            VegetationScatterBakeResult first = Bake(db, recipe);
            Assert.True(first.HasOutput, $"Regional scatter bake produced no output: {first.Metrics}");
            Assert.True(first.Metrics.AcceptedCount > 0);

            Guid expectedClusterGuid = VegetationScatterIdentity.CreateClusterGuid(
                recipe.Biome.Guid,
                PackageId,
                recipe.World.Guid,
                recipe.TerrainRoot.Guid,
                PackageId,
                biome.Entries.Single(entry => entry.EntryId == recipe.EntryId).Species.Guid,
                PackageId,
                recipe.EntryId,
                recipe.Cell);
            Assert.Equal(expectedClusterGuid, first.ClusterMetadata.Guid);

            // Unchanged inputs must reproduce identical placement and page identities; the cluster
            // identity already embeds the cell, so a drift in either would change the page set.
            VegetationScatterBakeResult second = Bake(db, recipe);
            Assert.Equal(first.PlacementContentHash, second.PlacementContentHash);
            Assert.Equal(
                first.PageMetadata.Select(page => page.Guid),
                second.PageMetadata.Select(page => page.Guid));
            Assert.Equal(
                first.PageContentHashes.Select(hash => Convert.ToHexString(hash)),
                second.PageContentHashes.Select(hash => Convert.ToHexString(hash)));
        }
        finally
        {
            DeleteCookedRoot(cookedRoot);
        }
    }

    private static VegetationScatterBakeResult Bake(
        TestAssetDatabase db,
        VegetationScatterRecipeDescriptor recipe) =>
        VegetationScatterBaker.Build(
            db,
            new VegetationScatterCookRequest(
                recipe.World.Guid,
                recipe.Biome,
                recipe.TerrainRoot,
                recipe.Partition,
                recipe.Cell,
                recipe.EntryId,
                recipe.UnscaledConservativeRadius,
                recipe.Exclusions));

    private static void AssertCellOwnsExactlyItsOwnTiles(
        TestAssetDatabase db,
        WorldCellDescriptor cell,
        Dictionary<TerrainTileCoordinate, Guid> owners)
    {
        var world = new EntityManager();
        SceneLoadResult load = SceneAssetLoader.LoadScene(
            db,
            new AssetRef<SceneSourceAsset>(cell.Scene.Guid, "Scene", cell.Scene.PackageId),
            world);

        Assert.True(load.Success, load.Diagnostic);
        Assert.Equal(TilesPerCell, load.EntityCount);
        Assert.Equal(0, load.CameraCount);
        Assert.Equal(0, load.DirectionalLightCount);
        Assert.Equal(0, load.PointLightCount);
        Assert.Equal(0, load.SpotLightCount);
        Assert.Equal(0, load.EnvironmentCount);
        Assert.NotNull(load.AuthoringEntities);

        int cellX = cell.Key.Coordinate.X;
        int cellZ = cell.Key.Coordinate.Z;
        for (int offsetZ = 0; offsetZ < TilesPerCellAxis; offsetZ++)
        {
            for (int offsetX = 0; offsetX < TilesPerCellAxis; offsetX++)
            {
                int tileX = (cellX * TilesPerCellAxis) + offsetX;
                int tileZ = (cellZ * TilesPerCellAxis) + offsetZ;
                var coordinate = new TerrainTileCoordinate(tileX, tileZ);
                Guid tileGuid = TerrainTileIdentity.CreateGuid(
                    s_TerrainRootGuid,
                    PackageId,
                    coordinate);
                Guid entityGuid = TerrainTileEntityIdentity.Create(cell.Scene.Guid, tileGuid);

                Assert.True(
                    load.AuthoringEntities.TryGetEntity(entityGuid, out Entity entity),
                    $"Cell {cellX},{cellZ} does not author the entity that owns tile {tileX},{tileZ}.");
                Assert.True(world.HasComponent<TerrainTileComponent>(entity));

                TerrainTileComponent tile = world.GetComponent<TerrainTileComponent>(entity);
                Assert.Equal(s_TerrainRootGuid, tile.TerrainRootGuid);
                Assert.Equal(tileGuid, tile.TileGuid);
                Assert.Equal(s_LayerSetGuid, tile.LayerSetGuid);
                Assert.Equal(tileX, tile.TileX);
                Assert.Equal(tileZ, tile.TileZ);
                Assert.True(tile.IsVisible);
                Assert.InRange(tile.WorldPlacement.X, cell.Bounds.Min.X, cell.Bounds.Max.X);
                Assert.InRange(tile.WorldPlacement.Z, cell.Bounds.Min.Z, cell.Bounds.Max.Z);
                Assert.True(
                    owners.TryAdd(coordinate, tileGuid),
                    $"Tile {tileX},{tileZ} is owned by more than one cell scene.");
            }
        }
    }

    private static void AddRegionalWorldAssets(TestAssetDatabase db, string packageRoot)
    {
        db.AddAsset(
            s_WorldGuid,
            "World",
            Path.Combine(packageRoot, "Assets", "Worlds", "MistfallValley.arisenworld"),
            PackageId);
        db.AddAsset(
            s_PersistentSceneGuid,
            "Scene",
            Path.Combine(packageRoot, "Assets", "Scenes", "MistfallValley-Persistent.arisenscene"),
            PackageId);
        foreach (string metaPath in Directory.GetFiles(
            Path.Combine(packageRoot, "Assets", "Scenes", "MistfallValley Cells"),
            "*.arisenscene.meta"))
        {
            db.AddAsset(ReadSourceGuid(metaPath), "Scene", metaPath[..^5], PackageId);
        }

        // The persistent scene places its outcrop from the shared valley boulder mesh and lights the
        // world through the shipped dusk environment, so both stay part of the regional closure.
        foreach ((string RelativePath, string AssetType) reference in new[]
        {
            ("Assets/Vegetation/Meshes/ValleyBoulder.armesh", "Mesh"),
            ("Assets/Materials/ShowcaseGround.arismaterial", "Material"),
            ("Assets/Environments/MistfallDusk.arienvironment", "EnvironmentTexture")
        })
        {
            string path = Path.Combine(packageRoot, reference.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            db.AddAsset(ReadSourceGuid(path + ".meta"), reference.AssetType, path, PackageId);
        }
    }

    private static void AddRegionalTerrainAssets(TestAssetDatabase db, string packageRoot)
    {
        string terrainRoot = Path.Combine(packageRoot, "Assets", "Terrain", "MistfallValley");
        db.AddAsset(
            s_TerrainRootGuid,
            TerrainAssetTypes.Root,
            Path.Combine(terrainRoot, "MistfallValley.aristerrain"),
            PackageId);
        db.AddAsset(
            s_LayerSetGuid,
            TerrainAssetTypes.LayerSet,
            Path.Combine(packageRoot, "Assets", "Terrain", "ShowcaseValley.ariterrainlayers"),
            PackageId);
        for (int tileZ = 0; tileZ < TilesPerAxis; tileZ++)
        {
            for (int tileX = 0; tileX < TilesPerAxis; tileX++)
            {
                db.AddAsset(
                    TerrainTileIdentity.CreateGuid(
                        s_TerrainRootGuid,
                        PackageId,
                        new TerrainTileCoordinate(tileX, tileZ)),
                    TerrainAssetTypes.Tile,
                    Path.Combine(
                        terrainRoot,
                        "Generated",
                        "MistfallValley",
                        $"x_{tileX}_z_{tileZ}.ariterraingenerated"),
                    PackageId);
            }
        }
    }

    private static void AddRegionalVegetationAssets(TestAssetDatabase db, string packageRoot)
    {
        string vegetationRoot = Path.Combine(packageRoot, "Assets", "Vegetation");
        db.AddAsset(
            s_BiomeGuid,
            VegetationAssetTypes.Biome,
            Path.Combine(vegetationRoot, "ShowcaseValley.arivegetationbiome"),
            PackageId);
        AddGeneratedChildren(db, Path.Combine(vegetationRoot, "Meshes"), "*.armesh", "Mesh");
        AddGeneratedChildren(db, Path.Combine(vegetationRoot, "Materials"), "*.arismaterial", "Material");
        AddGeneratedChildren(db, vegetationRoot, "*.arivegetationspecies", VegetationAssetTypes.Species);

    }

    private static void AddGeneratedChildren(
        TestAssetDatabase db,
        string directory,
        string pattern,
        string assetType)
    {
        foreach (string path in Directory.GetFiles(directory, pattern))
        {
            db.AddAsset(ReadSourceGuid(path + ".meta"), assetType, path, PackageId);
        }
    }

    private static Guid ReadSourceGuid(string metaPath)
    {
        foreach (string line in File.ReadLines(metaPath))
        {
            if (line.StartsWith("Guid:", StringComparison.Ordinal))
            {
                return Guid.Parse(line["Guid:".Length..].Trim());
            }
        }

        throw new InvalidOperationException($"Asset metadata '{metaPath}' declares no GUID.");
    }

    private static string CreateCookedRoot(string name) =>
        Path.Combine(Path.GetTempPath(), name, Guid.NewGuid().ToString("N"));

    private static void DeleteCookedRoot(string cookedRoot)
    {
        try
        {
            if (Directory.Exists(cookedRoot))
            {
                Directory.Delete(cookedRoot, recursive: true);
            }
        }
        catch
        {
            // Best-effort test cleanup.
        }
    }

    private static string GetRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Arisen")))
            {
                return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}