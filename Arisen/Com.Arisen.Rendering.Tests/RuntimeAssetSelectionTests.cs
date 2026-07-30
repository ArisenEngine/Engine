using System.Security.Cryptography;
using Arisen.Native.RHI;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Rendering;
using ArisenEngine.Rendering.Resources;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RuntimeAssetSelectionTests
{
    [Fact]
    public void AssetDatabase_MountsValidatedRuntimeCatalogWithoutSourceState()
    {
        using var temp = new TempDirectory();
        Guid guid = Guid.Parse("a1000000-0000-0000-0000-000000000001");
        byte[] payload = [1, 2, 3, 4, 5];
        DeploymentFixture fixture = CreateDeployment(
            temp.Path,
            guid,
            "com.arisen.test",
            "TestAsset",
            "runtime.test.v1",
            payload);
        var database = new AssetDatabase();

        database.InitializeRuntimeCatalog(temp.Path, "Production");

        Assert.Equal(AssetDatabaseMode.ReadOnlyRuntime, database.Mode);
        Assert.True(database.IsReadOnlyRuntime);
        Assert.Empty(database.Assets);
        Assert.Equal(Path.GetFullPath(Path.Combine(temp.Path, "Content")), database.CookedRoot);
        Assert.True(database.TryGetAssetDescriptor(guid, out AssetDescriptor descriptor));
        Assert.Equal("com.arisen.test", descriptor.PackageId);
        Assert.Equal("TestAsset", descriptor.AssetType);
        Assert.False(database.TryGetAsset(guid, out _));
        Assert.True(database.TryGetCookedArtifact(guid, fixture.Variant, out CookedAssetRecord artifact));
        Assert.Equal(fixture.ArtifactPath, artifact.Path);
        Assert.True(database.TryLoadCookedAsset(guid, fixture.Variant, "TestAsset", out CookedAssetHandle handle));
        Assert.Equal(payload, database.GetCookedAssetBytes(handle).ToArray());
        database.Release(handle);
        Assert.False(database.TryLoadCookedAsset(guid, fixture.Variant, "WrongType", out _));

        Assert.Throws<InvalidOperationException>(() =>
            database.GetCookedArtifactPath(guid, fixture.Variant, ".bin"));
        Assert.Throws<InvalidOperationException>(() =>
            database.RegisterCookedArtifact(artifact));
        Assert.Throws<InvalidOperationException>(() =>
            database.InvalidateCookedAssets(guid));
        Assert.Throws<InvalidOperationException>(() =>
            database.RemoveCookedArtifacts(
                [new CookedAssetIdentity(guid, fixture.Variant)]));
        Assert.Throws<InvalidOperationException>(() =>
            database.RefreshDirectory(temp.Path));
    }

    [Fact]
    public void AssetDatabase_RemovesCookedArtifactsPersistentlyAndRejectsExternalPaths()
    {
        using var temp = new TempDirectory();
        string workspaceRoot = Path.Combine(temp.Path, "Workspace");
        Directory.CreateDirectory(workspaceRoot);
        var database = new AssetDatabase();
        database.Initialize(workspaceRoot, AssetSourceAccessMode.RuntimeAssetCook);
        Guid removedGuid = Guid.Parse("a1000000-0000-0000-0000-000000000010");
        Guid retainedGuid = Guid.Parse("a1000000-0000-0000-0000-000000000011");
        const string variant = "runtime.test.v1";
        string removedPath = database.GetCookedArtifactPath(removedGuid, variant, ".bin");
        string retainedPath = database.GetCookedArtifactPath(retainedGuid, variant, ".bin");
        File.WriteAllBytes(removedPath, [1, 2, 3]);
        File.WriteAllBytes(retainedPath, [4, 5, 6]);
        RegisterCooked(database, removedGuid, variant, removedPath);
        RegisterCooked(database, retainedGuid, variant, retainedPath);

        int removed = database.RemoveCookedArtifacts(
            [new CookedAssetIdentity(removedGuid, variant)]);

        Assert.Equal(1, removed);
        Assert.False(File.Exists(removedPath));
        Assert.True(File.Exists(retainedPath));
        Assert.False(database.TryGetCookedArtifact(removedGuid, variant, out _));
        Assert.True(database.TryGetCookedArtifact(retainedGuid, variant, out _));

        var reopened = new AssetDatabase();
        reopened.Initialize(workspaceRoot, AssetSourceAccessMode.RuntimeAssetCook);
        Assert.False(reopened.TryGetCookedArtifact(removedGuid, variant, out _));
        Assert.True(reopened.TryGetCookedArtifact(retainedGuid, variant, out _));

        Guid externalGuid = Guid.Parse("a1000000-0000-0000-0000-000000000012");
        string externalPath = Path.Combine(temp.Path, "external.bin");
        File.WriteAllBytes(externalPath, [7, 8, 9]);
        RegisterCooked(database, externalGuid, variant, externalPath);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            database.RemoveCookedArtifacts(
                [new CookedAssetIdentity(externalGuid, variant)]));

        Assert.Contains("outside CookedRoot", error.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(externalPath));
        Assert.True(database.TryGetCookedArtifact(externalGuid, variant, out _));
    }

    [Fact]
    public void AssetDatabase_ConcurrentCookedAcquisitionSharesGenerationCheckedSlot()
    {
        using var temp = new TempDirectory();
        Guid guid = Guid.Parse("a1000000-0000-0000-0000-000000000099");
        DeploymentFixture fixture = CreateDeployment(
            temp.Path,
            guid,
            "com.arisen.test",
            "TestAsset",
            "runtime.test.v1",
            Enumerable.Range(0, 4096).Select(index => (byte)index).ToArray());
        var database = new AssetDatabase();
        database.InitializeRuntimeCatalog(temp.Path, "Production");
        var handles = new CookedAssetHandle[32];

        Parallel.For(0, handles.Length, index =>
        {
            Assert.True(database.TryLoadCookedAsset(
                guid,
                fixture.Variant,
                "TestAsset",
                out handles[index]));
        });

        Assert.All(handles, handle =>
        {
            Assert.Equal(handles[0].Index, handle.Index);
            Assert.Equal(handles[0].Generation, handle.Generation);
        });
        LoadedCookedAssetDiagnostic diagnostic =
            Assert.Single(database.GetLoadedCookedAssetDiagnostics());
        Assert.Equal(handles.Length, diagnostic.RefCount);

        Parallel.ForEach(handles, database.Release);
        Assert.Empty(database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public void AssetDatabase_RecookPreservesLeasedBytesAndRoutesNewLoadsToReplacement()
    {
        using var temp = new TempDirectory();
        string workspaceRoot = Path.Combine(temp.Path, "Workspace");
        Directory.CreateDirectory(workspaceRoot);
        File.WriteAllText(Path.Combine(workspaceRoot, "Source.asset"), "source");
        var database = new AssetDatabase();
        database.Initialize(workspaceRoot, AssetSourceAccessMode.RuntimeAssetCook);
        AssetRecord asset = Assert.Single(database.Assets);
        const string variant = "runtime.test.v1";
        string cookedPath = database.GetCookedArtifactPath(asset.Guid, variant, ".bin");
        byte[] originalBytes = [1, 2, 3, 4];
        byte[] replacementBytes = [5, 6, 7, 8];
        File.WriteAllBytes(cookedPath, originalBytes);
        RegisterCooked(database, asset.Guid, variant, cookedPath);
        Assert.True(database.TryLoadCookedAsset(
            asset.Guid,
            variant,
            asset.AssetType,
            out CookedAssetHandle original));

        DateTime replacementWriteTime = File.GetLastWriteTimeUtc(cookedPath).AddSeconds(2);
        File.WriteAllBytes(cookedPath, replacementBytes);
        File.SetLastWriteTimeUtc(cookedPath, replacementWriteTime);
        RegisterCooked(database, asset.Guid, variant, cookedPath);
        Assert.True(database.TryLoadCookedAsset(
            asset.Guid,
            variant,
            asset.AssetType,
            out CookedAssetHandle replacement));

        Assert.NotEqual(original, replacement);
        Assert.Equal(originalBytes, database.GetCookedAssetBytes(original).ToArray());
        Assert.Equal(replacementBytes, database.GetCookedAssetBytes(replacement).ToArray());
        Assert.Equal(2, database.GetLoadedCookedAssetDiagnostics().Count);

        database.Release(original);
        Assert.Equal(replacementBytes, database.GetCookedAssetBytes(replacement).ToArray());
        Assert.Single(database.GetLoadedCookedAssetDiagnostics());
        database.Release(replacement);
        Assert.Empty(database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public void AssetDatabase_RuntimeCatalogRejectsWrongProfileTamperingAndMissingContent()
    {
        using var temp = new TempDirectory();
        Guid guid = Guid.Parse("a2000000-0000-0000-0000-000000000001");
        byte[] payload = [10, 20, 30, 40];
        DeploymentFixture fixture = CreateDeployment(
            temp.Path,
            guid,
            "com.arisen.test",
            "TestAsset",
            "runtime.test.v1",
            payload);
        var database = new AssetDatabase();

        InvalidDataException wrongProfile = Assert.Throws<InvalidDataException>(() =>
            database.InitializeRuntimeCatalog(temp.Path, "Development"));
        Assert.Contains("targets profile", wrongProfile.Message, StringComparison.OrdinalIgnoreCase);

        File.WriteAllBytes(fixture.ArtifactPath, [10, 20, 30, 41]);
        InvalidDataException tampered = Assert.Throws<InvalidDataException>(() =>
            database.InitializeRuntimeCatalog(temp.Path, "Production"));
        Assert.Contains("SHA-256", tampered.Message, StringComparison.OrdinalIgnoreCase);

        File.Delete(fixture.ArtifactPath);
        InvalidDataException missing = Assert.Throws<InvalidDataException>(() =>
            database.InitializeRuntimeCatalog(temp.Path, "Production"));
        Assert.Contains("missing", missing.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AssetDatabaseMode.Uninitialized, database.Mode);
    }

    [Fact]
    public void GenericRenderPipelineSettings_CookedSelectionMatchesSourceAndRejectsMalformedPayload()
    {
        using var temp = new TempDirectory();
        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(temp.Path, "Cooked"));
        Guid settingsGuid = Guid.Parse("a3000000-0000-0000-0000-000000000001");
        const string packageId = "com.arisen.generic-renderpipeline";
        string sourcePath = temp.Write("Assets/Runtime.arisrenderpipeline", """
            Version: 1
            Pipeline: GenericRP
            Name: Cooked Runtime Pipeline
            Fallback:
              ClearColor: { R: 0.12, G: 0.23, B: 0.34, A: 1.0 }
            Shadows:
              Enabled: true
              MapSize: 1024
              DepthBias: 0.0012
              SlopeBias: 0.0023
              Strength: 0.81
              PcfRadius: 2
            """);
        database.AddAsset(
            settingsGuid,
            GenericRenderPipelineSettingsLoader.AssetType,
            sourcePath,
            packageId);
        var settingsRef = new AssetRef<RenderPipelineSettingsSourceAsset>(
            settingsGuid,
            GenericRenderPipelineSettingsLoader.AssetType,
            packageId);
        GenericRenderPipelineSettings source = GenericRenderPipelineSettingsLoader.LoadSource(
            database,
            settingsRef);
        CookedGenericRenderPipelineSettings cooked = GenericRenderPipelineSettingsCooker.Cook(
            database,
            settingsRef);
        byte[] validPayload = File.ReadAllBytes(cooked.Path);
        database.UseReadOnlyRuntime();
        File.Delete(sourcePath);

        GenericRenderPipelineSettings runtime = GenericRenderPipelineSettingsLoader.Load(
            database,
            settingsRef);
        Assert.Equal(source, runtime);

        byte[] unsupportedVersion = validPayload.ToArray();
        unsupportedVersion[8] = 99;
        File.WriteAllBytes(cooked.Path, unsupportedVersion);
        InvalidOperationException versionError = Assert.Throws<InvalidOperationException>(() =>
            GenericRenderPipelineSettingsLoader.Load(database, settingsRef));
        Assert.Contains("unsupported", versionError.Message, StringComparison.OrdinalIgnoreCase);

        File.WriteAllBytes(cooked.Path, validPayload[..^1]);
        InvalidOperationException truncatedError = Assert.Throws<InvalidOperationException>(() =>
            GenericRenderPipelineSettingsLoader.Load(database, settingsRef));
        Assert.Contains("truncated", truncatedError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeSceneService_UsesCookedSceneAndRejectsSourceSnapshotsInRuntimeMode()
    {
        using var temp = new TempDirectory();
        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(temp.Path, "Cooked"));
        Guid sceneGuid = Guid.Parse("a4000000-0000-0000-0000-000000000001");
        string sourcePath = Path.Combine(temp.Path, "Assets", "Runtime.arisenscene");
        temp.Write("Assets/Runtime.arisenscene", SceneTestSource.MigrateLegacy(sceneGuid, sourcePath, """
            Version: 1
            Name: Cooked Runtime Scene
            Entities:
            - Name: Runtime Camera
              Camera:
                VerticalFov: 60
                NearPlane: 0.1
                FarPlane: 1000
            """));
        database.AddAsset(sceneGuid, "Scene", sourcePath, "com.arisen.test");
        var sceneRef = new AssetRef<SceneSourceAsset>(
            sceneGuid,
            "Scene",
            "com.arisen.test");
        SceneAssetCooker.Cook(database, sceneRef);
        database.UseReadOnlyRuntime();
        File.Delete(sourcePath);
        var activeWorld = new EntityManager();
        var service = new RuntimeSceneService(database, activeWorld);

        SceneLoadResult result = service.LoadScene(sceneRef);

        Assert.True(result.Success, result.Diagnostic);
        Assert.Equal("Cooked Runtime Scene", result.SceneName);
        Assert.Single(activeWorld.GetAllEntities());
        Assert.Throws<InvalidOperationException>(() =>
            service.RequestSceneLoad(new SceneSourceSnapshot(
                sceneRef,
                sourcePath,
                "Name: Forbidden Runtime Snapshot\nEntities: []\n",
                Revision: 1)));
    }

    [Fact]
    public void RuntimeSceneService_SourceSelectionRequiresExplicitDiagnosticAccess()
    {
        using var temp = new TempDirectory();
        var database = new TestAssetDatabase(
            AssetSourceAccessMode.Diagnostic,
            Path.Combine(temp.Path, "Cooked"));
        Guid sceneGuid = Guid.Parse("a4000000-0000-0000-0000-000000000002");
        string sourcePath = Path.Combine(temp.Path, "Assets", "Selection.arisenscene");
        temp.Write("Assets/Selection.arisenscene", SceneTestSource.MigrateLegacy(sceneGuid, sourcePath, """
            Version: 1
            Name: Cooked Selection Scene
            Entities:
            - Name: Cooked Camera
              Camera:
                VerticalFov: 60
            """));
        database.AddAsset(sceneGuid, "Scene", sourcePath, "com.arisen.test");
        var sceneRef = new AssetRef<SceneSourceAsset>(
            sceneGuid,
            "Scene",
            "com.arisen.test");
        SceneAssetCooker.Cook(database, sceneRef);
        File.WriteAllText(sourcePath, SceneTestSource.MigrateLegacy(sceneGuid, sourcePath, """
            Version: 1
            Name: Diagnostic Source Scene
            Entities:
            - Name: Diagnostic Camera
              Camera:
                VerticalFov: 45
            """));
        database.UseSourceAccess(AssetSourceAccessMode.Disabled);
        var service = new RuntimeSceneService(database, new EntityManager());

        SceneLoadResult cookedResult = service.LoadScene(sceneRef);

        Assert.True(cookedResult.Success, cookedResult.Diagnostic);
        Assert.Equal("Cooked Selection Scene", cookedResult.SceneName);
        Assert.Throws<InvalidOperationException>(() =>
            service.RequestSceneLoad(new SceneSourceSnapshot(
                sceneRef,
                sourcePath,
                File.ReadAllText(sourcePath),
                Revision: 1)));

        database.UseSourceAccess(AssetSourceAccessMode.Diagnostic);
        SceneLoadResult sourceResult = service.LoadScene(sceneRef);

        Assert.True(sourceResult.Success, sourceResult.Diagnostic);
        Assert.Equal("Diagnostic Source Scene", sourceResult.SceneName);
    }

    [Fact]
    public void RenderingAssets_LoadDeclaredCookedVariantsAfterSourcesAreRemoved()
    {
        using var temp = new TempDirectory();
        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(temp.Path, "Cooked"));
        Guid meshGuid = Guid.Parse("a5000000-0000-0000-0000-000000000001");
        Guid textureGuid = Guid.Parse("a5000000-0000-0000-0000-000000000002");
        Guid shaderGuid = Guid.Parse("a5000000-0000-0000-0000-000000000003");
        Guid materialGuid = Guid.Parse("a5000000-0000-0000-0000-000000000004");
        Guid environmentSourceGuid = Guid.Parse("a5000000-0000-0000-0000-000000000005");
        Guid environmentGuid = Guid.Parse("a5000000-0000-0000-0000-000000000006");
        string meshPath = temp.Write("Assets/Runtime.armesh", """
            v -1 -1 0 0 0 1 0 0
            v 1 -1 0 0 0 1 1 0
            v 0 1 0 0 0 1 0.5 1
            i 0 1 2
            s 0 3 0
            """);
        string texturePath = temp.Write("Assets/Runtime.ppm", "P3\n1 1\n255\n180 120 60\n");
        string shaderPath = temp.Write("Assets/Runtime.shader", """
            Shader "Tests/Runtime"
            {
                MaterialContract { Texture2D BaseColor }
                SubShader
                {
                    Pass
                    {
                        HLSLPROGRAM
                        #pragma vertex VSMain
                        #pragma fragment PSMain
                        float4 VSMain() : SV_Position { return 0; }
                        float4 PSMain() : SV_Target0 { return 1; }
                        ENDHLSL
                    }
                }
            }
            """);
        string materialPath = temp.Write("Assets/Runtime.arismaterial", $$"""
            Name: Tests/RuntimeMaterial
            Shader:
              Guid: {{shaderGuid:D}}
            Texture2DRefs:
            - Name: BaseColor
              Slot: 0
              Texture:
                Guid: {{textureGuid:D}}
                Name: Tests/RuntimeTexture
                Variant:
                  Format: R8G8B8A8UNorm
                  ColorSpace: SRgb
                  GenerateMipMaps: false
                SourceFormat: PpmP3
            """);
        string environmentSourcePath = temp.Write(
            "Assets/RuntimeEnvironment.ppm",
            "P3\n4 2\n255\n255 128 0  64 128 255  0 32 128  255 255 255\n16 16 32  32 32 64  64 64 96  128 128 160\n");
        string environmentPath = temp.Write("Assets/Runtime.arienvironment", $$"""
            Version: 1
            Name: Runtime Environment
            SourceTexture:
              Guid: {{environmentSourceGuid:D}}
              PackageId: com.arisen.test
            Layout: LatLong
            SourceColorSpace: SRgb
            RuntimeFormat: R16G16B16A16SFloat
            RotationDegrees: 18
            Intensity: 1.25
            """);
        database.AddAsset(meshGuid, "Mesh", meshPath);
        database.AddAsset(textureGuid, "Texture2D", texturePath);
        database.AddAsset(shaderGuid, ShaderAssetCooker.ShaderSourceAssetType, shaderPath);
        database.AddAsset(materialGuid, "Material", materialPath);
        database.AddAsset(environmentSourceGuid, "Texture2D", environmentSourcePath);
        database.AddAsset(environmentGuid, "EnvironmentTexture", environmentPath);

        var mesh = new MeshAsset(meshGuid, "Tests/RuntimeMesh", MeshVariantKey.Default);
        var texture = new Texture2DAsset(
            textureGuid,
            "Tests/RuntimeTexture",
            Texture2DVariantKey.DefaultSRgb,
            Texture2DSourceFormat.PpmP3);
        CookedMesh mutableMesh = MeshAssetCooker.LoadOrCook(database, mesh);
        CookedTexture2D mutableTexture = Texture2DAssetCooker.LoadOrCook(database, texture);
        CookedMaterial mutableMaterial = MaterialAssetCooker.LoadOrCook(database, materialGuid);
        EnvironmentTextureAsset mutableEnvironment = EnvironmentTextureAssetLoader.LoadSource(
            database,
            environmentGuid);
        CookedEnvironmentTexture mutableEnvironmentTexture =
            EnvironmentTextureAssetCooker.LoadOrCook(database, mutableEnvironment);
        CookedEnvironmentLighting mutableLighting =
            EnvironmentLightingAssetCooker.LoadOrCook(database, mutableEnvironment);

        foreach (ShaderStageAsset stage in mutableMaterial.Asset.Shader.Stages)
        {
            string variant = mutableMaterial.Asset.Shader.Variant.GetCookedVariant(
                stage.EntryPoint,
                mutableMaterial.Asset.Shader.VariantKeywords);
            string path = database.GetCookedArtifactPath(shaderGuid, variant, ".spv");
            File.WriteAllBytes(path, [3, 2, 35, 7]);
            var info = new FileInfo(path);
            database.RegisterCookedArtifact(new CookedAssetRecord(
                shaderGuid,
                ShaderAssetCooker.ShaderSourceAssetType,
                variant,
                info.FullName,
                info.Length,
                info.LastWriteTimeUtc));
        }

        database.Release(mutableMesh.Handle);
        database.Release(mutableTexture.Handle);
        database.Release(mutableMaterial.Handle);
        database.Release(mutableEnvironmentTexture.Handle);
        database.Release(mutableLighting.Handle);
        database.UseReadOnlyRuntime();
        foreach (string sourcePath in new[]
                 {
                     meshPath,
                     texturePath,
                     shaderPath,
                     materialPath,
                     environmentSourcePath,
                     environmentPath
                 })
        {
            File.Delete(sourcePath);
        }

        CookedMesh runtimeMesh = MeshAssetCooker.LoadOrCook(database, mesh);
        CookedTexture2D runtimeTexture = Texture2DAssetCooker.LoadOrCook(database, texture);
        CookedMaterial runtimeMaterial = MaterialAssetCooker.LoadOrCook(database, materialGuid);
        CookedShaderStage runtimeShader = ShaderAssetCooker.LoadOrCookStage(
            database,
            runtimeMaterial.Asset.Shader,
            runtimeMaterial.Asset.Shader.Stages[0].Name);
        EnvironmentTextureAsset runtimeEnvironment = EnvironmentTextureAssetLoader.Load(
            database,
            environmentGuid);
        CookedEnvironmentTexture runtimeEnvironmentTexture =
            EnvironmentTextureAssetCooker.LoadOrCook(database, runtimeEnvironment);
        CookedEnvironmentLighting runtimeLighting =
            EnvironmentLightingAssetCooker.LoadOrCook(database, runtimeEnvironment);

        Assert.Equal(3u, runtimeMesh.VertexCount);
        Assert.Equal(1u, runtimeTexture.Width);
        Assert.Equal("Tests/RuntimeMaterial", runtimeMaterial.Asset.Name);
        Assert.True(runtimeShader.IsValid);
        Assert.Equal(18.0f, runtimeEnvironment.RotationDegrees);
        Assert.Equal(1.25f, runtimeEnvironment.Intensity);
        Assert.True(runtimeEnvironmentTexture.IsValid);
        Assert.True(runtimeLighting.IsValid);

        database.Release(runtimeMesh.Handle);
        database.Release(runtimeTexture.Handle);
        database.Release(runtimeMaterial.Handle);
        database.Release(runtimeShader.Handle);
        database.Release(runtimeEnvironmentTexture.Handle);
        database.Release(runtimeLighting.Handle);
    }

    private static DeploymentFixture CreateDeployment(
        string outputRoot,
        Guid guid,
        string packageId,
        string assetType,
        string variant,
        byte[] payload)
    {
        string relativePath = $"{packageId}/{guid:N}/{variant}.bin";
        string artifactPath = Path.Combine(
            outputRoot,
            "Content",
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        File.WriteAllBytes(artifactPath, payload);
        string hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var artifact = new RuntimeAssetCatalogArtifact(
            guid,
            packageId,
            assetType,
            variant,
            relativePath,
            payload.LongLength,
            hash,
            formatVersion: 1);
        RuntimeAssetCatalog catalog = RuntimeAssetCatalog.Create(
            "Production",
            [new RuntimeAssetCatalogRoot("testRoot", guid, packageId, assetType, variant)],
            [artifact]);
        File.WriteAllBytes(
            Path.Combine(outputRoot, RuntimeAssetCatalog.DefaultFileName),
            catalog.Serialize());
        return new DeploymentFixture(variant, Path.GetFullPath(artifactPath));
    }

    private static void RegisterCooked(
        AssetDatabase database,
        Guid guid,
        string variant,
        string path)
    {
        var info = new FileInfo(path);
        database.RegisterCookedArtifact(new CookedAssetRecord(
            guid,
            "TestAsset",
            variant,
            info.FullName,
            info.Length,
            info.LastWriteTimeUtc));
    }

    private readonly record struct DeploymentFixture(string Variant, string ArtifactPath);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ArisenRuntimeAssetSelectionTests",
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
