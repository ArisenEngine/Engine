using System.Security.Cryptography;
using System.Text;
using ArisenEngine.Core.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RuntimeAssetDeploymentTests
{
    private static readonly Guid s_SceneGuid = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid s_TextureGuid = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Deploy_CopiesExactlyDeclaredArtifactsAndPublishesCanonicalCatalog()
    {
        using var temp = new TempDirectory();
        RuntimeAssetCookResult cookResult = CreateTwoArtifactResult(temp.Path, "first");
        string outputRoot = Path.Combine(temp.Path, "bin", "Production", "Release");

        RuntimeAssetDeploymentResult deployment = RuntimeAssetDeployment.Deploy(
            cookResult,
            outputRoot);

        Assert.Equal(Path.GetFullPath(outputRoot), deployment.OutputRoot);
        Assert.Equal(2, deployment.ArtifactCount);
        Assert.Equal(0, deployment.ReusedArtifactCount);
        Assert.Equal(2, deployment.CopiedArtifactCount);
        Assert.Equal(
            cookResult.Catalog.Serialize(),
            File.ReadAllBytes(deployment.CatalogPath));
        Assert.Equal(
            new[]
            {
                "com.arisen.rendering/textures/albedo.aritex",
                "com.arisen.resources/scenes/startup.ariscene"
            },
            EnumerateRelativeFiles(deployment.ContentRoot));
        cookResult.Catalog.ValidateDeployment(deployment.ContentRoot);
        Assert.Equal(
            cookResult.Catalog.Serialize(),
            RuntimeAssetCatalog.Parse(File.ReadAllBytes(deployment.CatalogPath)).Serialize());
    }

    [Fact]
    public void Deploy_ReusesUnchangedArtifactsByHashAndFormatVersion()
    {
        using var temp = new TempDirectory();
        string outputRoot = Path.Combine(temp.Path, "output");
        RuntimeAssetCookResult cookResult = CreateTwoArtifactResult(temp.Path, "stable");
        RuntimeAssetDeploymentResult first = RuntimeAssetDeployment.Deploy(cookResult, outputRoot);
        DateTime preservedWriteTime = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        string[] deployedFiles = EnumerateRelativeFiles(first.ContentRoot);
        foreach (string relativePath in deployedFiles)
        {
            File.SetLastWriteTimeUtc(
                Path.Combine(first.ContentRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)),
                preservedWriteTime);
        }

        RuntimeAssetDeploymentResult second = RuntimeAssetDeployment.Deploy(
            CreateTwoArtifactResult(temp.Path, "stable"),
            outputRoot);

        Assert.Equal(2, second.ArtifactCount);
        Assert.Equal(2, second.ReusedArtifactCount);
        Assert.Equal(0, second.CopiedArtifactCount);
        foreach (string relativePath in deployedFiles)
        {
            Assert.Equal(
                preservedWriteTime,
                File.GetLastWriteTimeUtc(Path.Combine(
                    second.ContentRoot,
                    relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        cookResult.Catalog.ValidateDeployment(second.ContentRoot);
    }

    [Fact]
    public void Deploy_FormatVersionChangeCopiesOnlyChangedArtifact()
    {
        using var temp = new TempDirectory();
        string outputRoot = Path.Combine(temp.Path, "output");
        RuntimeAssetCookResult cookResult = CreateTwoArtifactResult(temp.Path, "stable");
        RuntimeAssetDeployment.Deploy(cookResult, outputRoot);
        RuntimeAssetCatalogArtifact[] artifacts = cookResult.Catalog.Artifacts
            .Select(artifact => artifact.Guid == s_SceneGuid
                ? new RuntimeAssetCatalogArtifact(
                    artifact.Guid,
                    artifact.PackageId,
                    artifact.AssetType,
                    artifact.Variant,
                    artifact.OutputRelativePath,
                    artifact.SizeInBytes,
                    artifact.Sha256,
                    formatVersion: 2,
                    artifact.Dependencies)
                : artifact)
            .ToArray();
        RuntimeAssetCatalog catalog = RuntimeAssetCatalog.Create(
            cookResult.Catalog.TargetProfile,
            cookResult.Catalog.Roots,
            artifacts);
        IReadOnlyDictionary<RuntimeAssetIdentity, string> sourcePaths = cookResult.Files
            .ToDictionary(file => file.Artifact.Identity, file => file.SourcePath);
        var versionChanged = new RuntimeAssetCookResult(
            catalog,
            catalog.Artifacts
                .Select(artifact => new RuntimeAssetCookedFile(
                    artifact,
                    sourcePaths[artifact.Identity]))
                .ToArray());

        RuntimeAssetDeploymentResult deployment = RuntimeAssetDeployment.Deploy(
            versionChanged,
            outputRoot);

        Assert.Equal(2, deployment.ArtifactCount);
        Assert.Equal(1, deployment.ReusedArtifactCount);
        Assert.Equal(1, deployment.CopiedArtifactCount);
        catalog.ValidateDeployment(deployment.ContentRoot);
    }

    [Fact]
    public void Deploy_CorruptExistingArtifactIsCopiedWhileValidArtifactIsReused()
    {
        using var temp = new TempDirectory();
        string outputRoot = Path.Combine(temp.Path, "output");
        RuntimeAssetCookResult cookResult = CreateTwoArtifactResult(temp.Path, "stable");
        RuntimeAssetDeploymentResult first = RuntimeAssetDeployment.Deploy(cookResult, outputRoot);
        string texturePath = Path.Combine(
            first.ContentRoot,
            "com.arisen.rendering",
            "textures",
            "albedo.aritex");
        File.WriteAllBytes(texturePath, "corrupt"u8.ToArray());

        RuntimeAssetDeploymentResult second = RuntimeAssetDeployment.Deploy(
            cookResult,
            outputRoot);

        Assert.Equal(1, second.ReusedArtifactCount);
        Assert.Equal(1, second.CopiedArtifactCount);
        cookResult.Catalog.ValidateDeployment(second.ContentRoot);
    }

    [Fact]
    public void Deploy_ReplacesOwnedContentAndRemovesStaleFiles()
    {
        using var temp = new TempDirectory();
        string outputRoot = Path.Combine(temp.Path, "output");
        RuntimeAssetDeployment.Deploy(CreateTwoArtifactResult(temp.Path, "first"), outputRoot);
        WriteFile(
            Path.Combine(outputRoot, RuntimeAssetDeployment.ContentDirectoryName),
            "untracked/obsolete.bin",
            "obsolete"u8.ToArray());

        byte[] replacementBytes = "replacement-scene"u8.ToArray();
        string sourcePath = WriteFile(
            temp.Path,
            "sources/second/startup.ariscene",
            replacementBytes);
        RuntimeAssetCatalogArtifact scene = Artifact(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            "com.arisen.resources/scenes/startup.ariscene",
            replacementBytes);
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
        var replacement = new RuntimeAssetCookResult(
            catalog,
            new[] { new RuntimeAssetCookedFile(catalog.Artifacts[0], sourcePath) });

        RuntimeAssetDeploymentResult deployment = RuntimeAssetDeployment.Deploy(
            replacement,
            outputRoot);

        Assert.Equal(0, deployment.ReusedArtifactCount);
        Assert.Equal(1, deployment.CopiedArtifactCount);
        Assert.Equal(
            new[] { "com.arisen.resources/scenes/startup.ariscene" },
            EnumerateRelativeFiles(deployment.ContentRoot));
        Assert.Equal(
            replacementBytes,
            File.ReadAllBytes(Path.Combine(
                deployment.ContentRoot,
                "com.arisen.resources",
                "scenes",
                "startup.ariscene")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(
            outputRoot,
            ".arisen-runtime-assets-*",
            SearchOption.TopDirectoryOnly));
    }

    [Theory]
    [InlineData(DeploymentSourceFault.Missing, "is missing")]
    [InlineData(DeploymentSourceFault.Tampered, "SHA-256 mismatch")]
    public void Deploy_InvalidSourcePreservesPreviousDeployment(
        DeploymentSourceFault fault,
        string expectedDiagnostic)
    {
        using var temp = new TempDirectory();
        string outputRoot = Path.Combine(temp.Path, "output");
        RuntimeAssetDeploymentResult originalDeployment = RuntimeAssetDeployment.Deploy(
            CreateTwoArtifactResult(temp.Path, "stable"),
            outputRoot);
        byte[] originalCatalog = File.ReadAllBytes(originalDeployment.CatalogPath);
        string[] originalFiles = EnumerateRelativeFiles(originalDeployment.ContentRoot);
        byte[][] originalPayloads = originalFiles
            .Select(path => File.ReadAllBytes(Path.Combine(
                originalDeployment.ContentRoot,
                path.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();

        RuntimeAssetCookResult invalid = CreateSingleArtifactResult(
            temp.Path,
            "invalid",
            "expected"u8.ToArray());
        if (fault == DeploymentSourceFault.Missing)
        {
            File.Delete(invalid.Files[0].SourcePath);
        }
        else
        {
            File.WriteAllBytes(invalid.Files[0].SourcePath, "modified"u8.ToArray());
        }

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            RuntimeAssetDeployment.Deploy(invalid, outputRoot));

        Assert.Contains(expectedDiagnostic, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalCatalog, File.ReadAllBytes(originalDeployment.CatalogPath));
        Assert.Equal(originalFiles, EnumerateRelativeFiles(originalDeployment.ContentRoot));
        for (int index = 0; index < originalFiles.Length; index++)
        {
            Assert.Equal(
                originalPayloads[index],
                File.ReadAllBytes(Path.Combine(
                    originalDeployment.ContentRoot,
                    originalFiles[index].Replace('/', Path.DirectorySeparatorChar))));
        }

        Assert.Empty(Directory.EnumerateFileSystemEntries(
            outputRoot,
            ".arisen-runtime-assets-*",
            SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void Deploy_RejectsCookedFileMetadataThatDiffersFromCatalog()
    {
        using var temp = new TempDirectory();
        RuntimeAssetCookResult valid = CreateSingleArtifactResult(
            temp.Path,
            "valid",
            "scene"u8.ToArray());
        RuntimeAssetCatalogArtifact catalogArtifact = valid.Catalog.Artifacts[0];
        var mismatchedArtifact = new RuntimeAssetCatalogArtifact(
            catalogArtifact.Guid,
            catalogArtifact.PackageId,
            catalogArtifact.AssetType,
            catalogArtifact.Variant,
            "com.arisen.resources/scenes/different.ariscene",
            catalogArtifact.SizeInBytes,
            catalogArtifact.Sha256,
            catalogArtifact.FormatVersion,
            catalogArtifact.Dependencies);
        var mismatched = new RuntimeAssetCookResult(
            valid.Catalog,
            new[] { new RuntimeAssetCookedFile(mismatchedArtifact, valid.Files[0].SourcePath) });

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            RuntimeAssetDeployment.Deploy(mismatched, Path.Combine(temp.Path, "output")));

        Assert.Contains("does not exactly match", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(
            temp.Path,
            "output",
            RuntimeAssetDeployment.ContentDirectoryName)));
    }

    [Fact]
    public void Deploy_RejectsDuplicateCookedFileMappings()
    {
        using var temp = new TempDirectory();
        RuntimeAssetCookResult valid = CreateTwoArtifactResult(temp.Path, "duplicate");
        RuntimeAssetCookedFile duplicate = valid.Files[0];
        var invalid = new RuntimeAssetCookResult(valid.Catalog, new[] { duplicate, duplicate });

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            RuntimeAssetDeployment.Deploy(invalid, Path.Combine(temp.Path, "output")));

        Assert.Contains("duplicate artifact", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RuntimeAssetCookResult CreateTwoArtifactResult(string root, string name)
    {
        byte[] sceneBytes = Encoding.UTF8.GetBytes($"scene-{name}");
        byte[] textureBytes = Encoding.UTF8.GetBytes($"texture-{name}");
        string scenePath = WriteFile(root, $"sources/{name}/startup.ariscene", sceneBytes);
        string texturePath = WriteFile(root, $"sources/{name}/albedo.aritex", textureBytes);
        RuntimeAssetCatalogArtifact texture = Artifact(
            s_TextureGuid,
            "com.arisen.rendering",
            "Texture2D",
            "rgba8.srgb.nomips",
            "com.arisen.rendering/textures/albedo.aritex",
            textureBytes);
        RuntimeAssetCatalogArtifact scene = Artifact(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            "com.arisen.resources/scenes/startup.ariscene",
            sceneBytes,
            new RuntimeAssetCatalogDependency(
                texture.Guid,
                texture.PackageId,
                texture.AssetType,
                texture.Variant,
                Required: true));
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
            new[] { texture, scene });
        var sourceByIdentity = new Dictionary<RuntimeAssetIdentity, string>
        {
            [scene.Identity] = scenePath,
            [texture.Identity] = texturePath
        };
        RuntimeAssetCookedFile[] files = catalog.Artifacts
            .Reverse()
            .Select(artifact => new RuntimeAssetCookedFile(
                artifact,
                sourceByIdentity[artifact.Identity]))
            .ToArray();
        return new RuntimeAssetCookResult(catalog, files);
    }

    private static RuntimeAssetCookResult CreateSingleArtifactResult(
        string root,
        string name,
        byte[] bytes)
    {
        string sourcePath = WriteFile(root, $"sources/{name}/startup.ariscene", bytes);
        RuntimeAssetCatalogArtifact scene = Artifact(
            s_SceneGuid,
            "com.arisen.resources",
            "Scene",
            "runtime.scene.v1",
            "com.arisen.resources/scenes/startup.ariscene",
            bytes);
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
        return new RuntimeAssetCookResult(
            catalog,
            new[] { new RuntimeAssetCookedFile(catalog.Artifacts[0], sourcePath) });
    }

    private static RuntimeAssetCatalogArtifact Artifact(
        Guid guid,
        string packageId,
        string assetType,
        string variant,
        string outputRelativePath,
        byte[] bytes,
        params RuntimeAssetCatalogDependency[] dependencies)
    {
        return new RuntimeAssetCatalogArtifact(
            guid,
            packageId,
            assetType,
            variant,
            outputRelativePath,
            bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            formatVersion: 1,
            dependencies);
    }

    private static string WriteFile(string root, string relativePath, byte[] bytes)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string[] EnumerateRelativeFiles(string contentRoot)
    {
        return Directory.GetFiles(contentRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(contentRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ArisenRuntimeDeploymentTests",
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

    public enum DeploymentSourceFault
    {
        Missing,
        Tampered
    }
}
