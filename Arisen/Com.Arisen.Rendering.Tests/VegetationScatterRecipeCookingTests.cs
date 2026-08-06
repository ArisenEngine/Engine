using System.Text;
using ArisenEngine.Core.Assets;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation;
using ArisenEngine.Vegetation.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

[Collection(SceneComponentExtensionRegistryCollection.Name)]
public sealed class VegetationScatterRecipeCookingTests : IDisposable
{
    private const string PackageId = "com.arisen.packagegame";
    private const string PipelinePackageId = "com.arisen.generic-renderpipeline";

    private static readonly Guid s_RecipeGuid =
        Guid.Parse("aa347d75-087c-4fb9-998f-2cc6130ceac1");
    private static readonly Guid s_WorldGuid =
        Guid.Parse("9a9b4db5-c0a8-4f2e-8929-89464bea9d51");
    private static readonly Guid s_CenterSceneGuid =
        Guid.Parse("506af06e-b16d-4573-b6c9-98548c370e90");
    private static readonly Guid s_CenterCellGuid =
        Guid.Parse("5d13eda6-606a-57a0-bae4-cd559ddad464");
    private static readonly Guid s_PersistentSceneGuid =
        Guid.Parse("bfdbfc32-8a32-4b02-b8a9-65a172859a5c");
    private static readonly Guid s_BiomeGuid =
        Guid.Parse("c0a92f10-0eb9-4d24-b729-7d0f38313001");
    private static readonly Guid s_SpeciesGuid =
        Guid.Parse("7b0f2e52-8b67-4e3d-bf0a-cbc42f622001");
    private static readonly Guid s_ClusterGuid =
        Guid.Parse("e90ae5ab-24fb-2617-9983-3ed656bd652c");
    private static readonly Guid s_PageGuid =
        Guid.Parse("c1d7d00e-4aac-3819-b9f5-7a2a65e8e1eb");
    private static readonly Guid s_VegetationMeshGuid =
        Guid.Parse("9f57d9cc-2db6-4c85-ae7b-544338806e2c");
    private static readonly Guid s_VegetationMaterialGuid =
        Guid.Parse("4ac21c64-e984-4ed0-9e21-93878de5249e");

    private readonly string m_Root = Path.Combine(
        Path.GetTempPath(),
        "ArisenVegetationScatterRecipeCookingTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void FreshRuntimeCook_GeneratesCanonicalWorldVegetationClosure()
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
        CopyAsset(
            sourcePackageRoot,
            packageRoot,
            "Assets/Vegetation/ShowcaseValley.arivegetationbiome");
        CopyAsset(
            sourcePackageRoot,
            packageRoot,
            "Assets/Vegetation/ValleyRock.arivegetationspecies");
        CopyAsset(
            sourcePackageRoot,
            packageRoot,
            "Assets/Vegetation/ShowcaseValley.arivegetationscatter");
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
            s_PersistentSceneGuid,
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
            s_CenterSceneGuid,
            "Scene",
            CreateMinimalVegetationCellSource());
        WriteAsset(
            packageRoot,
            "Assets/Worlds/LanternWorld.arisenworld",
            s_WorldGuid,
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
        Assert.False(database.TryGetAssetDescriptor(s_ClusterGuid, out _));
        Assert.False(database.TryGetAssetDescriptor(s_PageGuid, out _));

        VegetationScatterRecipeGenerationResult generated = Assert.Single(
            VegetationScatterRecipeGenerator.GenerateAll(database, database));
        Assert.Equal(s_RecipeGuid, generated.RecipeGuid);
        Assert.Equal(s_ClusterGuid, generated.ClusterGuid);
        Assert.Equal(s_PageGuid, Assert.Single(generated.PageGuids));
        Assert.Equal(13, generated.InstanceCount);
        Assert.Equal(new WorldPosition(-256.0, -64.0, -256.0), generated.Origin);
        Assert.Equal(
            new WorldBounds(
                new WorldPosition(
                    -9.9700844287872314,
                    -2.1974024772644043,
                    -8.4557883739471436),
                new WorldPosition(
                    1.9058775901794434,
                    3.9241063594818115,
                    2.6819112300872803)),
            generated.Bounds);
        Assert.True(database.TryGetAsset(s_ClusterGuid, out AssetRecord clusterSource));
        AssetMetadata clusterMetadata =
            ArisenEngine.Core.Serialization.SerializationUtil.Deserialize<AssetMetadata>(
                clusterSource.MetaPath,
                serializeIfNotExist: false);
        Assert.Equal(s_BiomeGuid, clusterMetadata.Generated?.SourceGuid);
        Assert.True(database.TryGetCookedArtifact(
            s_ClusterGuid,
            VegetationClusterAssetCooker.RuntimeVariant,
            out _));
        Assert.True(database.TryGetCookedArtifact(
            s_PageGuid,
            VegetationInstancePageAssetCooker.RuntimeVariant,
            out _));
        string generatedRelativePath = Path.Combine(
            "Assets",
            "Vegetation",
            "Generated",
            s_RecipeGuid.ToString("N"));
        AssertGeneratedSourceMatchesTracked(
            Path.Combine(packageRoot, generatedRelativePath),
            Path.Combine(sourcePackageRoot, generatedRelativePath),
            "cluster.arivegetationgenerated");
        AssertGeneratedSourceMatchesTracked(
            Path.Combine(packageRoot, generatedRelativePath),
            Path.Combine(sourcePackageRoot, generatedRelativePath),
            "page-0000.arivegetationgenerated");

        var codec = new VegetationClusterSceneComponentCodec();
        SceneComponentExtensionRegistry.Shared.Register(codec);
        try
        {
            SceneInspectionResult inspection = SceneAssetLoader.InspectScene(
                database,
                new AssetRef<SceneSourceAsset>(s_CenterSceneGuid, "Scene", PackageId));
            Assert.True(inspection.Success, inspection.Diagnostic);
            Assert.Equal(1, inspection.EntityCount);
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
                [new RuntimeAssetCookRootRequest("startupWorld", s_WorldGuid, PackageId, "World")],
                registry);

            AssertCataloged(closure, s_WorldGuid, "World");
            AssertCataloged(closure, s_CenterSceneGuid, "Scene");
            AssertCataloged(closure, s_ClusterGuid, VegetationAssetTypes.Cluster);
            AssertCataloged(closure, s_PageGuid, VegetationAssetTypes.InstancePage);
            AssertCataloged(closure, s_BiomeGuid, VegetationAssetTypes.Biome);
            AssertCataloged(closure, s_SpeciesGuid, VegetationAssetTypes.Species);
            AssertCataloged(closure, s_VegetationMeshGuid, "Mesh");
            AssertCataloged(closure, s_VegetationMaterialGuid, "Material");
            RuntimeAssetCatalogArtifact cookedScene = Assert.Single(
                closure.Catalog.Artifacts,
                artifact => artifact.Guid == s_CenterSceneGuid && artifact.AssetType == "Scene");
            Assert.Contains(
                cookedScene.Dependencies,
                dependency => dependency.Guid == s_ClusterGuid &&
                    dependency.AssetType == VegetationAssetTypes.Cluster);
        }
        finally
        {
            SceneComponentExtensionRegistry.Shared.Unregister(codec);
            database.ReleaseAllLoadedCookedAssets();
        }
    }

    [Fact]
    public void TrackedLanternWorld_OwnsCanonicalClusterInCenterCell()
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

        Assert.Contains($"Guid: {s_CenterSceneGuid:D}", world, StringComparison.Ordinal);
        Assert.Contains($"Cluster: {{ Guid: {s_ClusterGuid:D}", scene, StringComparison.Ordinal);
        Assert.Contains($"OwningCellGuid: {s_CenterCellGuid:D}", scene, StringComparison.Ordinal);
        Assert.Contains("Cell: { X: 0, Y: 0, Z: 0, Layer: surface }", scene, StringComparison.Ordinal);
        Assert.Contains("InstanceCount: 13", scene, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(scene, "  MeshRenderer:"));
        Assert.DoesNotContain("VegetationCluster", importedScene, StringComparison.Ordinal);
        Assert.Null(authoredMetadata.Generated);
        Assert.Equal("ArisenSceneImporter", authoredMetadata.Importer);
        Assert.Equal("GltfSceneImporter", importedMetadata.Importer);
        Assert.NotNull(importedMetadata.Generated);
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

    private static string CreateMinimalWorldSource() => $$"""
        Version: 2
        WorldGuid: {{s_WorldGuid:D}}
        Name: Vegetation Fresh Cache Test World
        PersistentScene:
          Guid: {{s_PersistentSceneGuid:D}}
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
            Guid: {{s_CenterSceneGuid:D}}
            PackageId: {{PackageId}}
          Bounds:
            Min: { X: -256, Y: -64, Z: -256 }
            Max: { X: 0, Y: 64, Z: 0 }
          EstimatedCpuBytes: 1048576
          EstimatedGpuBytes: 67108864
        """;

    private static string CreateMinimalVegetationCellSource() => $$"""
        Version: 2
        Name: Vegetation Center Cell
        ComponentSchemas:
        - TypeId: 1
          Name: Transform
          Version: 1
          Required: true
        - TypeId: 1447380803
          Name: VegetationCluster
          Version: 1
          Required: true
        Entities:
        - Guid: 9cfcb277-b756-4349-b55e-39ddf24cfbe1
          Name: Showcase Valley Vegetation Cluster
          Transform:
            Position: { X: 0, Y: 0, Z: 0 }
            Rotation: { X: 0, Y: 0, Z: 0, W: 1 }
            Scale: { X: 1, Y: 1, Z: 1 }
          VegetationCluster:
            Cluster: { Guid: {{s_ClusterGuid:D}}, PackageId: {{PackageId}} }
            Biome: { Guid: {{s_BiomeGuid:D}}, PackageId: {{PackageId}} }
            Species: { Guid: {{s_SpeciesGuid:D}}, PackageId: {{PackageId}} }
            WorldGuid: {{s_WorldGuid:D}}
            OwningCellGuid: {{s_CenterCellGuid:D}}
            Cell: { X: 0, Y: 0, Z: 0, Layer: surface }
            Origin: { X: -256, Y: -64, Z: -256 }
            Bounds:
              Min: { X: -9.9700844287872314, Y: -2.1974024772644043, Z: -8.4557883739471436 }
              Max: { X: 1.9058775901794434, Y: 3.9241063594818115, Z: 2.6819112300872803 }
            Visible: true
            CastShadows: true
            ReceiveShadows: true
            QualityGroup: 0
            PageCount: 1
            InstanceCount: 13
        """;

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

        throw new DirectoryNotFoundException("Could not locate the Arisen repository root.");
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
