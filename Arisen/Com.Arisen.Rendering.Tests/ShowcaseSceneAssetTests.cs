using System.Security.Cryptography;
using System.Numerics;
using Arisen.Native.RHI;
using ArisenEngine.Core.ECS;
using ArisenEngine.Rendering;
using ArisenEngine.Rendering.Resources;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

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
            var db = new TestAssetDatabase(cookedRoot);
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
            Assert.Contains("LinearToSRgb", File.ReadAllText(environmentShaderPath), StringComparison.Ordinal);

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
