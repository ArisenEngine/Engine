using System.Security.Cryptography;
using System.Numerics;
using Arisen.Native.RHI;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Rendering;
using ArisenEngine.Rendering.Resources;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using ArisenEngine.Vegetation.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

[Collection(SceneComponentExtensionRegistryCollection.Name)]
public sealed class ShowcaseSceneAssetTests
{
    private static readonly Guid s_SceneGuid = Guid.Parse("0bb7d5fb-1924-45ee-9b45-85891d0e6d9f");
    private static readonly Guid s_TeapotMeshGuid = Guid.Parse("7300d793-8cd9-4983-8035-e99aa92e9449");
    private static readonly Guid s_PedestalMeshGuid = Guid.Parse("ed26e5c7-77ae-4cd7-b4ec-cb02ea6bac33");
    private static readonly Guid s_GroundMeshGuid = Guid.Parse("0fed982b-5130-430e-860b-dc9c7284b8c1");
    private static readonly Guid s_MarbleTextureGuid = Guid.Parse("678ed7b8-bb78-4b47-b5c0-416879f06917");
    private static readonly Guid s_MarbleMaterialGuid = Guid.Parse("a2122786-c40a-41f2-bf35-1f5cbc4d39c2");
    private static readonly Guid s_CharcoalMaterialGuid = Guid.Parse("a2dd1381-3958-4480-837b-35ffe6dd15c0");
    private static readonly Guid s_GroundMaterialGuid = Guid.Parse("e87b0335-5689-45a0-9fe1-455a01b59a20");
    private static readonly Guid s_StandardLitShaderGuid = Guid.Parse("72b6d255-0f54-46e5-9d05-8e0486d4f875");
    private static readonly Guid s_DefaultNormalGuid = Guid.Parse("ead642b0-cde6-4ae0-8bdc-03472fb3d5aa");
    private static readonly Guid s_LanternModelGuid = Guid.Parse("8d5dc1c2-1a92-4502-9c49-b8fb2c42f9f7");
    private static readonly Guid s_LanternWorldGuid = Guid.Parse("9a9b4db5-c0a8-4f2e-8929-89464bea9d51");
    private static readonly Guid s_LanternShowcaseSceneGuid = Guid.Parse("bd52ee1a-84a1-4d9d-bdb4-2a83757b5a4b");
    private static readonly Guid s_LanternGeneratedSceneGuid = Guid.Parse("c3845237-9946-6fea-13b6-9672584bed67");
    private static readonly Guid s_TeapotWestCellSceneGuid = Guid.Parse("c3db7370-879e-40ad-956c-4de8a7331e88");
    private static readonly Guid s_TeapotCenterCellSceneGuid = Guid.Parse("506af06e-b16d-4573-b6c9-98548c370e90");
    private static readonly Guid s_LanternMesh0Guid = Guid.Parse("898e8c48-c5f9-b88b-d3a6-49e82e954bd4");
    private static readonly Guid s_LanternMesh1Guid = Guid.Parse("be413b32-5991-8ae5-c2c2-eba34d934325");
    private static readonly Guid s_LanternMesh2Guid = Guid.Parse("a8eb7531-7a75-0078-cf43-3fe29fbad766");
    private static readonly Guid s_LanternMaterialGuid = Guid.Parse("55b0353b-2e5d-b80f-545f-5a386d72f006");
    private static readonly Guid s_LanternBaseColorTextureGuid = Guid.Parse("85f2677d-96e2-bf70-3e3d-27e2e73a8a16");
    private static readonly Guid s_LanternMetallicRoughnessTextureGuid = Guid.Parse("984534a2-785f-4f19-5ebb-262a54c6e526");
    private static readonly Guid s_LanternNormalTextureGuid = Guid.Parse("1a36c586-10fb-ccf3-98e8-99310dbbf5d2");
    private static readonly Guid s_LanternEmissiveTextureGuid = Guid.Parse("9c41d0a0-2a84-8185-81c5-ff80d034b696");
    private static readonly Guid s_BlueHourSourceTextureGuid = Guid.Parse("d5a4f9f1-2f0f-4da8-8b15-19ac1fb30e57");
    private static readonly Guid s_BlueHourEnvironmentGuid = Guid.Parse("b7c4e40e-c95f-47e5-84b7-b7554e0edc17");
    private static readonly Guid s_TerrainRootGuid = Guid.Parse("6f4d0a1c-0e85-4a42-93fe-34058ef48511");
    private static readonly Guid s_TerrainLayerSetGuid = Guid.Parse("5dcaa6bd-2b51-498d-9fa9-bd68f4642761");
    private static readonly Guid s_TerrainTile0Guid = Guid.Parse("807a7664-36ee-bdf8-71ea-e78cd4db0c4f");
    private static readonly Guid s_TerrainTile1Guid = Guid.Parse("9b8e9a8a-7c9d-493e-9aa6-5ba74e5b630d");
    private static readonly Guid s_TerrainTile2Guid = Guid.Parse("331e576c-eddd-d7cc-e553-23ef719f496d");
    private static readonly Guid s_TerrainTile3Guid = Guid.Parse("3fd9bf23-4923-f0e5-cd73-94920028ed67");
    private static readonly Guid s_ValleyRockSpeciesGuid =
        Guid.Parse("7b0f2e52-8b67-4e3d-bf0a-cbc42f622001");
    private static readonly Guid s_ShowcaseValleyBiomeGuid =
        Guid.Parse("c0a92f10-0eb9-4d24-b729-7d0f38313001");
    private static readonly Guid s_ValleyRockMeshGuid =
        Guid.Parse("9f57d9cc-2db6-4c85-ae7b-544338806e2c");
    private static readonly Guid s_ValleyRockMaterialGuid =
        Guid.Parse("4ac21c64-e984-4ed0-9e21-93878de5249e");

    [Fact]
    public void PackageShowcaseScene_LoadsClassicMeshAndDistinctMaterials()
    {
        string packageRoot = GetRepositoryFile(
            "Arisen", "Development", "PackageGame", "Local", "com.arisen.packagegame");
        string pipelineRoot = GetRepositoryFile(
            "Arisen", "Development", "PackageGame", "Local", "com.arisen.generic-renderpipeline");
        string cookedRoot = Path.Combine(
            Path.GetTempPath(),
            "ArisenShowcaseSceneTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, cookedRoot);
            string scenePath = Path.Combine(packageRoot, "Assets", "Scenes", "SmokeScene.arisenscene");
            string teapotPath = Path.Combine(packageRoot, "Assets", "Meshes", "UtahTeapot.obj");
            string pedestalPath = Path.Combine(packageRoot, "Assets", "Meshes", "ShowcasePedestal.obj");
            string groundPath = Path.Combine(packageRoot, "Assets", "Meshes", "ShowcaseGround.obj");
            string marbleTexturePath = Path.Combine(packageRoot, "Assets", "Textures", "Marble021_BaseColor.jpg");
            string marbleMaterialPath = Path.Combine(packageRoot, "Assets", "Materials", "ShowcaseMarble.arismaterial");
            string charcoalMaterialPath = Path.Combine(packageRoot, "Assets", "Materials", "ShowcaseCharcoal.arismaterial");
            string groundMaterialPath = Path.Combine(packageRoot, "Assets", "Materials", "ShowcaseGround.arismaterial");
            string shaderPath = Path.Combine(pipelineRoot, "Assets", "Shaders", "StandardLit.shader");
            string environmentShaderPath = Path.Combine(pipelineRoot, "Assets", "Shaders", "EnvironmentSky.hlsl");
            string defaultNormalPath = Path.Combine(pipelineRoot, "Assets", "Textures", "DefaultNormal.ppm");

            db.AddAsset(s_SceneGuid, "Scene", scenePath, "com.arisen.packagegame");
            db.AddAsset(s_TeapotMeshGuid, "Mesh", teapotPath, "com.arisen.packagegame");
            db.AddAsset(s_PedestalMeshGuid, "Mesh", pedestalPath, "com.arisen.packagegame");
            db.AddAsset(s_GroundMeshGuid, "Mesh", groundPath, "com.arisen.packagegame");
            db.AddAsset(s_MarbleTextureGuid, "Texture2D", marbleTexturePath, "com.arisen.packagegame");
            db.AddAsset(s_MarbleMaterialGuid, "Material", marbleMaterialPath, "com.arisen.packagegame");
            db.AddAsset(s_CharcoalMaterialGuid, "Material", charcoalMaterialPath, "com.arisen.packagegame");
            db.AddAsset(s_GroundMaterialGuid, "Material", groundMaterialPath, "com.arisen.packagegame");
            db.AddAsset(s_StandardLitShaderGuid, ShaderAssetCooker.ShaderSourceAssetType, shaderPath, "com.arisen.generic-renderpipeline");
            db.AddAsset(s_DefaultNormalGuid, "Texture2D", defaultNormalPath, "com.arisen.generic-renderpipeline");

            var entityManager = new EntityManager();
            var loadResult = SceneAssetLoader.LoadScene(
                db,
                new ArisenEngine.Core.Assets.AssetRef<ArisenEngine.Core.Assets.SceneSourceAsset>(
                    s_SceneGuid,
                    "Scene",
                    "com.arisen.packagegame"),
                entityManager);

            Assert.True(loadResult.Success, loadResult.Diagnostic);
            Assert.Equal(8, loadResult.EntityCount);
            Assert.Equal(1, loadResult.CameraCount);
            Assert.Equal(3, loadResult.MeshRendererCount);
            Assert.Equal(1, loadResult.DirectionalLightCount);
            Assert.Equal(1, loadResult.PointLightCount);
            Assert.Equal(1, loadResult.SpotLightCount);
            Assert.Equal(1, loadResult.EnvironmentCount);

            var namePool = entityManager.GetPool<NameComponent>();
            var transformPool = entityManager.GetPool<TransformComponent>();
            var nameComponents = namePool.GetRawComponentArray();
            var nameEntities = namePool.GetRawEntityArray();
            var groundIndex = Array.FindIndex(
                nameComponents,
                0,
                namePool.Count,
                component => component.Name == "Ground");
            Assert.True(groundIndex >= 0);
            Assert.Equal(new Vector3(20.0f, 1.0f, 20.0f), transformPool.Get(nameEntities[groundIndex]).Scale);

            var pointLight = entityManager.GetPool<PointLightComponent>().GetRawComponentArray()[0];
            Assert.Equal(new Vector3(0.56f, 0.74f, 1.0f), pointLight.Color);
            Assert.Equal(1.25f, pointLight.Intensity);
            Assert.Equal(3.2f, pointLight.Range);

            var spotLight = entityManager.GetPool<SpotLightComponent>().GetRawComponentArray()[0];
            Assert.Equal(new Vector3(1.0f, 0.82f, 0.58f), spotLight.Color);
            Assert.Equal(2.35f, spotLight.Intensity);
            Assert.Equal(4.4f, spotLight.Range);
            Assert.Equal(13.0f, spotLight.InnerConeAngleDegrees);
            Assert.Equal(29.0f, spotLight.OuterConeAngleDegrees);

            var marbleMaterial = MaterialAssetLoader.LoadSource(db, s_MarbleMaterialGuid);
            var charcoalMaterial = MaterialAssetLoader.LoadSource(db, s_CharcoalMaterialGuid);
            Assert.Contains("USE_TRIPLANAR", marbleMaterial.Shader.VariantKeywords ?? Array.Empty<string>());
            Assert.Contains("USE_TRIPLANAR", charcoalMaterial.Shader.VariantKeywords ?? Array.Empty<string>());
            Assert.Equal(
                0.24f,
                marbleMaterial.ScalarProperties.Single(
                    property => property.Name == MaterialPropertySlots.RoughnessFactor).Value);
            Assert.Equal(
                0.30f,
                charcoalMaterial.ScalarProperties.Single(
                    property => property.Name == MaterialPropertySlots.RoughnessFactor).Value);
            Assert.Equal(
                new Vector4(0.02f, 0.04f, 0.075f, 0.65f),
                charcoalMaterial.Vector4Properties.Single(
                    property => property.Name == MaterialPropertySlots.EmissiveFactor).Value);
            Assert.Equal(ECullModeFlagBits.CULL_MODE_NONE, marbleMaterial.RenderState.CullMode);
            Assert.Contains("LinearToSRgb", File.ReadAllText(shaderPath), StringComparison.Ordinal);
            Assert.Contains("BindlessImages", File.ReadAllText(environmentShaderPath), StringComparison.Ordinal);
            Assert.Contains("atan2", File.ReadAllText(environmentShaderPath), StringComparison.Ordinal);

            var cookedTexture = Texture2DAssetCooker.LoadOrCook(
                db,
                new Texture2DAsset(
                    s_MarbleTextureGuid,
                    "PackageGame/Marble021BaseColor",
                    Texture2DVariantKey.DefaultSRgb,
                    Texture2DSourceFormat.ImageFile));
            Assert.Equal(1024u, cookedTexture.Width);
            Assert.Equal(1024u, cookedTexture.Height);

            var cookedTeapot = MeshAssetCooker.LoadOrCook(
                db,
                new MeshAsset(
                    s_TeapotMeshGuid,
                    "PackageGame/UtahTeapot",
                    MeshVariantKey.Default,
                    MeshSourceFormat.WavefrontObj));
            Assert.True(cookedTeapot.VertexCount > 1_000);
            Assert.True(cookedTeapot.IndexCount > 5_000);

            string textureMd5 = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(marbleTexturePath)));
            Assert.Equal("3372195CC9762FD2818642C462FA76A2", textureMd5);
        }
        finally
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
    }

    [Fact]
    public void PackageLanternModelRoot_InspectsStableGeneratedChildren()
    {
        string packageRoot = GetRepositoryFile(
            "Arisen", "Development", "PackageGame", "Local", "com.arisen.packagegame");
        string cookedRoot = Path.Combine(
            Path.GetTempPath(),
            "ArisenLanternModelRootTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, cookedRoot);
            string modelPath = Path.Combine(packageRoot, "Assets", "Models", "Lantern", "Lantern.arismodel");

            db.AddAsset(s_LanternModelGuid, "Model", modelPath, "com.arisen.packagegame");

            var model = ModelSourceAssetLoader.LoadSource(db, s_LanternModelGuid);
            Assert.Equal(s_LanternModelGuid, model.Guid);
            Assert.Equal("Khronos Lantern Showcase", model.Name);
            Assert.Equal("Source/Lantern.glb", model.SourcePath.Replace('\\', '/'));
            Assert.Equal(ModelSourceFormat.GltfBinary, model.SourceFormat);
            Assert.Equal("Assets/Generated/Lantern", model.Import.OutputRoot.Replace('\\', '/'));
            Assert.Equal(0, model.Import.SceneIndex);
            Assert.Equal(1.0f, model.Import.UnitScale);
            Assert.Equal(Vector3.Zero, model.Import.RootTransform.Position);
            Assert.Equal(Quaternion.Identity, model.Import.RootTransform.Rotation);
            Assert.Equal(Vector3.One, model.Import.RootTransform.Scale);
            Assert.True(model.Import.EmitTextures);
            Assert.Equal(s_StandardLitShaderGuid, model.Shader.Guid);

            var outputRoot = ModelSourceAssetLoader.ResolveOutputRoot(modelPath, model.Import.OutputRoot);
            Assert.Equal(
                Path.Combine(packageRoot, "Assets", "Generated", "Lantern"),
                outputRoot);

            var firstPlan = ModelSourceAssetLoader.CreateGltfPlan(db, s_LanternModelGuid);
            var secondPlan = ModelSourceAssetLoader.CreateGltfPlan(db, s_LanternModelGuid);

            Assert.Equal(firstPlan.GeneratedChildren.Select(child => child.Metadata.Guid), secondPlan.GeneratedChildren.Select(child => child.Metadata.Guid));
            Assert.Equal(1, firstPlan.GeneratedChildren.Count(child => child.Kind == "scene"));
            Assert.Equal(3, firstPlan.GeneratedChildren.Count(child => child.Kind == "mesh"));
            Assert.Equal(1, firstPlan.GeneratedChildren.Count(child => child.Kind == "material"));
            Assert.Equal(4, firstPlan.GeneratedChildren.Count(child => child.Kind == "texture2d"));
            Assert.Empty(firstPlan.Warnings);
            Assert.Equal(s_LanternGeneratedSceneGuid, firstPlan.GeneratedChildren.Single(child => child.Kind == "scene").Metadata.Guid);
            Assert.Contains(firstPlan.GeneratedChildren, child => child.Metadata.Guid == s_LanternMesh0Guid);
            Assert.Contains(firstPlan.GeneratedChildren, child => child.Metadata.Guid == s_LanternMesh1Guid);
            Assert.Contains(firstPlan.GeneratedChildren, child => child.Metadata.Guid == s_LanternMesh2Guid);
            Assert.Contains(firstPlan.GeneratedChildren, child => child.Metadata.Guid == s_LanternMaterialGuid);
            Assert.Contains(firstPlan.GeneratedChildren, child => child.Metadata.Guid == s_LanternBaseColorTextureGuid);
            Assert.Contains(firstPlan.GeneratedChildren, child => child.Metadata.Guid == s_LanternMetallicRoughnessTextureGuid);
            Assert.Contains(firstPlan.GeneratedChildren, child => child.Metadata.Guid == s_LanternNormalTextureGuid);
            Assert.Contains(firstPlan.GeneratedChildren, child => child.Metadata.Guid == s_LanternEmissiveTextureGuid);

            var material = Assert.Single(firstPlan.Materials);
            Assert.NotNull(material.BaseColorTexture);
            Assert.NotNull(material.NormalTexture);
            Assert.NotNull(material.EmissiveTexture);
            Assert.NotNull(material.MetallicRoughnessTexture);
            Assert.Null(material.OcclusionTexture);
            Assert.Equal(GltfMaterialAlphaMode.Opaque, material.AlphaMode);
        }
        finally
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
    }

    [Fact]
    public void PackageLanternShowcaseScene_LoadsGeneratedModelChildren()
    {
        string packageRoot = GetRepositoryFile(
            "Arisen", "Development", "PackageGame", "Local", "com.arisen.packagegame");
        string pipelineRoot = GetRepositoryFile(
            "Arisen", "Development", "PackageGame", "Local", "com.arisen.generic-renderpipeline");
        string cookedRoot = Path.Combine(
            Path.GetTempPath(),
            "ArisenLanternShowcaseSceneTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, cookedRoot);
            string modelPath = Path.Combine(packageRoot, "Assets", "Models", "Lantern", "Lantern.arismodel");
            string showcaseScenePath = Path.Combine(packageRoot, "Assets", "Scenes", "LanternShowcaseScene.arisenscene");
            string generatedScenePath = Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Scenes", "Scene_0.arisenscene");
            string mesh0Path = Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Meshes", "Mesh_0.glb");
            string mesh1Path = Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Meshes", "Mesh_1.glb");
            string mesh2Path = Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Meshes", "Mesh_2.glb");
            string materialPath = Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Materials", "LanternPost_Mat.arismaterial");
            string baseColorPath = Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Textures", "BaseColor_0.png");
            string metallicRoughnessPath = Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Textures", "MetallicRoughness_1.png");
            string normalPath = Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Textures", "Normal_2.png");
            string emissivePath = Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Textures", "Emissive_3.png");
            string environmentSourcePath = Path.Combine(packageRoot, "Assets", "Textures", "BlueHourPanorama.ppm");
            string environmentPath = Path.Combine(packageRoot, "Assets", "Environments", "BlueHour.arienvironment");
            string groundPath = Path.Combine(packageRoot, "Assets", "Meshes", "ShowcaseGround.obj");
            string groundMaterialPath = Path.Combine(packageRoot, "Assets", "Materials", "ShowcaseGround.arismaterial");
            string shaderPath = Path.Combine(pipelineRoot, "Assets", "Shaders", "StandardLit.shader");
            string defaultNormalPath = Path.Combine(pipelineRoot, "Assets", "Textures", "DefaultNormal.ppm");

            db.AddAsset(s_LanternModelGuid, "Model", modelPath, "com.arisen.packagegame");
            db.AddAsset(s_LanternShowcaseSceneGuid, "Scene", showcaseScenePath, "com.arisen.packagegame");
            db.AddAsset(s_LanternGeneratedSceneGuid, "Scene", generatedScenePath, "com.arisen.packagegame");
            db.AddAsset(s_LanternMesh0Guid, "Mesh", mesh0Path, "com.arisen.packagegame");
            db.AddAsset(s_LanternMesh1Guid, "Mesh", mesh1Path, "com.arisen.packagegame");
            db.AddAsset(s_LanternMesh2Guid, "Mesh", mesh2Path, "com.arisen.packagegame");
            db.AddAsset(s_GroundMeshGuid, "Mesh", groundPath, "com.arisen.packagegame");
            db.AddAsset(s_LanternMaterialGuid, "Material", materialPath, "com.arisen.packagegame");
            db.AddAsset(s_GroundMaterialGuid, "Material", groundMaterialPath, "com.arisen.packagegame");
            db.AddAsset(s_LanternBaseColorTextureGuid, "Texture2D", baseColorPath, "com.arisen.packagegame");
            db.AddAsset(s_LanternMetallicRoughnessTextureGuid, "Texture2D", metallicRoughnessPath, "com.arisen.packagegame");
            db.AddAsset(s_LanternNormalTextureGuid, "Texture2D", normalPath, "com.arisen.packagegame");
            db.AddAsset(s_LanternEmissiveTextureGuid, "Texture2D", emissivePath, "com.arisen.packagegame");
            db.AddAsset(s_BlueHourSourceTextureGuid, "Texture2D", environmentSourcePath, "com.arisen.packagegame");
            db.AddAsset(s_BlueHourEnvironmentGuid, "EnvironmentTexture", environmentPath, "com.arisen.packagegame");
            db.AddAsset(s_StandardLitShaderGuid, ShaderAssetCooker.ShaderSourceAssetType, shaderPath, "com.arisen.generic-renderpipeline");
            db.AddAsset(s_DefaultNormalGuid, "Texture2D", defaultNormalPath, "com.arisen.generic-renderpipeline");

            var showcaseInspection = SceneAssetLoader.InspectScene(
                db,
                new AssetRef<SceneSourceAsset>(
                    s_LanternShowcaseSceneGuid,
                    "Scene",
                    "com.arisen.packagegame"));

            Assert.True(showcaseInspection.Success, showcaseInspection.Diagnostic);
            var downlight = Assert.Single(
                showcaseInspection.Entities,
                entity => entity.Name == "Lantern Downlight");
            Assert.NotNull(downlight.SpotLight);
            Assert.Equal(new Vector3(2.0f, 2.0f, 0.0f), downlight.Transform.Position);
            Assert.Equal(new Vector3(1.0f, 0.58f, 0.26f), downlight.SpotLight!.Color);
            Assert.Equal(4.0f, downlight.SpotLight.Intensity);
            Assert.Equal(4.2f, downlight.SpotLight.Range);
            Assert.Equal(24.0f, downlight.SpotLight.InnerConeAngleDegrees);
            Assert.Equal(42.0f, downlight.SpotLight.OuterConeAngleDegrees);
            var downlightDirection = Vector3.Transform(Vector3.UnitZ, downlight.Transform.Rotation);
            Assert.Equal(0.0f, downlightDirection.X, precision: 5);
            Assert.Equal(-1.0f, downlightDirection.Y, precision: 5);
            Assert.Equal(0.0f, downlightDirection.Z, precision: 5);

            var generatedInspection = SceneAssetLoader.InspectScene(
                db,
                new AssetRef<SceneSourceAsset>(
                    s_LanternGeneratedSceneGuid,
                    "Scene",
                    "com.arisen.packagegame"));

            Assert.True(generatedInspection.Success, generatedInspection.Diagnostic);
            Assert.Equal(3, generatedInspection.MeshRendererCount);
            Assert.Equal(0, generatedInspection.CameraCount);
            Assert.All(generatedInspection.Entities, entity =>
            {
                Assert.NotNull(entity.MeshRenderer);
                Assert.Equal(s_LanternMaterialGuid, entity.MeshRenderer!.Material.Guid);
                Assert.True(entity.MeshRenderer.Mesh.IsResolved, entity.MeshRenderer.Mesh.Diagnostic);
                Assert.True(entity.MeshRenderer.Material.IsResolved, entity.MeshRenderer.Material.Diagnostic);
            });

            var sceneRef = new AssetRef<SceneSourceAsset>(
                s_LanternShowcaseSceneGuid,
                "Scene",
                "com.arisen.packagegame");
            var entityManager = new EntityManager();
            var loadResult = SceneAssetLoader.LoadScene(db, sceneRef, entityManager);

            Assert.True(loadResult.Success, loadResult.Diagnostic);
            Assert.Equal(9, loadResult.EntityCount);
            Assert.Equal(1, loadResult.CameraCount);
            Assert.Equal(4, loadResult.MeshRendererCount);
            Assert.Equal(1, loadResult.DirectionalLightCount);
            Assert.Equal(1, loadResult.PointLightCount);
            Assert.Equal(1, loadResult.SpotLightCount);
            Assert.Equal(1, loadResult.EnvironmentCount);

            var cookedScene = SceneAssetCooker.Cook(db, sceneRef);
            Assert.Equal(9, cookedScene.EntityCount);
            Assert.Equal(7, cookedScene.AssetReferenceCount);
            var cookedEntityManager = new EntityManager();
            var cookedLoadResult = SceneAssetCooker.LoadCooked(db, sceneRef, cookedEntityManager);
            Assert.True(cookedLoadResult.Success, cookedLoadResult.Diagnostic);
            Assert.Equal(loadResult.EntityCount, cookedLoadResult.EntityCount);
            Assert.Equal(loadResult.MeshRendererCount, cookedLoadResult.MeshRendererCount);
            Assert.Equal(loadResult.EnvironmentCount, cookedLoadResult.EnvironmentCount);

            var meshRenderers = entityManager.GetPool<MeshRendererComponent>().GetRawComponentArray();
            Assert.Contains(meshRenderers.Take(4), renderer => renderer.MeshGuid == s_LanternMesh0Guid);
            Assert.Contains(meshRenderers.Take(4), renderer => renderer.MeshGuid == s_LanternMesh1Guid);
            Assert.Contains(meshRenderers.Take(4), renderer => renderer.MeshGuid == s_LanternMesh2Guid);
            Assert.Contains(meshRenderers.Take(4), renderer => renderer.MeshGuid == s_GroundMeshGuid);

            var environment = entityManager.GetPool<SceneEnvironmentComponent>().GetRawComponentArray()[0];
            Assert.Equal(s_BlueHourEnvironmentGuid, environment.EnvironmentTextureGuid);
            Assert.Equal(1.15f, environment.Exposure);

            var cookedEnvironment = EnvironmentTextureAssetCooker.LoadOrCook(
                db,
                s_BlueHourEnvironmentGuid);
            Assert.Equal(16u, cookedEnvironment.Width);
            Assert.Equal(8u, cookedEnvironment.Height);
            Assert.Equal(EnvironmentTextureCookedFormat.R16G16B16A16SFloat, cookedEnvironment.Format);

            var material = MaterialAssetLoader.LoadSource(db, s_LanternMaterialGuid);
            Assert.Equal("LanternPost_Mat", material.Name);
            Assert.Equal(s_StandardLitShaderGuid, material.Shader.Guid);
            Assert.Equal(new[] { "USE_NORMAL_MAP" }, material.Shader.VariantKeywords);
            Assert.Equal(ECullModeFlagBits.CULL_MODE_BACK_BIT, material.RenderState.CullMode);
            Assert.Equal(EFrontFace.FRONT_FACE_COUNTER_CLOCKWISE, material.RenderState.FrontFace);
            Assert.Equal(4, material.Texture2DRefs.Count);
            Assert.Equal(
                Texture2DColorSpace.SRgb,
                material.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.BaseColor).Texture.Variant.ColorSpace);
            Assert.Equal(
                Texture2DColorSpace.Linear,
                material.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.Normal).Texture.Variant.ColorSpace);
            Assert.Equal(
                Texture2DColorSpace.SRgb,
                material.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.Emissive).Texture.Variant.ColorSpace);
            Assert.Equal(
                Texture2DColorSpace.Linear,
                material.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.MetallicRoughness).Texture.Variant.ColorSpace);
            Assert.Equal(
                s_LanternMetallicRoughnessTextureGuid,
                material.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.MetallicRoughness).Texture.Guid);
            Assert.All(material.Texture2DRefs, texture =>
            {
                Assert.True(texture.Texture.Variant.GenerateMipMaps);
                Assert.Equal(MaterialTextureMipmapMode.Linear, texture.ResolvedSampler.MipmapMode);
            });

            AssertGeneratedMeshCooks(db, s_LanternMesh0Guid, "Lantern/Mesh_0");
            AssertGeneratedMeshCooks(db, s_LanternMesh1Guid, "Lantern/Mesh_1");
            AssertGeneratedMeshCooks(db, s_LanternMesh2Guid, "Lantern/Mesh_2");

            MaterialTexture2DRef baseColorTexture = material.Texture2DRefs.Single(
                texture => texture.Name == MaterialTextureSlots.BaseColor);
            var cookedBaseColor = Texture2DAssetCooker.LoadOrCook(db, baseColorTexture.Texture);
            Assert.True(cookedBaseColor.Width > 0);
            Assert.True(cookedBaseColor.Height > 0);
            Assert.True(cookedBaseColor.MipCount > 1);
        }
        finally
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
    }

    [Fact]
    public void PackageShowcaseTerrain_CooksTwoByTwoTilesWithBitExactSharedBorders()
    {
        const string packageId = "com.arisen.packagegame";
        string packageRoot = GetRepositoryFile(
            "Arisen", "Development", "PackageGame", "Local", packageId);
        string cookedRoot = Path.Combine(
            Path.GetTempPath(),
            "ArisenShowcaseTerrainTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, cookedRoot);
            AddTerrainAssets(db, packageRoot);
            var rootRef = new AssetRef<TerrainRootSourceAsset>(
                s_TerrainRootGuid,
                TerrainAssetTypes.Root,
                packageId);

            CookedTerrainRootArtifact artifact = TerrainRootAssetCooker.Cook(db, rootRef);
            Assert.Equal(4, artifact.TileCount);
            Assert.Equal(
                4,
                artifact.Dependencies.Count(
                    dependency => dependency.AssetType == TerrainAssetTypes.Tile));
            Assert.Equal(
                s_TerrainTile1Guid,
                TerrainTileIdentity.CreateGuid(
                    s_TerrainRootGuid,
                    packageId,
                    new TerrainTileCoordinate(1, 0)));

            Assert.True(
                TerrainRootAssetCooker.TryLoadCooked(
                    db,
                    rootRef,
                    out CookedTerrainRoot root,
                    out string rootDiagnostic),
                rootDiagnostic);
            Assert.Equal(33, root.HeightSourceWidth);
            Assert.Equal(33, root.HeightSourceHeight);
            Assert.Equal(2, root.SourceSchemaVersion);
            Assert.Equal(3, root.Layers.Count);
            Assert.Equal(
                9,
                artifact.Dependencies.Count(
                    dependency => dependency.AssetType == "Texture2D"));
            Assert.Equal(
                3,
                artifact.Dependencies.Count(
                    dependency => dependency.Variant == TerrainTextureCookVariants.Albedo));
            Assert.Equal(
                3,
                artifact.Dependencies.Count(
                    dependency => dependency.Variant == TerrainTextureCookVariants.Normal));
            Assert.Equal(
                3,
                artifact.Dependencies.Count(
                    dependency => dependency.Variant == TerrainTextureCookVariants.Orm));
            Assert.Equal(
                s_TerrainTile1Guid,
                root.Tiles.Single(tile => tile.Coordinate == new TerrainTileCoordinate(0, 0))
                    .Neighbors.PositiveX);
            Assert.Equal(
                s_TerrainTile0Guid,
                root.Tiles.Single(tile => tile.Coordinate == new TerrainTileCoordinate(1, 0))
                    .Neighbors.NegativeX);
            Assert.Equal(
                s_TerrainTile2Guid,
                root.Tiles.Single(tile => tile.Coordinate == new TerrainTileCoordinate(0, 0))
                    .Neighbors.PositiveZ);
            Assert.Equal(
                s_TerrainTile0Guid,
                root.Tiles.Single(tile => tile.Coordinate == new TerrainTileCoordinate(0, 1))
                    .Neighbors.NegativeZ);

            CookedTerrainTile tile0 = LoadTerrainTile(db, s_TerrainTile0Guid);
            CookedTerrainTile tile1 = LoadTerrainTile(db, s_TerrainTile1Guid);
            CookedTerrainTile tile2 = LoadTerrainTile(db, s_TerrainTile2Guid);
            CookedTerrainTile tile3 = LoadTerrainTile(db, s_TerrainTile3Guid);
            Assert.Equal(new TerrainTileCoordinate(0, 0), tile0.Coordinate);
            Assert.Equal(new TerrainTileCoordinate(1, 0), tile1.Coordinate);
            Assert.Equal(new TerrainTileCoordinate(0, 1), tile2.Coordinate);
            Assert.Equal(new TerrainTileCoordinate(1, 1), tile3.Coordinate);
            Assert.Equal(tile0.WorldPlacement.Y, tile1.WorldPlacement.Y);
            Assert.Equal(tile0.WorldPlacement.Z, tile1.WorldPlacement.Z);
            Assert.Equal(
                tile0.WorldPlacement.X +
                ((tile0.Resolution - 1) * tile0.SampleSpacing.X),
                tile1.WorldPlacement.X);
            Assert.Equal(tile0.WorldPlacement.X, tile2.WorldPlacement.X);
            Assert.Equal(
                tile0.WorldPlacement.Z +
                ((tile0.Resolution - 1) * tile0.SampleSpacing.Z),
                tile2.WorldPlacement.Z);
            CookedTerrainTile[] tiles = [tile0, tile1, tile2, tile3];
            bool sawRock = false;
            bool sawPath = false;
            bool sawBlend = false;
            foreach (CookedTerrainTile tile in tiles)
            {
                Assert.Equal(3, tile.LayerCount);
                for (int z = 0; z < tile.Resolution; z++)
                {
                    for (int x = 0; x < tile.Resolution; x++)
                    {
                        int nonZeroLayers = 0;
                        int weightSum = 0;
                        for (int layer = 0; layer < TerrainCookedFormat.WeightChannelCount; layer++)
                        {
                            byte weight = tile.GetLayerWeight(x, z, layer);
                            weightSum += weight;
                            nonZeroLayers += weight == 0 ? 0 : 1;
                            sawRock |= layer == 1 && weight != 0;
                            sawPath |= layer == 2 && weight != 0;
                        }

                        Assert.Equal(byte.MaxValue, weightSum);
                        sawBlend |= nonZeroLayers > 1;
                    }
                }
            }

            Assert.True(sawRock, "The authored fixture never contributes its rock layer.");
            Assert.True(sawPath, "The authored fixture never contributes its path layer.");
            Assert.True(sawBlend, "The authored fixture contains no blended layer samples.");
            TerrainTileAssetCooker.ValidateSharedBorders(tiles);
        }
        finally
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
    }

    [Fact]
    public void PackageShowcaseVegetation_BakesOneCanonicalTerrainBackedClusterPage()
    {
        const string packageId = "com.arisen.packagegame";
        const string pipelinePackageId = "com.arisen.generic-renderpipeline";
        string packageRoot = GetRepositoryFile(
            "Arisen", "Development", "PackageGame", "Local", packageId);
        string pipelineRoot = GetRepositoryFile(
            "Arisen", "Development", "PackageGame", "Local", pipelinePackageId);
        string cookedRoot = Path.Combine(
            Path.GetTempPath(),
            "ArisenShowcaseVegetationTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, cookedRoot);
            AddTerrainAssets(db, packageRoot);
            db.AddAsset(
                s_ValleyRockSpeciesGuid,
                VegetationAssetTypes.Species,
                Path.Combine(
                    packageRoot,
                    "Assets",
                    "Vegetation",
                    "ValleyRock.arivegetationspecies"),
                packageId);
            db.AddAsset(
                s_ShowcaseValleyBiomeGuid,
                VegetationAssetTypes.Biome,
                Path.Combine(
                    packageRoot,
                    "Assets",
                    "Vegetation",
                    "ShowcaseValley.arivegetationbiome"),
                packageId);
            db.AddAsset(
                s_ValleyRockMeshGuid,
                "Mesh",
                Path.Combine(pipelineRoot, "Assets", "Meshes", "FacetedCrystal.obj"),
                pipelinePackageId);
            db.AddAsset(
                s_ValleyRockMaterialGuid,
                "Material",
                Path.Combine(
                    pipelineRoot,
                    "Assets",
                    "Materials",
                    "StandardLitMaterial.arismaterial"),
                pipelinePackageId);

            var terrainRootRef = new AssetRef<TerrainRootSourceAsset>(
                s_TerrainRootGuid,
                TerrainAssetTypes.Root,
                packageId);
            TerrainRootAssetCooker.Cook(db, terrainRootRef);
            Assert.True(
                TerrainRootAssetCooker.TryLoadCooked(
                    db,
                    terrainRootRef,
                    out CookedTerrainRoot terrainRoot,
                    out string terrainDiagnostic),
                terrainDiagnostic);
            CookedTerrainTile[] tiles =
            [
                LoadTerrainTile(db, s_TerrainTile0Guid),
                LoadTerrainTile(db, s_TerrainTile1Guid),
                LoadTerrainTile(db, s_TerrainTile2Guid),
                LoadTerrainTile(db, s_TerrainTile3Guid)
            ];

            var speciesRef = new AssetRef<VegetationSpeciesSourceAsset>(
                s_ValleyRockSpeciesGuid,
                VegetationAssetTypes.Species,
                packageId);
            var biomeRef = new AssetRef<VegetationBiomeSourceAsset>(
                s_ShowcaseValleyBiomeGuid,
                VegetationAssetTypes.Biome,
                packageId);
            VegetationSpeciesAssetCooker.Cook(db, speciesRef);
            VegetationBiomeAssetCooker.Cook(db, biomeRef);
            Assert.True(
                VegetationSpeciesAssetCooker.TryLoadCooked(
                    db,
                    speciesRef,
                    out CookedVegetationSpecies species,
                    out string speciesDiagnostic),
                speciesDiagnostic);
            Assert.True(
                VegetationBiomeAssetCooker.TryLoadCooked(
                    db,
                    biomeRef,
                    out CookedVegetationBiome biome,
                    out string biomeDiagnostic),
                biomeDiagnostic);

            var partition = new WorldPartitionSettings(
                new WorldPosition(-256.0, -64.0, -256.0),
                new WorldPosition(256.0, 128.0, 256.0),
                LoadRadius: 1,
                UnloadHysteresis: 1,
                MaxActiveCells: 16);
            VegetationScatterBakeResult result = VegetationScatterBaker.Build(
                db,
                new VegetationScatterCookRequest(
                    s_LanternWorldGuid,
                    biomeRef,
                    terrainRootRef,
                    partition,
                    new WorldCellKey(new WorldCellCoordinate(1, 0, 0), "surface"),
                    "valley-rock",
                    UnscaledConservativeRadius: 1.75f,
                    Exclusions: []));
            Assert.Equal(1, result.Metrics.AcceptedCount);
            Assert.Equal(
                Guid.Parse("cadf9261-1ffe-85e6-23df-b62a204da08d"),
                result.ClusterMetadata.Guid);
            Assert.Equal(
                Guid.Parse("dd3ef159-5eea-0247-97ac-4add9c3d4994"),
                result.PageMetadata[0].Guid);
            string placementHash = Convert.ToHexString(result.PlacementContentHash);
            Assert.Equal("99168D344D128746B87BC2F3900698CB", placementHash[..32]);
            Assert.Equal("79EFE1671F9C286C61D4151E99468A9A", placementHash[32..]);
            Assert.Single(result.Cluster.Pages);
            Assert.Equal(result.Metrics.AcceptedCount, result.Cluster.Pages[0].Instances.Count);

            string generatedRoot = Path.Combine(cookedRoot, "GeneratedSources");
            Directory.CreateDirectory(generatedRoot);
            string clusterSource = Path.Combine(generatedRoot, "ShowcaseValley.cluster.generated");
            string pageSource = Path.Combine(generatedRoot, "ShowcaseValley.page.generated");
            File.WriteAllText(clusterSource, "generated vegetation cluster identity");
            File.WriteAllText(pageSource, "generated vegetation page identity");
            db.AddAsset(
                result.ClusterMetadata.Guid,
                VegetationAssetTypes.Cluster,
                clusterSource,
                packageId);
            db.AddAsset(
                result.PageMetadata[0].Guid,
                VegetationAssetTypes.InstancePage,
                pageSource,
                packageId);
            CookedVegetationClusterArtifact artifact = VegetationClusterAssetCooker.Cook(
                db,
                result.Cluster);
            Assert.True(
                VegetationClusterAssetCooker.TryLoadCooked(
                    db,
                    new AssetRef<VegetationClusterSourceAsset>(
                        artifact.ClusterGuid,
                        VegetationAssetTypes.Cluster,
                        packageId),
                    out CookedVegetationCluster cluster,
                    out string clusterDiagnostic),
                clusterDiagnostic);
            CookedVegetationInstancePageReference page = Assert.Single(cluster.Pages);
            Assert.Equal(result.PageMetadata[0].Guid, page.Guid);
            Assert.Equal(result.Metrics.AcceptedCount, page.InstanceCount);
            Assert.True(
                VegetationInstancePageAssetCooker.TryLoadCooked(
                    db,
                    new AssetRef<VegetationInstancePageSourceAsset>(
                        page.Guid,
                        VegetationAssetTypes.InstancePage,
                        packageId),
                    cluster.Guid,
                    out CookedVegetationInstancePage loadedPage,
                    out string pageDiagnostic),
                pageDiagnostic);
            Assert.Equal(page.InstanceCount, loadedPage.Instances.Count);
            Assert.Equal(page.Origin, loadedPage.Origin);
            Assert.Equal(page.Bounds, loadedPage.Bounds);
            Assert.Equal(SHA256.HashSizeInBytes, result.PlacementContentHash.Length);
            string pageHash = Convert.ToHexString(page.ContentHash);
            string clusterHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(artifact.Path)));
            Assert.Equal("B3CB5AB88DEDEEB59CC0366AB2165DAB", pageHash[..32]);
            Assert.Equal("2B25922AB65D5B3FDE4B3A63C3C341F4", pageHash[32..]);
            Assert.Equal("073268F1E53948B89A6FCFA5A99708A7", clusterHash[..32]);
            Assert.Equal("10F133D3BCCFFBAA75CE592694F94BB5", clusterHash[32..]);

            VegetationScatterBakeResult reordered = VegetationScatterBaker.Build(
                new VegetationScatterBakeDescriptor(
                    s_LanternWorldGuid,
                    biome,
                    species,
                    terrainRoot,
                    tiles.Reverse().ToArray(),
                    partition,
                    new WorldCellKey(new WorldCellCoordinate(1, 0, 0), "surface"),
                    "valley-rock",
                    UnscaledConservativeRadius: 1.75f,
                    Exclusions: []));
            Assert.Equal(result.ClusterMetadata.Guid, reordered.ClusterMetadata.Guid);
            Assert.Equal(result.PageMetadata[0].Guid, reordered.PageMetadata[0].Guid);
            Assert.Equal(result.PlacementContentHash, reordered.PlacementContentHash);
            Assert.Equal(
                VegetationClusterAssetCooker.WritePayload(cluster),
                File.ReadAllBytes(artifact.Path));
        }
        finally
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
    }

    [Fact]
    public void PackageLanternWorld_KeepsGlobalRenderComponentsOutOfCells()
    {
        string packageRoot = GetRepositoryFile(
            "Arisen", "Development", "PackageGame", "Local", "com.arisen.packagegame");
        string cookedRoot = Path.Combine(
            Path.GetTempPath(),
            "ArisenLanternWorldContentTests",
            Guid.NewGuid().ToString("N"));
        var terrainCodec = new TerrainTileSceneComponentCodec();
        SceneComponentExtensionRegistry.Shared.Register(terrainCodec);

        try
        {
            var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, cookedRoot);
            db.AddAsset(
                s_LanternWorldGuid,
                "World",
                Path.Combine(packageRoot, "Assets", "Worlds", "LanternWorld.arisenworld"),
                "com.arisen.packagegame");
            db.AddAsset(
                s_LanternShowcaseSceneGuid,
                "Scene",
                Path.Combine(packageRoot, "Assets", "Scenes", "LanternShowcaseScene.arisenscene"),
                "com.arisen.packagegame");
            db.AddAsset(
                s_TeapotWestCellSceneGuid,
                "Scene",
                Path.Combine(packageRoot, "Assets", "Scenes", "TeapotWestCell.arisenscene"),
                "com.arisen.packagegame");
            db.AddAsset(
                s_TeapotCenterCellSceneGuid,
                "Scene",
                Path.Combine(packageRoot, "Assets", "Scenes", "TeapotCenterCell.arisenscene"),
                "com.arisen.packagegame");
            db.AddAsset(
                s_LanternGeneratedSceneGuid,
                "Scene",
                Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Scenes", "Scene_0.arisenscene"),
                "com.arisen.packagegame");

            AddCellRenderAssets(db, packageRoot);

            WorldDescriptorLoadResult loaded = WorldDescriptorLoader.LoadSource(
                db,
                new AssetRef<WorldSourceAsset>(
                    s_LanternWorldGuid,
                    "World",
                    "com.arisen.packagegame"));

            Assert.True(loaded.Success, loaded.Diagnostic);
            WorldDescriptor descriptor = Assert.IsType<WorldDescriptor>(loaded.Descriptor);
            Assert.Equal(s_LanternShowcaseSceneGuid, descriptor.PersistentScene.Guid);
            Assert.Equal(3, descriptor.Cells.Count);
            Assert.DoesNotContain(descriptor.Cells, cell => cell.Scene.Guid == s_SceneGuid);
            WorldCellDescriptor westCell = Assert.Single(
                descriptor.Cells,
                cell => cell.Key.Coordinate == new WorldCellCoordinate(-1, 0, 0));
            WorldCellDescriptor centerCell = Assert.Single(
                descriptor.Cells,
                cell => cell.Key.Coordinate == new WorldCellCoordinate(0, 0, 0));
            WorldCellDescriptor lanternCell = Assert.Single(
                descriptor.Cells,
                cell => cell.Key.Coordinate == new WorldCellCoordinate(1, 0, 0));
            Assert.Equal(
                new WorldBounds(
                    new WorldPosition(-385.5, -0.25, -129.5),
                    new WorldPosition(-382.5, 1.75, -126.5)),
                westCell.FocusBounds);
            Assert.Equal(
                new WorldBounds(
                    new WorldPosition(-129.5, -0.25, -129.5),
                    new WorldPosition(-126.5, 1.75, -126.5)),
                centerCell.FocusBounds);
            Assert.Null(lanternCell.FocusBounds);

            foreach (WorldCellDescriptor cell in descriptor.Cells)
            {
                SceneInspectionResult inspection = SceneAssetLoader.InspectScene(
                    db,
                    new AssetRef<SceneSourceAsset>(
                        cell.Scene.Guid,
                        "Scene",
                        cell.Scene.PackageId));
                Assert.True(inspection.Success, inspection.Diagnostic);
                Assert.Equal(0, inspection.CameraCount);
                Assert.Equal(0, inspection.DirectionalLightCount);
                Assert.Equal(0, inspection.PointLightCount);
                Assert.Equal(0, inspection.SpotLightCount);
                Assert.Equal(0, inspection.EnvironmentCount);
            }
        }
        finally
        {
            SceneComponentExtensionRegistry.Shared.Unregister(terrainCodec);
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
    }

    private static void AddCellRenderAssets(TestAssetDatabase db, string packageRoot)
    {
        db.AddAsset(s_TeapotMeshGuid, "Mesh", Path.Combine(packageRoot, "Assets", "Meshes", "UtahTeapot.obj"), "com.arisen.packagegame");
        db.AddAsset(s_PedestalMeshGuid, "Mesh", Path.Combine(packageRoot, "Assets", "Meshes", "ShowcasePedestal.obj"), "com.arisen.packagegame");
        db.AddAsset(s_GroundMeshGuid, "Mesh", Path.Combine(packageRoot, "Assets", "Meshes", "ShowcaseGround.obj"), "com.arisen.packagegame");
        db.AddAsset(s_MarbleMaterialGuid, "Material", Path.Combine(packageRoot, "Assets", "Materials", "ShowcaseMarble.arismaterial"), "com.arisen.packagegame");
        db.AddAsset(s_CharcoalMaterialGuid, "Material", Path.Combine(packageRoot, "Assets", "Materials", "ShowcaseCharcoal.arismaterial"), "com.arisen.packagegame");
        db.AddAsset(s_GroundMaterialGuid, "Material", Path.Combine(packageRoot, "Assets", "Materials", "ShowcaseGround.arismaterial"), "com.arisen.packagegame");
        db.AddAsset(s_LanternMesh0Guid, "Mesh", Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Meshes", "Mesh_0.glb"), "com.arisen.packagegame");
        db.AddAsset(s_LanternMesh1Guid, "Mesh", Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Meshes", "Mesh_1.glb"), "com.arisen.packagegame");
        db.AddAsset(s_LanternMesh2Guid, "Mesh", Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Meshes", "Mesh_2.glb"), "com.arisen.packagegame");
        db.AddAsset(s_LanternMaterialGuid, "Material", Path.Combine(packageRoot, "Assets", "Generated", "Lantern", "Materials", "LanternPost_Mat.arismaterial"), "com.arisen.packagegame");
        db.AddAsset(s_BlueHourEnvironmentGuid, "EnvironmentTexture", Path.Combine(packageRoot, "Assets", "Environments", "BlueHour.arienvironment"), "com.arisen.packagegame");
        AddTerrainAssets(db, packageRoot);
    }

    private static void AddTerrainAssets(TestAssetDatabase db, string packageRoot)
    {
        const string packageId = "com.arisen.packagegame";
        db.AddAsset(s_TerrainRootGuid, TerrainAssetTypes.Root, Path.Combine(packageRoot, "Assets", "Terrain", "ShowcaseValley.aristerrain"), packageId);
        db.AddAsset(s_TerrainLayerSetGuid, TerrainAssetTypes.LayerSet, Path.Combine(packageRoot, "Assets", "Terrain", "ShowcaseValley.ariterrainlayers"), packageId);
        db.AddAsset(s_TerrainTile0Guid, TerrainAssetTypes.Tile, Path.Combine(packageRoot, "Assets", "Terrain", "Generated", "ShowcaseValley_0_0.ariterraingenerated"), packageId);
        db.AddAsset(s_TerrainTile1Guid, TerrainAssetTypes.Tile, Path.Combine(packageRoot, "Assets", "Terrain", "Generated", "ShowcaseValley_1_0.ariterraingenerated"), packageId);
        db.AddAsset(s_TerrainTile2Guid, TerrainAssetTypes.Tile, Path.Combine(packageRoot, "Assets", "Terrain", "Generated", "ShowcaseValley_0_1.ariterraingenerated"), packageId);
        db.AddAsset(s_TerrainTile3Guid, TerrainAssetTypes.Tile, Path.Combine(packageRoot, "Assets", "Terrain", "Generated", "ShowcaseValley_1_1.ariterraingenerated"), packageId);
    }

    private static CookedTerrainTile LoadTerrainTile(TestAssetDatabase db, Guid tileGuid)
    {
        Assert.True(
            TerrainTileAssetCooker.TryLoadCooked(
                db,
                new AssetRef<TerrainTileSourceAsset>(
                    tileGuid,
                    TerrainAssetTypes.Tile,
                    "com.arisen.packagegame"),
                s_TerrainRootGuid,
                s_TerrainLayerSetGuid,
                out CookedTerrainTile tile,
                out string diagnostic),
            diagnostic);
        return tile;
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

    private static void AssertGeneratedMeshCooks(TestAssetDatabase db, Guid meshGuid, string name)
    {
        var cooked = MeshAssetCooker.LoadOrCook(
            db,
            new MeshAsset(
                meshGuid,
                name,
                MeshVariantKey.Default,
                MeshSourceFormat.GltfBinary));

        Assert.True(cooked.IsValid);
        Assert.True(cooked.VertexCount > 0);
        Assert.True(cooked.IndexCount > 0);
        Assert.Equal(1u, cooked.SubmeshCount);
    }
}
