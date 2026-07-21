using System.Security.Cryptography;
using System.Text;
using ArisenEngine.Core.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RuntimeAssetCatalogTests
{
    private static readonly Guid s_SceneGuid = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid s_PipelineGuid = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid s_MaterialGuid = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid s_TextureGuid = Guid.Parse("40000000-0000-0000-0000-000000000004");
    private static readonly Guid s_ShaderGuid = Guid.Parse("50000000-0000-0000-0000-000000000005");
    private static readonly Guid s_MeshGuid = Guid.Parse("60000000-0000-0000-0000-000000000006");
    private static readonly Guid s_UnusedGuid = Guid.Parse("70000000-0000-0000-0000-000000000007");

    [Fact]
    public void Serialization_IsCanonicalAcrossInputOrdering()
    {
        RuntimeAssetCatalog forward = CreateRepresentativeCatalog(reverseInput: false);
        RuntimeAssetCatalog reverse = CreateRepresentativeCatalog(reverseInput: true);

        Assert.Equal(forward.Serialize(), reverse.Serialize());

        string json = Encoding.UTF8.GetString(forward.Serialize());
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", json, StringComparison.Ordinal);
        Assert.DoesNotContain("LastWriteTime", json, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".arisen/Cache", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialization_RoundTripsStrictVersionedCatalog()
    {
        RuntimeAssetCatalog source = CreateRepresentativeCatalog(reverseInput: true);

        RuntimeAssetCatalog parsed = RuntimeAssetCatalog.Parse(source.Serialize());

        Assert.Equal(RuntimeAssetCatalog.CurrentSchemaVersion, parsed.SchemaVersion);
        Assert.Equal("Production", parsed.TargetProfile);
        Assert.Equal(new[] { "renderPipeline", "startupScene" }, parsed.Roots.Select(root => root.Name));
        Assert.Equal(4, parsed.Artifacts.Count);
        Assert.True(parsed.TryGetArtifact(s_SceneGuid, "runtime.scene.v1", out var scene));
        Assert.Equal("com.arisen.resources", scene.PackageId);
        Assert.Equal("Scene", scene.AssetType);
        Assert.Equal(2, scene.Dependencies.Count);
        Assert.Equal(s_MaterialGuid, scene.Dependencies[0].Guid);
        Assert.True(scene.Dependencies[0].Required);
        Assert.Equal(s_TextureGuid, scene.Dependencies[1].Guid);
        Assert.False(scene.Dependencies[1].Required);
        Assert.Equal(source.Serialize(), parsed.Serialize());
    }

    [Fact]
    public void Parse_RejectsUnsupportedMalformedOrNonCanonicalData()
    {
        RuntimeAssetCatalog catalog = CreateRepresentativeCatalog(reverseInput: false);
        string json = Encoding.UTF8.GetString(catalog.Serialize());

        string unsupportedVersion = json.Replace(
            "\"schemaVersion\":1",
            "\"schemaVersion\":2",
            StringComparison.Ordinal);
        string unknownProperty = json.Insert(1, "\"generatedAt\":\"now\",");
        string duplicateProperty = json.Replace(
            "\"targetProfile\":\"Production\",",
            "\"targetProfile\":\"Production\",\"targetProfile\":\"Editor\",",
            StringComparison.Ordinal);
        string uppercaseHash = json.Replace(
            Hash("scene"),
            Hash("scene").ToUpperInvariant(),
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Parse(unsupportedVersion));
        Assert.Throws<InvalidDataException>(() => Parse(unknownProperty));
        Assert.Throws<InvalidDataException>(() => Parse(duplicateProperty));
        Assert.Throws<InvalidDataException>(() => Parse(uppercaseHash));
        Assert.Throws<InvalidDataException>(() => Parse(json + "// comment"));
    }

    [Fact]
    public void Create_RejectsDuplicateArtifactDependencyAndOutputPath()
    {
        RuntimeAssetCatalogArtifact material = Artifact(
            s_MaterialGuid,
            "com.arisen.rendering",
            "Material",
            "material.runtime",
            "com.arisen.rendering/material.bin",
            "material");
        RuntimeAssetCatalogArtifact duplicateMaterial = Artifact(
            s_MaterialGuid,
            "com.arisen.rendering",
            "Material",
            "material.runtime",
            "com.arisen.rendering/material-copy.bin",
            "material-copy");
        RuntimeAssetCatalogArtifact textureWithCollidingPath = Artifact(
            s_TextureGuid,
            "com.arisen.rendering",
            "Texture2D",
            "r8g8b8a8unorm.srgb.nomips",
            "COM.ARISEN.RENDERING/MATERIAL.BIN",
            "texture");
        RuntimeAssetCatalogDependency materialDependency = Dependency(material, required: true);
        RuntimeAssetCatalogArtifact sceneWithDuplicateDependency = Artifact(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            "com.arisen.resources/scene.ariscene",
            "scene",
            materialDependency,
            materialDependency);

        var duplicateIdentity = Assert.Throws<InvalidDataException>(() => RuntimeAssetCatalog.Create(
            "Production",
            Array.Empty<RuntimeAssetCatalogRoot>(),
            new[] { material, duplicateMaterial }));
        var duplicatePath = Assert.Throws<InvalidDataException>(() => RuntimeAssetCatalog.Create(
            "Production",
            Array.Empty<RuntimeAssetCatalogRoot>(),
            new[] { material, textureWithCollidingPath }));
        var duplicateDependency = Assert.Throws<InvalidDataException>(() => RuntimeAssetCatalog.Create(
            "Production",
            Array.Empty<RuntimeAssetCatalogRoot>(),
            new[] { material, sceneWithDuplicateDependency }));

        Assert.Contains("Duplicate artifact identity", duplicateIdentity.Message, StringComparison.Ordinal);
        Assert.Contains("Duplicate output-relative", duplicatePath.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate dependency", duplicateDependency.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/absolute/scene.ariscene")]
    [InlineData("C:/absolute/scene.ariscene")]
    [InlineData("../scene.ariscene")]
    [InlineData("content\\scene.ariscene")]
    [InlineData("content//scene.ariscene")]
    [InlineData("content/./scene.ariscene")]
    [InlineData("content/../scene.ariscene")]
    [InlineData("content/scene.ariscene.")]
    public void Create_RejectsAbsoluteTraversalOrNonPortablePaths(string path)
    {
        RuntimeAssetCatalogArtifact artifact = Artifact(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            path,
            "scene");

        Assert.Throws<InvalidDataException>(() => RuntimeAssetCatalog.Create(
            "Production",
            Array.Empty<RuntimeAssetCatalogRoot>(),
            new[] { artifact }));
    }

    [Fact]
    public void Create_RejectsMissingOrMismatchedRootsAndDependencies()
    {
        RuntimeAssetCatalogArtifact scene = Artifact(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            "com.arisen.resources/scene.ariscene",
            "scene");
        var missingRoot = new RuntimeAssetCatalogRoot(
            "startupScene",
            s_MaterialGuid,
            "com.arisen.rendering",
            "Material",
            "material.runtime");
        var mismatchedRoot = new RuntimeAssetCatalogRoot(
            "startupScene",
            s_SceneGuid,
            "com.arisen.wrong-owner",
            "Scene",
            "runtime.scene.v1");
        var missingOptionalDependency = new RuntimeAssetCatalogDependency(
            s_TextureGuid,
            "com.arisen.rendering",
            "Texture2D",
            "r8g8b8a8unorm.srgb.nomips",
            Required: false);
        RuntimeAssetCatalogArtifact dependentScene = Artifact(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            "com.arisen.resources/dependent-scene.ariscene",
            "dependent-scene",
            missingOptionalDependency);

        Assert.Throws<InvalidDataException>(() => RuntimeAssetCatalog.Create(
            "Production",
            new[] { missingRoot },
            new[] { scene }));
        Assert.Throws<InvalidDataException>(() => RuntimeAssetCatalog.Create(
            "Production",
            new[] { mismatchedRoot },
            new[] { scene }));
        Assert.Throws<InvalidDataException>(() => RuntimeAssetCatalog.Create(
            "Production",
            Array.Empty<RuntimeAssetCatalogRoot>(),
            new[] { dependentScene }));
    }

    [Fact]
    public void ClosurePlanner_SelectsTwoRootTransitiveClosureDeterministically()
    {
        RuntimeAssetCatalog forward = CreateRepresentativeClosure(reverseInput: false);
        RuntimeAssetCatalog reverse = CreateRepresentativeClosure(reverseInput: true);

        Assert.Equal(forward.Serialize(), reverse.Serialize());
        Assert.Equal(new[] { "renderPipeline", "startupScene" }, forward.Roots.Select(root => root.Name));
        Assert.Equal(
            new[]
            {
                s_SceneGuid,
                s_PipelineGuid,
                s_MaterialGuid,
                s_TextureGuid,
                s_ShaderGuid,
                s_MeshGuid
            },
            forward.Artifacts.Select(artifact => artifact.Guid));
        Assert.False(forward.TryGetArtifact(s_UnusedGuid, "unused.runtime", out _));
        Assert.True(forward.TryGetArtifact(s_ShaderGuid, "vulkan1.3.runtime", out _));
    }

    [Fact]
    public void ClosurePlanner_ReportsCompleteChainForMissingDependencies()
    {
        var missingShader = new RuntimeAssetCatalogDependency(
            s_ShaderGuid,
            "com.arisen.rendering",
            "ShaderSource",
            "vulkan1.3.runtime",
            Required: true);
        RuntimeAssetCatalogArtifact material = Artifact(
            s_MaterialGuid,
            "com.arisen.rendering",
            "Material",
            "material.runtime",
            "com.arisen.rendering/materials/showcase.arimaterial",
            "material",
            missingShader);
        RuntimeAssetCatalogArtifact scene = Artifact(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            "com.arisen.resources/scenes/startup.ariscene",
            "scene",
            Dependency(material, required: true));
        var root = new RuntimeAssetCatalogRoot(
            "startupScene",
            scene.Guid,
            scene.PackageId,
            scene.AssetType,
            scene.Variant);

        var error = Assert.Throws<InvalidDataException>(() => RuntimeAssetClosurePlanner.CreateCatalog(
            "Production",
            new[] { root },
            new[] { scene, material }));

        Assert.Contains("root 'startupScene'", error.Message, StringComparison.Ordinal);
        Assert.Contains(scene.Identity.ToString(), error.Message, StringComparison.Ordinal);
        Assert.Contains(material.Identity.ToString(), error.Message, StringComparison.Ordinal);
        Assert.Contains(missingShader.Identity.ToString(), error.Message, StringComparison.Ordinal);
        Assert.Contains("required dependency", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosurePlanner_RejectsMismatchedAndDuplicateCandidateMetadata()
    {
        RuntimeAssetCatalogArtifact material = Artifact(
            s_MaterialGuid,
            "com.arisen.rendering",
            "Material",
            "material.runtime",
            "com.arisen.rendering/materials/showcase.arimaterial",
            "material");
        var mismatchedMaterial = new RuntimeAssetCatalogDependency(
            material.Guid,
            "com.arisen.wrong-owner",
            material.AssetType,
            material.Variant,
            Required: true);
        RuntimeAssetCatalogArtifact scene = Artifact(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            "com.arisen.resources/scenes/startup.ariscene",
            "scene",
            mismatchedMaterial);
        var root = new RuntimeAssetCatalogRoot(
            "startupScene",
            scene.Guid,
            scene.PackageId,
            scene.AssetType,
            scene.Variant);

        var mismatch = Assert.Throws<InvalidDataException>(() => RuntimeAssetClosurePlanner.CreateCatalog(
            "Production",
            new[] { root },
            new[] { material, scene }));
        var duplicate = Assert.Throws<InvalidDataException>(() => RuntimeAssetClosurePlanner.CreateCatalog(
            "Production",
            Array.Empty<RuntimeAssetCatalogRoot>(),
            new[] { material, material }));

        Assert.Contains("com.arisen.wrong-owner:Material", mismatch.Message, StringComparison.Ordinal);
        Assert.Contains("com.arisen.rendering:Material", mismatch.Message, StringComparison.Ordinal);
        Assert.Contains("Duplicate candidate artifact identity", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosurePlanner_IncludesReachableCyclesWithoutLooping()
    {
        var sceneToMaterial = new RuntimeAssetCatalogDependency(
            s_MaterialGuid,
            "com.arisen.rendering",
            "Material",
            "material.runtime",
            Required: true);
        var materialToScene = new RuntimeAssetCatalogDependency(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            Required: true);
        RuntimeAssetCatalogArtifact scene = Artifact(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            "com.arisen.resources/scenes/startup.ariscene",
            "scene",
            sceneToMaterial);
        RuntimeAssetCatalogArtifact material = Artifact(
            s_MaterialGuid,
            "com.arisen.rendering",
            "Material",
            "material.runtime",
            "com.arisen.rendering/materials/showcase.arimaterial",
            "material",
            materialToScene);
        var root = new RuntimeAssetCatalogRoot(
            "startupScene",
            scene.Guid,
            scene.PackageId,
            scene.AssetType,
            scene.Variant);

        RuntimeAssetCatalog forward = RuntimeAssetClosurePlanner.CreateCatalog(
            "Production",
            new[] { root },
            new[] { scene, material });
        RuntimeAssetCatalog reverse = RuntimeAssetClosurePlanner.CreateCatalog(
            "Production",
            new[] { root },
            new[] { material, scene });

        Assert.Equal(forward.Serialize(), reverse.Serialize());
        Assert.Equal(new[] { s_SceneGuid, s_MaterialGuid }, forward.Artifacts.Select(x => x.Guid));
    }

    [Fact]
    public void DeploymentValidation_RemainsValidAfterContentRootMoves()
    {
        using var temp = new TempDirectory();
        string firstRoot = Path.Combine(temp.Path, "first", "Content");
        string movedRoot = Path.Combine(temp.Path, "moved", "Content");
        const string scenePath = "com.arisen.resources/scenes/startup.ariscene";
        const string materialPath = "com.arisen.rendering/materials/showcase.arimaterial";
        byte[] sceneBytes = Encoding.UTF8.GetBytes("relocatable-scene-payload");
        byte[] materialBytes = Encoding.UTF8.GetBytes("relocatable-material-payload");
        WriteArtifact(firstRoot, scenePath, sceneBytes);
        WriteArtifact(firstRoot, materialPath, materialBytes);

        RuntimeAssetCatalogArtifact material = ArtifactForBytes(
            s_MaterialGuid,
            "com.arisen.rendering",
            "Material",
            "material.runtime",
            materialPath,
            materialBytes);
        RuntimeAssetCatalogArtifact scene = ArtifactForBytes(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            scenePath,
            sceneBytes,
            Dependency(material, required: true));
        RuntimeAssetCatalog catalog = RuntimeAssetCatalog.Parse(RuntimeAssetCatalog.Create(
            "Production",
            new[]
            {
                new RuntimeAssetCatalogRoot(
                    "startupScene",
                    scene.Guid,
                    scene.PackageId,
                    scene.AssetType,
                    scene.Variant)
            },
            new[] { scene, material }).Serialize());

        catalog.ValidateDeployment(firstRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(movedRoot)!);
        Directory.Move(firstRoot, movedRoot);
        catalog.ValidateDeployment(movedRoot);

        string resolved = catalog.ResolveArtifactPath(movedRoot, s_SceneGuid, "runtime.scene.v1");
        Assert.StartsWith(Path.GetFullPath(movedRoot), resolved, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, Encoding.UTF8.GetString(catalog.Serialize()), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeploymentValidation_RejectsTamperedMissingAndWrongSizeArtifacts()
    {
        using var temp = new TempDirectory();
        string contentRoot = Path.Combine(temp.Path, "Content");
        const string path = "com.arisen.resources/scenes/startup.ariscene";
        byte[] originalBytes = Encoding.UTF8.GetBytes("known-scene-payload");
        string fullPath = WriteArtifact(contentRoot, path, originalBytes);
        RuntimeAssetCatalogArtifact scene = ArtifactForBytes(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            path,
            originalBytes);
        RuntimeAssetCatalog catalog = RuntimeAssetCatalog.Create(
            "Production",
            new[]
            {
                new RuntimeAssetCatalogRoot(
                    "startupScene",
                    scene.Guid,
                    scene.PackageId,
                    scene.AssetType,
                    scene.Variant)
            },
            new[] { scene });

        catalog.ValidateDeployment(contentRoot);

        File.WriteAllBytes(fullPath, Encoding.UTF8.GetBytes("evil!-scene-payload"));
        var tampered = Assert.Throws<InvalidDataException>(() => catalog.ValidateDeployment(contentRoot));
        Assert.Contains("SHA-256 mismatch", tampered.Message, StringComparison.Ordinal);

        File.Delete(fullPath);
        var missing = Assert.Throws<InvalidDataException>(() => catalog.ValidateDeployment(contentRoot));
        Assert.Contains("is missing", missing.Message, StringComparison.Ordinal);

        File.WriteAllBytes(fullPath, Encoding.UTF8.GetBytes("wrong-size"));
        var wrongSize = Assert.Throws<InvalidDataException>(() => catalog.ValidateDeployment(contentRoot));
        Assert.Contains("size mismatch", wrongSize.Message, StringComparison.Ordinal);
    }

    private static RuntimeAssetCatalog CreateRepresentativeCatalog(bool reverseInput)
    {
        RuntimeAssetCatalogArtifact material = Artifact(
            s_MaterialGuid,
            "com.arisen.rendering",
            "Material",
            "material.runtime",
            "com.arisen.rendering/materials/showcase.arimaterial",
            "material");
        RuntimeAssetCatalogArtifact texture = Artifact(
            s_TextureGuid,
            "com.arisen.rendering",
            "Texture2D",
            "r8g8b8a8unorm.srgb.nomips",
            "com.arisen.rendering/textures/showcase.aritexture",
            "texture");
        RuntimeAssetCatalogArtifact scene = Artifact(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            "com.arisen.resources/scenes/startup.ariscene",
            "scene",
            reverseInput
                ? new[] { Dependency(texture, required: false), Dependency(material, required: true) }
                : new[] { Dependency(material, required: true), Dependency(texture, required: false) });
        RuntimeAssetCatalogArtifact pipeline = Artifact(
            s_PipelineGuid,
            "com.arisen.generic-renderpipeline",
            "RenderPipelineSettings",
            "generic-rp.settings.v1",
            "com.arisen.generic-renderpipeline/pipeline/default.arirp",
            "pipeline");

        RuntimeAssetCatalogRoot[] roots =
        {
            new(
                "startupScene",
                scene.Guid,
                scene.PackageId,
                scene.AssetType,
                scene.Variant),
            new(
                "renderPipeline",
                pipeline.Guid,
                pipeline.PackageId,
                pipeline.AssetType,
                pipeline.Variant)
        };
        RuntimeAssetCatalogArtifact[] artifacts = { scene, pipeline, texture, material };
        if (reverseInput)
        {
            Array.Reverse(roots);
            Array.Reverse(artifacts);
        }

        return RuntimeAssetCatalog.Create("Production", roots, artifacts);
    }

    private static RuntimeAssetCatalog CreateRepresentativeClosure(bool reverseInput)
    {
        RuntimeAssetCatalogArtifact shader = Artifact(
            s_ShaderGuid,
            "com.arisen.rendering",
            "ShaderSource",
            "vulkan1.3.runtime",
            "com.arisen.rendering/shaders/lighting.spv",
            "shader");
        RuntimeAssetCatalogArtifact texture = Artifact(
            s_TextureGuid,
            "com.arisen.rendering",
            "Texture2D",
            "r8g8b8a8unorm.srgb.nomips",
            "com.arisen.rendering/textures/showcase.aritexture",
            "texture");
        RuntimeAssetCatalogArtifact mesh = Artifact(
            s_MeshGuid,
            "com.arisen.rendering",
            "Mesh",
            "mesh.runtime.v1",
            "com.arisen.rendering/meshes/showcase.arimesh",
            "mesh");
        RuntimeAssetCatalogArtifact material = Artifact(
            s_MaterialGuid,
            "com.arisen.rendering",
            "Material",
            "material.runtime",
            "com.arisen.rendering/materials/showcase.arimaterial",
            "material",
            reverseInput
                ? new[] { Dependency(texture, required: false), Dependency(shader, required: true) }
                : new[] { Dependency(shader, required: true), Dependency(texture, required: false) });
        RuntimeAssetCatalogArtifact scene = Artifact(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            "com.arisen.resources/scenes/startup.ariscene",
            "scene",
            reverseInput
                ? new[] { Dependency(mesh, required: true), Dependency(material, required: true) }
                : new[] { Dependency(material, required: true), Dependency(mesh, required: true) });
        RuntimeAssetCatalogArtifact pipeline = Artifact(
            s_PipelineGuid,
            "com.arisen.generic-renderpipeline",
            "RenderPipelineSettings",
            "generic-rp.settings.v1",
            "com.arisen.generic-renderpipeline/pipeline/default.arirp",
            "pipeline",
            Dependency(shader, required: true));
        RuntimeAssetCatalogArtifact unused = Artifact(
            s_UnusedGuid,
            "com.arisen.rendering",
            "Texture2D",
            "unused.runtime",
            "com.arisen.rendering/textures/unused.aritexture",
            "unused");

        RuntimeAssetCatalogRoot[] roots =
        {
            new(
                "startupScene",
                scene.Guid,
                scene.PackageId,
                scene.AssetType,
                scene.Variant),
            new(
                "renderPipeline",
                pipeline.Guid,
                pipeline.PackageId,
                pipeline.AssetType,
                pipeline.Variant)
        };
        RuntimeAssetCatalogArtifact[] candidates =
        {
            unused,
            scene,
            pipeline,
            mesh,
            material,
            texture,
            shader
        };
        if (reverseInput)
        {
            Array.Reverse(roots);
            Array.Reverse(candidates);
        }

        return RuntimeAssetClosurePlanner.CreateCatalog("Production", roots, candidates);
    }

    private static RuntimeAssetCatalogArtifact Artifact(
        Guid guid,
        string packageId,
        string assetType,
        string variant,
        string path,
        string hashSeed,
        params RuntimeAssetCatalogDependency[] dependencies)
    {
        return new RuntimeAssetCatalogArtifact(
            guid,
            packageId,
            assetType,
            variant,
            path,
            sizeInBytes: Encoding.UTF8.GetByteCount(hashSeed),
            sha256: Hash(hashSeed),
            formatVersion: 1,
            dependencies);
    }

    private static RuntimeAssetCatalogArtifact ArtifactForBytes(
        Guid guid,
        string packageId,
        string assetType,
        string variant,
        string path,
        byte[] bytes,
        params RuntimeAssetCatalogDependency[] dependencies)
    {
        return new RuntimeAssetCatalogArtifact(
            guid,
            packageId,
            assetType,
            variant,
            path,
            bytes.LongLength,
            Hash(bytes),
            formatVersion: 1,
            dependencies);
    }

    private static RuntimeAssetCatalogDependency Dependency(
        RuntimeAssetCatalogArtifact artifact,
        bool required)
    {
        return new RuntimeAssetCatalogDependency(
            artifact.Guid,
            artifact.PackageId,
            artifact.AssetType,
            artifact.Variant,
            required);
    }

    private static byte[] ParseBytes(string json)
    {
        return Encoding.UTF8.GetBytes(json);
    }

    private static RuntimeAssetCatalog Parse(string json)
    {
        return RuntimeAssetCatalog.Parse(ParseBytes(json));
    }

    private static string Hash(string value)
    {
        return Hash(Encoding.UTF8.GetBytes(value));
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string WriteArtifact(string contentRoot, string relativePath, byte[] bytes)
    {
        string path = Path.Combine(contentRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ArisenRuntimeCatalogTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

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
