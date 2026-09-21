using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using ArisenEngine.Core.Assets;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation;
using ArisenEngine.Vegetation.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

[Collection(SceneComponentExtensionRegistryCollection.Name)]
public sealed class VegetationScatterRecipeCookingTests : IDisposable
{
    private const string PackageId = VegetationCanonicalFixture.PackageId;
    private const string PipelinePackageId = VegetationCanonicalFixture.FilterPackageId;

    private readonly string m_Root = Path.Combine(
        Path.GetTempPath(),
        "ArisenVegetationScatterRecipeCookingTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void FreshRuntimeCook_GeneratesCanonicalSpeciesFixtureClosure()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sourcePackageRoot = Path.Combine(
            repositoryRoot,
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            PackageId);
        string sourcePipelineRoot = Path.Combine(
            repositoryRoot,
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            PipelinePackageId);
        string workspaceRoot = Path.Combine(m_Root, "Workspace");
        string packageRoot = Path.Combine(m_Root, "PackageGame");
        string pipelineRoot = Path.Combine(m_Root, "GenericRenderPipeline");
        Directory.CreateDirectory(workspaceRoot);

        CopyDirectory(
            Path.Combine(sourcePackageRoot, "Assets", "Terrain"),
            Path.Combine(packageRoot, "Assets", "Terrain"));
        foreach (string directory in new[] { "Meshes", "Materials", "Textures" })
        {
            CopyDirectory(
                Path.Combine(sourcePackageRoot, "Assets", "Vegetation", directory),
                Path.Combine(packageRoot, "Assets", "Vegetation", directory));
        }

        CopyAsset(
            sourcePackageRoot,
            packageRoot,
            "Assets/Vegetation/ShowcaseValley.arivegetationbiome");
        foreach (VegetationCanonicalSpecies species in VegetationCanonicalFixture.Species)
        {
            CopyAsset(
                sourcePackageRoot,
                packageRoot,
                $"Assets/Vegetation/Valley{species.Name}.arivegetationspecies");
            CopyAsset(
                sourcePackageRoot,
                packageRoot,
                $"Assets/Vegetation/Valley{species.Name}.arivegetationscatter");
        }

        CopyAsset(
            sourcePipelineRoot,
            pipelineRoot,
            "Assets/Meshes/FacetedCrystal.obj");
        CopyAsset(
            sourcePipelineRoot,
            pipelineRoot,
            "Assets/Materials/StandardLitMaterial.arismaterial");

        WriteAsset(
            packageRoot,
            "Assets/Scenes/EmptyPersistent.arisenscene",
            VegetationCanonicalFixture.PersistentSceneGuid,
            "Scene",
            "Version: 2\nName: Empty Persistent\nComponentSchemas:\n" +
            "- TypeId: 1\n  Name: Transform\n  Version: 1\n  Required: true\n" +
            "Entities:\n" +
            "- Guid: a5d438ce-e272-411e-b8a5-f325d77adbe9\n" +
            "  Name: Persistent Root\n" +
            "  Transform:\n" +
            "    Position: { X: 0, Y: 0, Z: 0 }\n" +
            "    Rotation: { X: 0, Y: 0, Z: 0, W: 1 }\n" +
            "    Scale: { X: 1, Y: 1, Z: 1 }\n");
        WriteAsset(
            packageRoot,
            "Assets/Scenes/VegetationCenterCell.arisenscene",
            VegetationCanonicalFixture.CenterSceneGuid,
            "Scene",
            CreateMinimalVegetationCellSource());
        WriteAsset(
            packageRoot,
            "Assets/Worlds/LanternWorld.arisenworld",
            VegetationCanonicalFixture.WorldGuid,
            "World",
            CreateMinimalWorldSource());

        var database = new AssetDatabase();
        database.InitializeWorkspace(
            workspaceRoot,
            [
                (PackageId, packageRoot),
                (PipelinePackageId, pipelineRoot)
            ],
            AssetSourceAccessMode.RuntimeAssetCook);
        Assert.Empty(Directory.EnumerateFiles(
            Path.Combine(packageRoot, "Assets", "Vegetation"),
            "*.arivegetationgenerated",
            SearchOption.AllDirectories));
        foreach (VegetationCanonicalSpecies species in VegetationCanonicalFixture.Species)
        {
            Assert.False(database.TryGetAssetDescriptor(species.ClusterGuid, out _));
            Assert.False(database.TryGetAssetDescriptor(species.PageGuid, out _));
        }

        IReadOnlyList<VegetationScatterRecipeGenerationResult> generated =
            VegetationScatterRecipeGenerator.GenerateAll(database, database);
        Assert.Equal(VegetationCanonicalFixture.Species.Length, generated.Count);
        foreach (VegetationCanonicalSpecies species in VegetationCanonicalFixture.Species)
        {
            VegetationScatterRecipeGenerationResult result = Assert.Single(
                generated,
                candidate => candidate.RecipeGuid == species.RecipeGuid);
            Assert.Equal(species.ClusterGuid, result.ClusterGuid);
            Assert.Equal(species.PageCount, result.PageGuids.Count);
            Assert.Contains(species.PageGuid, result.PageGuids);
            Assert.Equal(species.InstanceCount, result.InstanceCount);
            Assert.Equal(VegetationCanonicalFixture.CellOrigin, result.Origin);
            Assert.Equal(species.Bounds, result.Bounds);
            Assert.True(database.TryGetAsset(species.ClusterGuid, out AssetRecord clusterSource));
            AssetMetadata clusterMetadata =
                ArisenEngine.Core.Serialization.SerializationUtil.Deserialize<AssetMetadata>(
                    clusterSource.MetaPath,
                    serializeIfNotExist: false);
            Assert.Equal(VegetationCanonicalFixture.BiomeGuid, clusterMetadata.Generated?.SourceGuid);
            Assert.True(database.TryGetCookedArtifact(
                species.ClusterGuid,
                VegetationClusterAssetCooker.RuntimeVariant,
                out _));
            Assert.True(database.TryGetCookedArtifact(
                species.PageGuid,
                VegetationInstancePageAssetCooker.RuntimeVariant,
                out _));

            string generatedRelativePath = Path.Combine(
                "Assets",
                "Vegetation",
                "Generated",
                VegetationCanonicalFixture.GetGeneratedDirectoryName(species.RecipeGuid));
            AssertGeneratedSourceMatchesTracked(
                Path.Combine(packageRoot, generatedRelativePath),
                Path.Combine(sourcePackageRoot, generatedRelativePath),
                "cluster.arivegetationgenerated");
            AssertGeneratedSourceMatchesTracked(
                Path.Combine(packageRoot, generatedRelativePath),
                Path.Combine(sourcePackageRoot, generatedRelativePath),
                "page-0000.arivegetationgenerated");
        }

        var codec = new VegetationClusterSceneComponentCodec();
        SceneComponentExtensionRegistry.Shared.Register(codec);
        try
        {
            SceneInspectionResult inspection = SceneAssetLoader.InspectScene(
                database,
                new AssetRef<SceneSourceAsset>(
                    VegetationCanonicalFixture.CenterSceneGuid,
                    "Scene",
                    PackageId));
            Assert.True(inspection.Success, inspection.Diagnostic);
            Assert.Equal(VegetationCanonicalFixture.Species.Length, inspection.EntityCount);
            Assert.Equal(0, inspection.MeshRendererCount);

            var registry = new RuntimeAssetCookerRegistry();
            registry.RegisterCooker(new SceneRuntimeAssetCooker(database));
            registry.RegisterCooker(new WorldRuntimeAssetCooker(database));
            registry.RegisterCooker(new VegetationRuntimeAssetCooker(database));
            registry.RegisterCooker(new DeterministicRenderingDependencyCooker(m_Root));
            RuntimeAssetCookResult closure = RuntimeAssetCookCoordinator.Cook(
                new RuntimeAssetCookContext(
                    workspaceRoot,
                    "Development",
                    "Debug",
                    "win-x64",
                    Path.Combine(m_Root, "Staging"),
                    ForceRebuild: false),
                [new RuntimeAssetCookRootRequest(
                    "startupWorld",
                    VegetationCanonicalFixture.WorldGuid,
                    PackageId,
                    "World")],
                registry);

            AssertCataloged(closure, VegetationCanonicalFixture.WorldGuid, "World");
            AssertCataloged(closure, VegetationCanonicalFixture.CenterSceneGuid, "Scene");
            AssertCataloged(closure, VegetationCanonicalFixture.BiomeGuid, VegetationAssetTypes.Biome);
            foreach (VegetationCanonicalSpecies species in VegetationCanonicalFixture.Species)
            {
                AssertCataloged(closure, species.ClusterGuid, VegetationAssetTypes.Cluster);
                AssertCataloged(closure, species.PageGuid, VegetationAssetTypes.InstancePage);
                AssertCataloged(closure, species.SpeciesGuid, VegetationAssetTypes.Species);
                AssertCataloged(closure, species.MeshGuid, "Mesh");
                AssertCataloged(closure, species.MaterialGuid, "Material");
            }

            RuntimeAssetCatalogArtifact cookedScene = Assert.Single(
                closure.Catalog.Artifacts,
                artifact => artifact.Guid == VegetationCanonicalFixture.CenterSceneGuid &&
                    artifact.AssetType == "Scene");
            foreach (VegetationCanonicalSpecies species in VegetationCanonicalFixture.Species)
            {
                Assert.Contains(
                    cookedScene.Dependencies,
                    dependency => dependency.Guid == species.ClusterGuid &&
                        dependency.AssetType == VegetationAssetTypes.Cluster);
            }
        }
        finally
        {
            SceneComponentExtensionRegistry.Shared.Unregister(codec);
            database.ReleaseAllLoadedCookedAssets();
        }

        AssertDenseRockRebuildRollsBackPublication(database, packageRoot);
    }

    private static void AssertDenseRockRebuildRollsBackPublication(
        AssetDatabase database,
        string packageRoot)
    {
        VegetationCanonicalSpecies rock = VegetationCanonicalFixture.Require("Rock");
        string biomePath = Path.Combine(
            packageRoot,
            "Assets",
            "Vegetation",
            "ShowcaseValley.arivegetationbiome");
        string originalBiome = File.ReadAllText(biomePath);
        string generatedRelativePath = Path.Combine(
            "Assets",
            "Vegetation",
            "Generated",
            VegetationCanonicalFixture.GetGeneratedDirectoryName(rock.RecipeGuid));
        string generatedRoot = Path.Combine(packageRoot, generatedRelativePath);
        try
        {
            // Reduce only the first biome entry (the rock) to one instance per page so the rebuild
            // republishes a multi-page cluster while every other species keeps its single page.
            int remainingEntryIndex = originalBiome.IndexOf(
                "- EntryId: valley-grass",
                StringComparison.Ordinal);
            Assert.True(remainingEntryIndex > 0);
            string rockEntry = originalBiome[..remainingEntryIndex];
            Match clusterSize = Regex.Match(
                rockEntry,
                @"ClusterSize: (?<size>[0-9]+)",
                RegexOptions.CultureInvariant);
            Assert.True(clusterSize.Success, "The rock biome entry declares no ClusterSize.");
            int rockPageCapacity = int.Parse(
                clusterSize.Groups["size"].Value,
                CultureInfo.InvariantCulture);
            Assert.True(
                rockPageCapacity > 1,
                "The rock biome entry must publish more than one instance per page so the dense " +
                "rebuild exercises a multi-page cluster.");
            File.WriteAllText(
                biomePath,
                rockEntry[..clusterSize.Index] +
                    "ClusterSize: 1" +
                    rockEntry[(clusterSize.Index + clusterSize.Length)..] +
                    originalBiome[remainingEntryIndex..]);

            IReadOnlyList<VegetationScatterRecipeGenerationResult> dense =
                VegetationScatterRecipeGenerator.GenerateAll(database, database);
            Assert.Equal(VegetationCanonicalFixture.Species.Length, dense.Count);
            VegetationScatterRecipeGenerationResult denseRock = Assert.Single(
                dense,
                candidate => candidate.RecipeGuid == rock.RecipeGuid);
            Assert.Equal(rock.InstanceCount, denseRock.PageGuids.Count);
            Assert.DoesNotContain(rock.PageGuid, denseRock.PageGuids);
            Assert.True(database.TryGetCookedArtifact(
                rock.ClusterGuid,
                VegetationClusterAssetCooker.RuntimeVariant,
                out CookedAssetRecord denseClusterArtifact));
            byte[] denseClusterBytes = File.ReadAllBytes(denseClusterArtifact.Path);
            Assert.False(database.TryGetAsset(rock.PageGuid, out _));
            Assert.False(database.TryGetCookedArtifact(
                rock.PageGuid,
                VegetationInstancePageAssetCooker.RuntimeVariant,
                out _));
            foreach (Guid pageGuid in denseRock.PageGuids)
            {
                Assert.True(database.TryGetAsset(pageGuid, out _));
                Assert.True(database.TryGetCookedArtifact(
                    pageGuid,
                    VegetationInstancePageAssetCooker.RuntimeVariant,
                    out _));
            }

            Dictionary<string, byte[]> denseGeneratedSources =
                SnapshotGeneratedSources(generatedRoot);

            // Republishing the whole fixture must be atomic per recipe: restoring the authored
            // biome makes the rebuild republish every species again, and a failure at the first
            // manifest commit leaves the previous closure, cache paths, and generated sources
            // untouched for every recipe.
            File.WriteAllText(biomePath, originalBiome);
            int manifestCommitCount = 0;
            database.BeforeCookedManifestReplace = _ =>
            {
                if (++manifestCommitCount == 1)
                {
                    throw new InvalidOperationException(
                        "Injected scatter closure publication failure.");
                }
            };
            Assert.Throws<InvalidOperationException>(
                () => VegetationScatterRecipeGenerator.GenerateAll(database, database));
            database.BeforeCookedManifestReplace = null;
            Assert.Equal(1, manifestCommitCount);

            AssertGeneratedSourcesUnchanged(generatedRoot, denseGeneratedSources);
            foreach (Guid pageGuid in denseRock.PageGuids)
            {
                Assert.True(database.TryGetAsset(pageGuid, out _));
                Assert.True(database.TryGetCookedArtifact(
                    pageGuid,
                    VegetationInstancePageAssetCooker.RuntimeVariant,
                    out _));
            }

            Assert.True(database.TryGetCookedArtifact(
                rock.ClusterGuid,
                VegetationClusterAssetCooker.RuntimeVariant,
                out CookedAssetRecord restoredClusterArtifact));
            Assert.Equal(denseClusterArtifact.Path, restoredClusterArtifact.Path);
            Assert.Equal(denseClusterBytes, File.ReadAllBytes(restoredClusterArtifact.Path));
        }
        finally
        {
            database.BeforeCookedManifestReplace = null;
            File.WriteAllText(biomePath, originalBiome);
        }
    }

    [Fact]
    public void TrackedLanternWorld_OwnsCanonicalSpeciesClustersInCenterCell()
    {
        string packageRoot = Path.Combine(
            FindRepositoryRoot(),
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            PackageId);
        string world = File.ReadAllText(
            Path.Combine(packageRoot, "Assets", "Worlds", "LanternWorld.arisenworld"));
        string scene = File.ReadAllText(
            Path.Combine(packageRoot, "Assets", "Scenes", "TeapotCenterCell.arisenscene"));
        string importedScene = File.ReadAllText(
            Path.Combine(
                packageRoot,
                "Assets",
                "Generated",
                "Lantern",
                "Scenes",
                "Scene_0.arisenscene"));
        AssetMetadata authoredMetadata =
            ArisenEngine.Core.Serialization.SerializationUtil.Deserialize<AssetMetadata>(
                Path.Combine(
                    packageRoot,
                    "Assets",
                    "Scenes",
                    "TeapotCenterCell.arisenscene.meta"),
                serializeIfNotExist: false);
        AssetMetadata importedMetadata =
            ArisenEngine.Core.Serialization.SerializationUtil.Deserialize<AssetMetadata>(
                Path.Combine(
                    packageRoot,
                    "Assets",
                    "Generated",
                    "Lantern",
                    "Scenes",
                    "Scene_0.arisenscene.meta"),
                serializeIfNotExist: false);

        Assert.Contains(
            $"Guid: {VegetationCanonicalFixture.CenterSceneGuid:D}",
            world,
            StringComparison.Ordinal);
        Assert.Contains(
            $"OwningCellGuid: {VegetationCanonicalFixture.CenterCellGuid:D}",
            scene,
            StringComparison.Ordinal);
        Assert.Equal(
            VegetationCanonicalFixture.Species.Length,
            CountOccurrences(scene, "Cell: { X: 0, Y: 0, Z: 0, Layer: surface }"));
        Assert.Equal(
            VegetationCanonicalFixture.Species.Length,
            CountOccurrences(scene, "  TerrainTile:"));
        Assert.Equal(0, CountOccurrences(scene, "  MeshRenderer:"));
        Assert.DoesNotContain("VegetationCluster", importedScene, StringComparison.Ordinal);
        Assert.Null(authoredMetadata.Generated);
        Assert.Equal("ArisenSceneImporter", authoredMetadata.Importer);
        Assert.Equal("GltfSceneImporter", importedMetadata.Importer);
        Assert.NotNull(importedMetadata.Generated);

        foreach (VegetationCanonicalSpecies species in VegetationCanonicalFixture.Species)
        {
            Assert.Contains(
                $"Cluster: {{ Guid: {species.ClusterGuid:D}",
                scene,
                StringComparison.Ordinal);
            Assert.Contains(
                $"Species: {{ Guid: {species.SpeciesGuid:D}",
                scene,
                StringComparison.Ordinal);
            AuthoredCluster authored = ReadAuthoredCluster(scene, species.ClusterGuid);
            Assert.Equal(species.InstanceCount, authored.InstanceCount);
            Assert.Equal(species.MinX, authored.MinX);
            Assert.Equal(species.MinY, authored.MinY);
            Assert.Equal(species.MinZ, authored.MinZ);
            Assert.Equal(species.MaxX, authored.MaxX);
            Assert.Equal(species.MaxY, authored.MaxY);
            Assert.Equal(species.MaxZ, authored.MaxZ);

            string generatedDirectory = Path.Combine(
                packageRoot,
                "Assets",
                "Vegetation",
                "Generated",
                VegetationCanonicalFixture.GetGeneratedDirectoryName(species.RecipeGuid));
            string clusterSource = File.ReadAllText(
                Path.Combine(generatedDirectory, "cluster.arivegetationgenerated"));
            Assert.Contains(
                $"Guid: {species.ClusterGuid:D}",
                clusterSource,
                StringComparison.Ordinal);
            Assert.Contains(
                $"RecipeGuid: {species.RecipeGuid:D}",
                clusterSource,
                StringComparison.Ordinal);
            AssetMetadata pageMetadata =
                ArisenEngine.Core.Serialization.SerializationUtil.Deserialize<AssetMetadata>(
                    Path.Combine(generatedDirectory, "page-0000.arivegetationgenerated.meta"),
                    serializeIfNotExist: false);
            Assert.Equal(species.PageGuid, pageMetadata.Guid);
            Assert.Equal(
                VegetationAssetTypes.InstancePage,
                pageMetadata.AssetType);
            Assert.Equal(species.ClusterGuid, pageMetadata.Generated?.SourceGuid);
        }
    }

    public void Dispose()
    {
        if (!Directory.Exists(m_Root))
        {
            return;
        }

        try
        {
            Directory.Delete(m_Root, recursive: true);
        }
        catch
        {
            // Best-effort test cleanup.
        }
    }

    private static void AssertCataloged(
        RuntimeAssetCookResult result,
        Guid guid,
        string assetType)
    {
        Assert.Contains(
            result.Catalog.Artifacts,
            artifact => artifact.Guid == guid &&
                string.Equals(artifact.AssetType, assetType, StringComparison.Ordinal));
    }

    private static Dictionary<string, byte[]> SnapshotGeneratedSources(string generatedRoot)
    {
        var snapshot = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(
                     generatedRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            snapshot[Path.GetRelativePath(generatedRoot, file)] = File.ReadAllBytes(file);
        }

        return snapshot;
    }

    private static void AssertGeneratedSourcesUnchanged(
        string generatedRoot,
        Dictionary<string, byte[]> snapshot)
    {
        Dictionary<string, byte[]> current = SnapshotGeneratedSources(generatedRoot);
        Assert.Equal(
            snapshot.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray(),
            current.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray());
        foreach ((string relativePath, byte[] expected) in snapshot)
        {
            Assert.Equal(expected, current[relativePath]);
        }
    }

    private static void AssertGeneratedSourceMatchesTracked(
        string generatedRoot,
        string trackedRoot,
        string fileName)
    {
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(trackedRoot, fileName)),
            File.ReadAllBytes(Path.Combine(generatedRoot, fileName)));
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(trackedRoot, fileName + ".meta")),
            File.ReadAllBytes(Path.Combine(generatedRoot, fileName + ".meta")));
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static AuthoredCluster ReadAuthoredCluster(string scene, Guid clusterGuid)
    {
        string marker = $"Cluster: {{ Guid: {clusterGuid:D}";
        string? block = scene
            .Split("- Guid: ", StringSplitOptions.None)
            .FirstOrDefault(candidate => candidate.Contains(marker, StringComparison.Ordinal));
        Assert.NotNull(block);
        Match bounds = Regex.Match(
            block!,
            @"Min: \{ X: (?<minX>[^,]+), Y: (?<minY>[^,]+), Z: (?<minZ>[^}]+) \}\s+" +
            @"Max: \{ X: (?<maxX>[^,]+), Y: (?<maxY>[^,]+), Z: (?<maxZ>[^}]+) \}");
        Match instances = Regex.Match(block!, @"InstanceCount: (?<instances>\d+)");
        Assert.True(bounds.Success, $"Scene block for cluster '{clusterGuid:D}' has no authored bounds.");
        Assert.True(
            instances.Success,
            $"Scene block for cluster '{clusterGuid:D}' has no authored instance count.");
        return new AuthoredCluster(
            int.Parse(instances.Groups["instances"].Value, CultureInfo.InvariantCulture),
            ParseDouble(bounds.Groups["minX"].Value),
            ParseDouble(bounds.Groups["minY"].Value),
            ParseDouble(bounds.Groups["minZ"].Value),
            ParseDouble(bounds.Groups["maxX"].Value),
            ParseDouble(bounds.Groups["maxY"].Value),
            ParseDouble(bounds.Groups["maxZ"].Value));
    }

    private static double ParseDouble(string value) =>
        double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string FormatDouble(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private readonly record struct AuthoredCluster(
        int InstanceCount,
        double MinX,
        double MinY,
        double MinZ,
        double MaxX,
        double MaxY,
        double MaxZ);

    private static string CreateMinimalWorldSource() => $$"""
        Version: 2
        WorldGuid: {{VegetationCanonicalFixture.WorldGuid:D}}
        Name: Vegetation Fresh Cache Test World
        PersistentScene:
          Guid: {{VegetationCanonicalFixture.PersistentSceneGuid:D}}
          PackageId: {{PackageId}}
        Partition:
          Origin: { X: -256, Y: -64, Z: -256 }
          CellSize: { X: 256, Y: 128, Z: 256 }
          LoadRadius: 1
          UnloadHysteresis: 1
          MaxActiveCells: 4
        Policy:
          UnresolvedReferences: KeepUnresolved
          UnloadedTargets: ClearAndLateResolve
          DependencyCycles: Reject
        Layers:
        - Id: surface
          Priority: 0
        Cells:
        - Coordinate: { X: 0, Y: 0, Z: 0 }
          Layer: surface
          Scene:
            Guid: {{VegetationCanonicalFixture.CenterSceneGuid:D}}
            PackageId: {{PackageId}}
          Bounds:
            Min: { X: -256, Y: -64, Z: -256 }
            Max: { X: 0, Y: 64, Z: 0 }
          EstimatedCpuBytes: 1048576
          EstimatedGpuBytes: 67108864
        """;

    private static string CreateMinimalVegetationCellSource()
    {
        var builder = new StringBuilder();
        builder.Append(
            "Version: 2\n" +
            "Name: Vegetation Center Cell\n" +
            "ComponentSchemas:\n" +
            "- TypeId: 1\n  Name: Transform\n  Version: 1\n  Required: true\n" +
            "- TypeId: 1447380803\n  Name: VegetationCluster\n  Version: 1\n  Required: true\n" +
            "Entities:\n");
        for (int index = 0; index < VegetationCanonicalFixture.Species.Length; index++)
        {
            VegetationCanonicalSpecies species = VegetationCanonicalFixture.Species[index];
            builder
                .Append($"- Guid: 9cfcb277-b756-4349-b55e-39ddf24cfbe{index + 1}\n")
                .Append($"  Name: Showcase Valley Vegetation Cluster {species.Name}\n")
                .Append("  Transform:\n")
                .Append("    Position: { X: 0, Y: 0, Z: 0 }\n")
                .Append("    Rotation: { X: 0, Y: 0, Z: 0, W: 1 }\n")
                .Append("    Scale: { X: 1, Y: 1, Z: 1 }\n")
                .Append("  VegetationCluster:\n")
                .Append($"    Cluster: {{ Guid: {species.ClusterGuid:D}, PackageId: {PackageId} }}\n")
                .Append(
                    $"    Biome: {{ Guid: {VegetationCanonicalFixture.BiomeGuid:D}, " +
                    $"PackageId: {PackageId} }}\n")
                .Append(
                    $"    Species: {{ Guid: {species.SpeciesGuid:D}, PackageId: {PackageId} }}\n")
                .Append($"    WorldGuid: {VegetationCanonicalFixture.WorldGuid:D}\n")
                .Append(
                    $"    OwningCellGuid: {VegetationCanonicalFixture.CenterCellGuid:D}\n")
                .Append("    Cell: { X: 0, Y: 0, Z: 0, Layer: surface }\n")
                .Append("    Origin: { X: -256, Y: -64, Z: -256 }\n")
                .Append("    Bounds:\n")
                .Append(
                    $"      Min: {{ X: {FormatDouble(species.MinX)}, " +
                    $"Y: {FormatDouble(species.MinY)}, Z: {FormatDouble(species.MinZ)} }}\n")
                .Append(
                    $"      Max: {{ X: {FormatDouble(species.MaxX)}, " +
                    $"Y: {FormatDouble(species.MaxY)}, Z: {FormatDouble(species.MaxZ)} }}\n")
                .Append("    Visible: true\n")
                .Append("    CastShadows: true\n")
                .Append("    ReceiveShadows: true\n")
                .Append("    QualityGroup: 0\n")
                .Append($"    PageCount: {species.PageCount}\n")
                .Append($"    InstanceCount: {species.InstanceCount}\n");
        }

        return builder.ToString();
    }

    private static void CopyAsset(
        string sourcePackageRoot,
        string targetPackageRoot,
        string relativePath)
    {
        string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        string source = Path.Combine(sourcePackageRoot, normalized);
        string target = Path.Combine(targetPackageRoot, normalized);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target);
        File.Copy(source + ".meta", target + ".meta");
    }

    private static void CopyDirectory(string source, string target)
    {
        foreach (string sourceFile in Directory.EnumerateFiles(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, sourceFile);
            string targetFile = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(sourceFile, targetFile);
        }
    }

    private static void WriteAsset(
        string packageRoot,
        string relativePath,
        Guid guid,
        string assetType,
        string source)
    {
        string path = Path.Combine(
            packageRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(
            path + ".meta",
            $"Guid: {guid:D}\nAssetType: {assetType}\nImporter: TestImporter\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Arisen")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class DeterministicRenderingDependencyCooker : IRuntimeAssetCooker
    {
        private readonly string m_OutputRoot;

        public DeterministicRenderingDependencyCooker(string outputRoot)
        {
            m_OutputRoot = outputRoot;
        }

        public string ProviderId => "com.arisen.test.vegetation-recipe-rendering-dependencies";

        public IReadOnlyCollection<string> AssetTypes { get; } = ["Mesh", "Material"];

        public RuntimeAssetCookerOutput Cook(
            RuntimeAssetCookContext context,
            RuntimeAssetCookRequest request)
        {
            string variant = request.AssetType switch
            {
                "Mesh" => "staticmesh.uint32",
                "Material" => "material.runtime",
                _ => throw new InvalidOperationException(
                    $"Unsupported rendering dependency type '{request.AssetType}'.")
            };
            if (request.Variant.Length > 0 &&
                !string.Equals(request.Variant, variant, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unexpected rendering dependency variant '{request.Variant}'.");
            }

            string sourcePath = Path.Combine(
                m_OutputRoot,
                "Rendering",
                $"{request.Guid:N}.{request.AssetType}.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllBytes(
                sourcePath,
                Encoding.UTF8.GetBytes(
                    $"{request.Guid:N}|{request.PackageId}|{request.AssetType}|{request.Variant}"));
            return RuntimeAssetCookerOutput.FromFile(
                request,
                variant,
                $"{request.PackageId}/{request.Guid:N}/{variant}.bin",
                sourcePath,
                formatVersion: 1);
        }
    }
}
