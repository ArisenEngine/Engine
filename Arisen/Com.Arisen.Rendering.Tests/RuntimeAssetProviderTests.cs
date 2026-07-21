using System.Buffers.Binary;
using Arisen.Native.RHI;
using ArisenEngine.Core.Assets;
using ArisenEngine.Rendering;
using ArisenEngine.Rendering.Resources;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RuntimeAssetProviderTests
{
    [Fact]
    public void ShaderRecipeRegistry_AcceptsEquivalentRecipesAndRejectsHiddenInputConflicts()
    {
        Guid shaderGuid = Guid.Parse("91000000-0000-0000-0000-000000000001");
        var stage = new ShaderStageAsset("Fragment", EProgramStage.Fragment, "PSMain");
        var original = new ShaderAsset(
            shaderGuid,
            "Tests/Original",
            [stage],
            ShaderVariantKey.VulkanDebug,
            Defines: ["ARISEN_TEST=1"],
            Includes: ["Assets/Shaders/Common"],
            VariantKeywords: ["USE_FOG"]);
        var equivalent = original with { Name = "Tests/Equivalent" };
        string variant = original.Variant.GetCookedVariant(
            stage.EntryPoint,
            original.VariantKeywords);
        var registry = new RuntimeShaderCookRecipeRegistry();

        registry.RegisterRecipe(original, stage.Name, "package.first");
        registry.RegisterRecipe(equivalent, stage.Name, "package.second");

        Assert.True(registry.TryGetRecipe(shaderGuid, variant, out RuntimeShaderCookRecipe recipe));
        Assert.Same(original, recipe.Shader);
        Assert.Equal("package.first", recipe.OwnerId);

        InvalidOperationException defineConflict = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterRecipe(
                original with { Defines = ["ARISEN_TEST=2"] },
                stage.Name,
                "package.define-conflict"));
        Assert.Contains("different stage, variant, define, or include inputs", defineConflict.Message);

        InvalidOperationException includeConflict = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterRecipe(
                original with { Includes = ["Assets/Shaders/Different"] },
                stage.Name,
                "package.include-conflict"));
        Assert.Contains("different stage, variant, define, or include inputs", includeConflict.Message);
    }

    [Fact]
    public void SceneProvider_EmitsTypedDependenciesWithIndexedPackageOwnership()
    {
        using var temp = new TempDirectory();
        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(temp.Path, "Cooked"));
        Guid sceneGuid = Guid.Parse("92000000-0000-0000-0000-000000000001");
        Guid meshGuid = Guid.Parse("92000000-0000-0000-0000-000000000002");
        Guid materialGuid = Guid.Parse("92000000-0000-0000-0000-000000000003");
        Guid environmentGuid = Guid.Parse("92000000-0000-0000-0000-000000000004");
        string scenePath = Path.Combine(temp.Path, "Assets", "ProviderScene.arisenscene");
        temp.Write("Assets/ProviderScene.arisenscene", SceneTestSource.MigrateLegacy(sceneGuid, scenePath, $$"""
            Version: 1
            Name: Provider Scene
            Entities:
            - Name: Rendered Mesh
              MeshRenderer:
                Mesh:
                  Guid: {{meshGuid:D}}
                Material:
                  Guid: {{materialGuid:D}}
            - Name: Environment
              Environment:
                EnvironmentTexture:
                  Guid: {{environmentGuid:D}}
            """));
        string meshPath = temp.Write("Assets/ProviderMesh.armesh", string.Empty);
        string materialPath = temp.Write("Assets/ProviderMaterial.arismaterial", string.Empty);
        string environmentPath = temp.Write("Assets/ProviderEnvironment.arienvironment", string.Empty);
        database.AddAsset(sceneGuid, "Scene", scenePath, "com.game.world");
        database.AddAsset(meshGuid, "Mesh", meshPath, "com.game.geometry");
        database.AddAsset(materialGuid, "Material", materialPath, "com.game.materials");
        database.AddAsset(
            environmentGuid,
            "EnvironmentTexture",
            environmentPath,
            "com.game.environment");
        var provider = new SceneRuntimeAssetCooker(database);

        RuntimeAssetCookerOutput output = provider.Cook(
            CreateContext(temp),
            new RuntimeAssetCookRequest(
                sceneGuid,
                "com.game.world",
                "Scene"));

        Assert.Equal(SceneAssetCooker.RuntimeVariant, output.Artifact.Variant);
        Assert.Equal(SceneAssetCooker.CookedFormatVersion, output.Artifact.FormatVersion);
        Assert.Equal(
            $"com.game.world/{sceneGuid:N}/{SceneAssetCooker.RuntimeVariant}" +
            SceneAssetCooker.CookedExtension,
            output.Artifact.OutputRelativePath);
        Assert.Collection(
            output.Dependencies,
            dependency => AssertDependency(
                dependency,
                meshGuid,
                "com.game.geometry",
                "Mesh",
                required: true),
            dependency => AssertDependency(
                dependency,
                materialGuid,
                "com.game.materials",
                "Material",
                required: false),
            dependency => AssertDependency(
                dependency,
                environmentGuid,
                "com.game.environment",
                "EnvironmentTexture",
                required: false));
    }

    [Fact]
    public void GenericRenderPipelineProvider_CooksDeterministicSettingsAndCompleteDependencies()
    {
        using var temp = new TempDirectory();
        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(temp.Path, "Cooked"));
        Guid settingsGuid = Guid.Parse("93000000-0000-0000-0000-000000000001");
        Guid materialGuid = Guid.Parse("93000000-0000-0000-0000-000000000002");
        Guid shadowShaderGuid = Guid.Parse("93000000-0000-0000-0000-000000000003");
        Guid skyShaderGuid = Guid.Parse("93000000-0000-0000-0000-000000000004");
        Guid fallbackMeshGuid = Guid.Parse("93000000-0000-0000-0000-000000000005");
        string settingsPath = temp.Write("Assets/Default.arisrenderpipeline", """
            Version: 1
            Pipeline: GenericRP
            Name: Test Generic RP
            Fallback:
              ClearColor: { R: 0.1, G: 0.2, B: 0.3, A: 1.0 }
            Shadows:
              Enabled: true
              MapSize: 1024
              DepthBias: 0.001
              SlopeBias: 0.002
              Strength: 0.75
              PcfRadius: 2
            """);
        const string packageId = "com.arisen.generic-renderpipeline";
        database.AddAsset(settingsGuid, "RenderPipelineSettings", settingsPath, packageId);
        database.AddAsset(
            materialGuid,
            "Material",
            temp.Write("Assets/Default.arismaterial", string.Empty),
            packageId);
        database.AddAsset(
            shadowShaderGuid,
            ShaderAssetCooker.ShaderSourceAssetType,
            temp.Write("Assets/Shadow.hlsl", string.Empty),
            packageId);
        database.AddAsset(
            skyShaderGuid,
            ShaderAssetCooker.ShaderSourceAssetType,
            temp.Write("Assets/Sky.hlsl", string.Empty),
            packageId);
        database.AddAsset(
            fallbackMeshGuid,
            "Mesh",
            temp.Write("Assets/Fallback.obj", string.Empty),
            packageId);
        var shadowShader = new ShaderAsset(
            shadowShaderGuid,
            "Tests/Shadow",
            [new ShaderStageAsset("Vertex", EProgramStage.Vertex, "VSMain")],
            ShaderVariantKey.VulkanDebug);
        var skyShader = new ShaderAsset(
            skyShaderGuid,
            "Tests/Sky",
            [
                new ShaderStageAsset("Vertex", EProgramStage.Vertex, "VSMain"),
                new ShaderStageAsset("Fragment", EProgramStage.Fragment, "PSMain")
            ],
            ShaderVariantKey.VulkanDebug);
        var recipes = new RuntimeShaderCookRecipeRegistry();
        var provider = new GenericRenderPipelineRuntimeAssetCooker(
            database,
            recipes,
            new AssetRef<MaterialSourceAsset>(materialGuid, "Material", packageId),
            new AssetRef<MeshSourceAsset>(fallbackMeshGuid, "Mesh", packageId),
            [shadowShader, skyShader]);
        var request = new RuntimeAssetCookRequest(
            settingsGuid,
            packageId,
            GenericRenderPipelineSettingsLoader.AssetType);

        RuntimeAssetCookerOutput first = provider.Cook(CreateContext(temp), request);
        byte[] firstBytes = File.ReadAllBytes(first.SourcePath);
        RuntimeAssetCookerOutput second = provider.Cook(CreateContext(temp), request);
        byte[] secondBytes = File.ReadAllBytes(second.SourcePath);

        Assert.Equal(firstBytes, secondBytes);
        Assert.Equal("ARISGRPS", System.Text.Encoding.ASCII.GetString(firstBytes, 0, 8));
        Assert.Equal(
            GenericRenderPipelineSettingsCooker.CookedFormatVersion,
            BinaryPrimitives.ReadInt32LittleEndian(firstBytes.AsSpan(8, sizeof(int))));
        Assert.Equal(GenericRenderPipelineSettingsCooker.RuntimeVariant, first.Artifact.Variant);
        Assert.Equal(first.Artifact.Sha256, second.Artifact.Sha256);
        Assert.Collection(
            first.Dependencies,
            dependency => AssertDependency(
                dependency,
                materialGuid,
                packageId,
                "Material",
                required: true),
            dependency => AssertDependency(
                dependency,
                shadowShaderGuid,
                packageId,
                ShaderAssetCooker.ShaderSourceAssetType,
                required: true,
                shadowShader.Variant.GetCookedVariant("VSMain")),
            dependency => AssertDependency(
                dependency,
                skyShaderGuid,
                packageId,
                ShaderAssetCooker.ShaderSourceAssetType,
                required: true,
                skyShader.Variant.GetCookedVariant("VSMain")),
            dependency => AssertDependency(
                dependency,
                skyShaderGuid,
                packageId,
                ShaderAssetCooker.ShaderSourceAssetType,
                required: true,
                skyShader.Variant.GetCookedVariant("PSMain")),
            dependency => AssertDependency(
                dependency,
                fallbackMeshGuid,
                packageId,
                "Mesh",
                required: true));
        Assert.True(recipes.TryGetRecipe(
            shadowShaderGuid,
            shadowShader.Variant.GetCookedVariant("VSMain"),
            out _));
        Assert.True(recipes.TryGetRecipe(
            skyShaderGuid,
            skyShader.Variant.GetCookedVariant("PSMain"),
            out _));
    }

    [Fact]
    public void RenderingProvider_UsesDefaultTextureVariantAndRejectsMalformedVariants()
    {
        using var temp = new TempDirectory();
        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(temp.Path, "Cooked"));
        Guid textureGuid = Guid.Parse("94000000-0000-0000-0000-000000000001");
        const string packageId = "com.game.textures";
        database.AddAsset(
            textureGuid,
            "Texture2D",
            temp.Write("Assets/Pixel.ppm", """
                P3
                1 1
                255
                255 0 0
                """),
            packageId);
        var provider = new RenderingRuntimeAssetCooker(
            database,
            new RuntimeShaderCookRecipeRegistry());

        RuntimeAssetCookerOutput output = provider.Cook(
            CreateContext(temp),
            new RuntimeAssetCookRequest(textureGuid, packageId, "Texture2D"));

        Assert.Equal(Texture2DVariantKey.DefaultSRgb.GetCookedVariant(), output.Artifact.Variant);
        Assert.Equal(Texture2DAssetCooker.CookedFormatVersion, output.Artifact.FormatVersion);
        Assert.Throws<InvalidOperationException>(() => provider.Cook(
            CreateContext(temp),
            new RuntimeAssetCookRequest(
                textureGuid,
                packageId,
                "Texture2D",
                "not.a.texture-variant")));
    }

    private static RuntimeAssetCookContext CreateContext(TempDirectory temp)
    {
        return new RuntimeAssetCookContext(
            temp.Path,
            "Production",
            "Release",
            "win-x64",
            Path.Combine(temp.Path, "Staging"),
            ForceRebuild: false);
    }

    private static void AssertDependency(
        RuntimeAssetCookDependencyRequest actual,
        Guid guid,
        string packageId,
        string assetType,
        bool required,
        string variant = "")
    {
        Assert.Equal(guid, actual.Guid);
        Assert.Equal(packageId, actual.PackageId);
        Assert.Equal(assetType, actual.AssetType);
        Assert.Equal(variant, actual.Variant);
        Assert.Equal(required, actual.Required);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ArisenRuntimeAssetProviderTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string Write(string relativePath, string contents)
        {
            string path = System.IO.Path.Combine(
                Path,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
