using System.Security.Cryptography;
using System.Text;
using ArisenEngine.Core.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RuntimeAssetCookingTests
{
    private static readonly Guid s_SceneGuid = Guid.Parse("81000000-0000-0000-0000-000000000001");
    private static readonly Guid s_PipelineGuid = Guid.Parse("82000000-0000-0000-0000-000000000002");
    private static readonly Guid s_MaterialGuid = Guid.Parse("83000000-0000-0000-0000-000000000003");
    private static readonly Guid s_ShaderGuid = Guid.Parse("84000000-0000-0000-0000-000000000004");
    private static readonly Guid s_TextureGuid = Guid.Parse("85000000-0000-0000-0000-000000000005");

    [Fact]
    public void Cook_IsDeterministicAcrossRootAndDependencyOrdering()
    {
        using var temp = new TempDirectory();

        CookRun forward = CookRepresentativeGraph(temp, reverseInput: false);
        CookRun reverse = CookRepresentativeGraph(temp, reverseInput: true);

        Assert.Equal(forward.Result.Catalog.Serialize(), reverse.Result.Catalog.Serialize());
        Assert.Equal(
            forward.Cooker.Requests.Select(request => request.Guid),
            reverse.Cooker.Requests.Select(request => request.Guid));
        Assert.Equal(
            new[] { "renderPipeline", "startupScene" },
            forward.Result.Catalog.Roots.Select(root => root.Name));
        Assert.Equal(5, forward.Result.Catalog.Artifacts.Count);
    }

    [Fact]
    public void Cook_ResolvesDefaultVariantsAndCooksSharedDependenciesOnce()
    {
        using var temp = new TempDirectory();
        RuntimeAssetCookContext context = CreateContext(temp);
        var definitions = new Dictionary<Guid, CookDefinition>
        {
            [s_SceneGuid] = new(
                "runtime.scene.v1",
                "scene",
                Dependency(s_MaterialGuid, "com.arisen.rendering", "Material", "")),
            [s_PipelineGuid] = new(
                "generic-rp.v1",
                "pipeline",
                Dependency(s_ShaderGuid, "com.arisen.rendering", "Shader", "shader.spirv.v1")),
            [s_MaterialGuid] = new(
                "material.runtime.v6",
                "material",
                Dependency(s_TextureGuid, "com.arisen.rendering", "Texture2D", "")),
            [s_ShaderGuid] = new(
                "shader.spirv.v1",
                "shader",
                Dependency(s_TextureGuid, "com.arisen.rendering", "Texture2D", "")),
            [s_TextureGuid] = new("rgba8.srgb.nomips", "texture")
        };
        DelegateCooker cooker = CreateDefinitionCooker(temp, definitions);
        RuntimeAssetCookerRegistry registry = Registry(cooker);

        RuntimeAssetCookResult result = RuntimeAssetCookCoordinator.Cook(
            context,
            new[]
            {
                Root("startupScene", s_SceneGuid, "com.arisen.resources", "Scene", ""),
                Root(
                    "renderPipeline",
                    s_PipelineGuid,
                    "com.arisen.generic-renderpipeline",
                    "RenderPipelineSettings",
                    "generic-rp.v1")
            },
            registry);

        Assert.Equal("runtime.scene.v1", result.Catalog.Roots.Single(root => root.Name == "startupScene").Variant);
        Assert.Equal("generic-rp.v1", result.Catalog.Roots.Single(root => root.Name == "renderPipeline").Variant);
        Assert.Equal(5, result.Catalog.Artifacts.Count);
        Assert.Equal(1, cooker.Requests.Count(request => request.Guid == s_TextureGuid));
        Assert.Contains(
            cooker.Requests,
            request => request.Guid == s_SceneGuid && request.Variant.Length == 0);
        Assert.Contains(
            cooker.Requests,
            request => request.Guid == s_PipelineGuid && request.Variant == "generic-rp.v1");

        RuntimeAssetCatalogArtifact material = result.Catalog.Artifacts.Single(
            artifact => artifact.Guid == s_MaterialGuid);
        Assert.Equal("rgba8.srgb.nomips", Assert.Single(material.Dependencies).Variant);
    }

    [Fact]
    public void Cook_ClosesCyclicDependenciesWithoutRepeatedProviderCalls()
    {
        using var temp = new TempDirectory();
        var definitions = new Dictionary<Guid, CookDefinition>
        {
            [s_SceneGuid] = new(
                "runtime.scene.v1",
                "scene",
                Dependency(s_MaterialGuid, "com.arisen.rendering", "Material", "material.runtime.v6")),
            [s_MaterialGuid] = new(
                "material.runtime.v6",
                "material",
                Dependency(s_SceneGuid, "com.arisen.resources", "Scene", "runtime.scene.v1"))
        };
        DelegateCooker cooker = CreateDefinitionCooker(temp, definitions);

        RuntimeAssetCookResult result = RuntimeAssetCookCoordinator.Cook(
            CreateContext(temp),
            new[]
            {
                Root(
                    "startupScene",
                    s_SceneGuid,
                    "com.arisen.resources",
                    "Scene",
                    "runtime.scene.v1")
            },
            Registry(cooker));

        Assert.Equal(2, result.Catalog.Artifacts.Count);
        Assert.Equal(2, cooker.Requests.Count);
        Assert.All(cooker.Requests.GroupBy(request => request.Guid), group => Assert.Single(group));
    }

    [Fact]
    public void Cook_ReportsTheFullRootChainWhenDependencyProviderIsMissing()
    {
        using var temp = new TempDirectory();
        var definitions = new Dictionary<Guid, CookDefinition>
        {
            [s_SceneGuid] = new(
                "runtime.scene.v1",
                "scene",
                Dependency(s_MaterialGuid, "com.arisen.rendering", "Material", "material.runtime.v6")),
            [s_MaterialGuid] = new(
                "material.runtime.v6",
                "material",
                Dependency(s_TextureGuid, "com.arisen.rendering", "Texture2D", "rgba8.srgb.nomips"))
        };
        DelegateCooker cooker = CreateDefinitionCooker(
            temp,
            definitions,
            new[] { "Scene", "Material" });

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            RuntimeAssetCookCoordinator.Cook(
                CreateContext(temp),
                new[]
                {
                    Root(
                        "startupScene",
                        s_SceneGuid,
                        "com.arisen.resources",
                        "Scene",
                        "runtime.scene.v1")
                },
                Registry(cooker)));

        Assert.Contains("No package-owned cooker", error.Message, StringComparison.Ordinal);
        Assert.Contains("root 'startupScene'", error.Message, StringComparison.Ordinal);
        Assert.Contains(s_SceneGuid.ToString("D"), error.Message, StringComparison.Ordinal);
        Assert.Contains(s_MaterialGuid.ToString("D"), error.Message, StringComparison.Ordinal);
        Assert.Contains(s_TextureGuid.ToString("D"), error.Message, StringComparison.Ordinal);
        Assert.Contains("Texture2D", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_RejectsDuplicateAssetTypeProvidersWithoutPartialRegistration()
    {
        var registry = new RuntimeAssetCookerRegistry();
        var first = new DelegateCooker("package.first", new[] { "Scene" }, (_, request) =>
            throw new InvalidOperationException(request.AssetType));
        var conflicting = new DelegateCooker(
            "package.conflicting",
            new[] { "Material", "Scene" },
            (_, request) => throw new InvalidOperationException(request.AssetType));

        registry.RegisterCooker(first);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterCooker(conflicting));

        Assert.Contains("already has cooker provider", error.Message, StringComparison.Ordinal);
        Assert.True(registry.TryGetCooker("scene", out IRuntimeAssetCooker selected));
        Assert.Same(first, selected);
        Assert.False(registry.TryGetCooker("Material", out _));
        Assert.Equal(
            new[] { new RuntimeAssetCookerRegistration("Scene", "package.first") },
            registry.GetRegistrations());
    }

    [Fact]
    public void Registry_UnregisterRemovesOnlyTheExactCookerInstance()
    {
        var registry = new RuntimeAssetCookerRegistry();
        var first = new DelegateCooker("package.first", new[] { "Scene", "Material" },
            (_, request) => throw new InvalidOperationException(request.AssetType));
        var second = new DelegateCooker("package.second", new[] { "Texture2D" },
            (_, request) => throw new InvalidOperationException(request.AssetType));

        registry.RegisterCooker(first);
        registry.RegisterCooker(second);

        Assert.True(registry.UnregisterCooker(first));
        Assert.False(registry.UnregisterCooker(first));
        Assert.False(registry.TryGetCooker("Scene", out _));
        Assert.False(registry.TryGetCooker("Material", out _));
        Assert.True(registry.TryGetCooker("Texture2D", out IRuntimeAssetCooker selected));
        Assert.Same(second, selected);
        Assert.Equal(
            new[] { new RuntimeAssetCookerRegistration("Texture2D", "package.second") },
            registry.GetRegistrations());
    }

    [Fact]
    public void Cook_RejectsProviderOutputThatDoesNotMatchTheRequest()
    {
        using var temp = new TempDirectory();
        var cooker = new DelegateCooker("com.arisen.resources", new[] { "Scene" }, (_, request) =>
        {
            byte[] bytes = Encoding.UTF8.GetBytes("scene");
            string sourcePath = WriteFile(temp.Path, "staging/incompatible.ariscene", bytes);
            return new RuntimeAssetCookerOutput(
                Artifact(
                    request.Guid,
                    "com.arisen.wrong-owner",
                    request.AssetType,
                    "runtime.scene.v1",
                    "com.arisen.wrong-owner/incompatible.ariscene",
                    bytes),
                sourcePath);
        });

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            RuntimeAssetCookCoordinator.Cook(
                CreateContext(temp),
                new[]
                {
                    Root(
                        "startupScene",
                        s_SceneGuid,
                        "com.arisen.resources",
                        "Scene",
                        "runtime.scene.v1")
                },
                Registry(cooker)));

        Assert.Contains("incompatible request", error.Message, StringComparison.Ordinal);
        Assert.Contains("com.arisen.wrong-owner", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CookedFileFault.Missing, "is missing")]
    [InlineData(CookedFileFault.WrongSize, "size mismatch")]
    [InlineData(CookedFileFault.Tampered, "SHA-256 mismatch")]
    public void Cook_RejectsInvalidProducedFiles(CookedFileFault fault, string expectedDiagnostic)
    {
        using var temp = new TempDirectory();
        var cooker = new DelegateCooker("com.arisen.resources", new[] { "Scene" }, (_, request) =>
        {
            byte[] actualBytes = Encoding.UTF8.GetBytes("ABCD");
            byte[] declaredBytes = fault == CookedFileFault.Tampered
                ? Encoding.UTF8.GetBytes("WXYZ")
                : actualBytes;
            string sourcePath = Path.Combine(temp.Path, "staging", "scene.ariscene");
            if (fault != CookedFileFault.Missing)
            {
                WriteFile(temp.Path, "staging/scene.ariscene", actualBytes);
            }

            RuntimeAssetCatalogArtifact artifact = Artifact(
                request.Guid,
                request.PackageId,
                request.AssetType,
                "runtime.scene.v1",
                "com.arisen.resources/scene.ariscene",
                declaredBytes,
                sizeOverride: fault == CookedFileFault.WrongSize
                    ? actualBytes.LongLength + 1
                    : null);
            return new RuntimeAssetCookerOutput(artifact, sourcePath);
        });

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            RuntimeAssetCookCoordinator.Cook(
                CreateContext(temp),
                new[]
                {
                    Root(
                        "startupScene",
                        s_SceneGuid,
                        "com.arisen.resources",
                        "Scene",
                        "runtime.scene.v1")
                },
                Registry(cooker)));

        Assert.Contains(expectedDiagnostic, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("root 'startupScene'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Cook_KeepsMachineLocalSourcePathsOutOfCatalogSerialization()
    {
        using var temp = new TempDirectory();
        var definitions = new Dictionary<Guid, CookDefinition>
        {
            [s_SceneGuid] = new("runtime.scene.v1", "scene")
        };
        DelegateCooker cooker = CreateDefinitionCooker(temp, definitions, new[] { "Scene" });

        RuntimeAssetCookResult result = RuntimeAssetCookCoordinator.Cook(
            CreateContext(temp),
            new[]
            {
                Root(
                    "startupScene",
                    s_SceneGuid,
                    "com.arisen.resources",
                    "Scene",
                    "")
            },
            Registry(cooker));

        RuntimeAssetCookedFile file = Assert.Single(result.Files);
        Assert.True(Path.IsPathFullyQualified(file.SourcePath));
        Assert.StartsWith(temp.Path, file.SourcePath, StringComparison.OrdinalIgnoreCase);

        string json = Encoding.UTF8.GetString(result.Catalog.Serialize());
        Assert.DoesNotContain(Path.GetFileName(temp.Path), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SourcePath", json, StringComparison.Ordinal);
        Assert.Contains(file.Artifact.OutputRelativePath, json, StringComparison.Ordinal);
        Assert.Equal(result.Catalog.Serialize(), RuntimeAssetCatalog.Parse(result.Catalog.Serialize()).Serialize());
    }

    private static CookRun CookRepresentativeGraph(TempDirectory temp, bool reverseInput)
    {
        RuntimeAssetCookDependencyRequest[] sceneDependencies =
        {
            Dependency(s_MaterialGuid, "com.arisen.rendering", "Material", "material.runtime.v6"),
            Dependency(s_ShaderGuid, "com.arisen.rendering", "Shader", "shader.spirv.v1")
        };
        if (reverseInput)
        {
            Array.Reverse(sceneDependencies);
        }

        var definitions = new Dictionary<Guid, CookDefinition>
        {
            [s_SceneGuid] = new("runtime.scene.v1", "scene", sceneDependencies),
            [s_PipelineGuid] = new(
                "generic-rp.v1",
                "pipeline",
                Dependency(s_ShaderGuid, "com.arisen.rendering", "Shader", "shader.spirv.v1")),
            [s_MaterialGuid] = new(
                "material.runtime.v6",
                "material",
                Dependency(s_TextureGuid, "com.arisen.rendering", "Texture2D", "rgba8.srgb.nomips")),
            [s_ShaderGuid] = new("shader.spirv.v1", "shader"),
            [s_TextureGuid] = new("rgba8.srgb.nomips", "texture")
        };
        DelegateCooker cooker = CreateDefinitionCooker(temp, definitions);
        RuntimeAssetCookRootRequest[] roots =
        {
            Root("startupScene", s_SceneGuid, "com.arisen.resources", "Scene", ""),
            Root(
                "renderPipeline",
                s_PipelineGuid,
                "com.arisen.generic-renderpipeline",
                "RenderPipelineSettings",
                "")
        };
        if (reverseInput)
        {
            Array.Reverse(roots);
        }

        RuntimeAssetCookResult result = RuntimeAssetCookCoordinator.Cook(
            CreateContext(temp, reverseInput ? "reverse" : "forward"),
            roots,
            Registry(cooker));
        return new CookRun(result, cooker);
    }

    private static DelegateCooker CreateDefinitionCooker(
        TempDirectory temp,
        IReadOnlyDictionary<Guid, CookDefinition> definitions,
        IReadOnlyCollection<string>? assetTypes = null)
    {
        string[] supportedTypes = assetTypes?.ToArray() ??
        ["Scene", "RenderPipelineSettings", "Material", "Shader", "Texture2D"];
        return new DelegateCooker("com.arisen.test-cookers", supportedTypes, (_, request) =>
        {
            CookDefinition definition = definitions[request.Guid];
            if (request.Variant.Length > 0 && request.Variant != definition.Variant)
            {
                throw new InvalidDataException(
                    $"Variant '{request.Variant}' is unsupported for '{request.Guid:D}'.");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(definition.Payload);
            string fileName = $"{request.Guid:N}.{definition.Variant}.bin";
            string sourcePath = WriteFile(temp.Path, $"staging/{fileName}", bytes);
            return new RuntimeAssetCookerOutput(
                Artifact(
                    request.Guid,
                    request.PackageId,
                    request.AssetType,
                    definition.Variant,
                    $"{request.PackageId}/{fileName}",
                    bytes),
                sourcePath,
                definition.Dependencies);
        });
    }

    private static RuntimeAssetCookerRegistry Registry(params IRuntimeAssetCooker[] cookers)
    {
        var registry = new RuntimeAssetCookerRegistry();
        foreach (IRuntimeAssetCooker cooker in cookers)
        {
            registry.RegisterCooker(cooker);
        }

        return registry;
    }

    private static RuntimeAssetCookContext CreateContext(TempDirectory temp, string suffix = "default")
    {
        return new RuntimeAssetCookContext(
            temp.Path,
            "Production",
            "Release",
            "win-x64",
            Path.Combine(temp.Path, "cook", suffix),
            ForceRebuild: false);
    }

    private static RuntimeAssetCookRootRequest Root(
        string name,
        Guid guid,
        string packageId,
        string assetType,
        string variant)
    {
        return new RuntimeAssetCookRootRequest(name, guid, packageId, assetType, variant);
    }

    private static RuntimeAssetCookDependencyRequest Dependency(
        Guid guid,
        string packageId,
        string assetType,
        string variant,
        bool required = true)
    {
        return new RuntimeAssetCookDependencyRequest(
            guid,
            packageId,
            assetType,
            variant,
            required);
    }

    private static RuntimeAssetCatalogArtifact Artifact(
        Guid guid,
        string packageId,
        string assetType,
        string variant,
        string outputRelativePath,
        byte[] declaredBytes,
        long? sizeOverride = null)
    {
        return new RuntimeAssetCatalogArtifact(
            guid,
            packageId,
            assetType,
            variant,
            outputRelativePath,
            sizeOverride ?? declaredBytes.LongLength,
            Convert.ToHexString(SHA256.HashData(declaredBytes)).ToLowerInvariant(),
            formatVersion: 1);
    }

    private static string WriteFile(string root, string relativePath, byte[] bytes)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private sealed record CookDefinition(
        string Variant,
        string Payload,
        params RuntimeAssetCookDependencyRequest[] Dependencies);

    private sealed record CookRun(
        RuntimeAssetCookResult Result,
        DelegateCooker Cooker);

    private sealed class DelegateCooker : IRuntimeAssetCooker
    {
        private readonly Func<RuntimeAssetCookContext, RuntimeAssetCookRequest, RuntimeAssetCookerOutput> m_Cook;

        public DelegateCooker(
            string providerId,
            IReadOnlyCollection<string> assetTypes,
            Func<RuntimeAssetCookContext, RuntimeAssetCookRequest, RuntimeAssetCookerOutput> cook)
        {
            ProviderId = providerId;
            AssetTypes = assetTypes;
            m_Cook = cook;
        }

        public string ProviderId { get; }

        public IReadOnlyCollection<string> AssetTypes { get; }

        public List<RuntimeAssetCookRequest> Requests { get; } = new();

        public RuntimeAssetCookerOutput Cook(
            RuntimeAssetCookContext context,
            RuntimeAssetCookRequest request)
        {
            Requests.Add(request);
            return m_Cook(context, request);
        }
    }

    public enum CookedFileFault
    {
        Missing,
        WrongSize,
        Tampered
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ArisenRuntimeCookingTests",
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
