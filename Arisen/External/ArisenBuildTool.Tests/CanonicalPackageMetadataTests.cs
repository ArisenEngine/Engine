using ArisenBuildTool.Models;
using ArisenBuildTool.Services;
using ArisenBuildTool.Utils;
using Xunit;

namespace ArisenBuildTool.Tests;

public sealed class CanonicalPackageMetadataTests
{
    private const string BoundaryProfile = "PackageBoundary";

    [Fact]
    public void EcsPackageDeclaresSceneSubsystem()
    {
        string packageDirectory = GetPackageDirectory("com.arisen.ecs");

        var manifest = PackageManifestService.ReadEffectiveManifest(packageDirectory);

        Assert.NotNull(manifest);
        var subsystem = Assert.Single(
            manifest!.Subsystems!,
            item => item.Class == "ArisenEngine.ECS.Lifecycle.SceneSubsystem");
        Assert.Equal("Init", subsystem.Phase);
        Assert.Equal(50, subsystem.Priority);
    }

    [Fact]
    public void TerrainFreeEditorCompositionResolvesWithoutTerrainPackages()
    {
        var result = ValidateComposition(
            isEditor: true,
            "com.arisen.editor",
            "com.arisen.generic-renderpipeline",
            "com.arisen.rhi.vulkan.native");

        Assert.True(result.Success, FormatErrors(result));
        Assert.Contains("com.arisen.editor", result.PackageMap.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(
            "com.arisen.generic-renderpipeline",
            result.PackageMap.Keys,
            StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.PackageMap.Keys, IsTerrainPackage);
    }

    [Fact]
    public void TerrainRuntimeCompositionResolvesWithoutEditorOrGenericRenderPipeline()
    {
        var result = ValidateComposition(isEditor: false, "com.arisen.terrain");

        Assert.True(result.Success, FormatErrors(result));
        Assert.Contains("com.arisen.terrain", result.PackageMap.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            result.PackageMap.Keys,
            packageId => string.Equals(
                packageId,
                "com.arisen.editor",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            result.PackageMap.Keys,
            packageId => string.Equals(
                packageId,
                "com.arisen.generic-renderpipeline",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TerrainPackageDependenciesPreserveOptionalAdapterBoundaries()
    {
        var genericRenderPipeline = ReadPackageManifest("com.arisen.generic-renderpipeline");
        var editor = ReadPackageManifest("com.arisen.editor");
        var terrain = ReadPackageManifest("com.arisen.terrain");

        Assert.DoesNotContain(genericRenderPipeline.Dependencies!.Keys, IsTerrainPackage);
        Assert.DoesNotContain(editor.Dependencies!.Keys, IsTerrainPackage);
        Assert.DoesNotContain(
            terrain.Dependencies!.Keys,
            packageId => string.Equals(
                packageId,
                "com.arisen.generic-renderpipeline",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            terrain.Dependencies.Keys,
            packageId => string.Equals(
                packageId,
                "com.arisen.editor",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TerrainAdaptersLoadAfterTheirOwnedProviders()
    {
        string repositoryRoot = FindRepositoryRoot();
        string engineRoot = Path.Combine(repositoryRoot, "Arisen");
        string workspace = GetWorkspaceDirectory(repositoryRoot);
        var manifest = ManifestJson.DeserializeFile<ProjectManifest>(
            Path.Combine(workspace, "manifest.json"));

        Assert.NotNull(manifest);
        var result = PackageValidationService.Validate(manifest!, workspace, engineRoot, "Editor");
        Assert.True(result.Success, FormatErrors(result));

        var loadOrder = result.SortedPackages
            .Select((package, index) => (package.Manifest.Id, index))
            .ToDictionary(item => item.Id, item => item.index, StringComparer.OrdinalIgnoreCase);

        Assert.True(
            loadOrder["com.arisen.terrain"] <
            loadOrder["com.arisen.terrain.generic-renderpipeline"]);
        Assert.True(
            loadOrder["com.arisen.generic-renderpipeline"] <
            loadOrder["com.arisen.terrain.generic-renderpipeline"]);
        Assert.True(
            loadOrder["com.arisen.editor"] <
            loadOrder["com.arisen.terrain.editor"]);
    }

    private static PackageValidationResult ValidateComposition(
        bool isEditor,
        params string[] packageIds)
    {
        string repositoryRoot = FindRepositoryRoot();
        string engineRoot = Path.Combine(repositoryRoot, "Arisen");
        string workspace = GetWorkspaceDirectory(repositoryRoot);
        var manifest = new ProjectManifest
        {
            Name = "CanonicalPackageBoundaryTests",
            EngineVersion = "Current",
            Packages = packageIds
                .Select(packageId => new PackageRequirement
                {
                    Id = packageId,
                    Url = $"file://Local/{packageId}",
                    Version = "1.0.0"
                })
                .ToList(),
            Profiles = new Dictionary<string, ProfileDefinition>
            {
                [BoundaryProfile] = new() { IsEditor = isEditor }
            }
        };

        return PackageValidationService.Validate(
            manifest,
            workspace,
            engineRoot,
            BoundaryProfile);
    }

    private static PackageManifest ReadPackageManifest(string packageId)
    {
        var manifest = PackageManifestService.ReadEffectiveManifest(
            GetPackageDirectory(packageId));
        return Assert.IsType<PackageManifest>(manifest);
    }

    private static string GetPackageDirectory(string packageId)
    {
        return Path.Combine(
            GetWorkspaceDirectory(FindRepositoryRoot()),
            "Local",
            packageId);
    }

    private static string GetWorkspaceDirectory(string repositoryRoot)
    {
        return Path.Combine(
            repositoryRoot,
            "Arisen",
            "Development",
            "PackageGame");
    }

    private static bool IsTerrainPackage(string packageId)
    {
        return packageId.StartsWith("com.arisen.terrain", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatErrors(PackageValidationResult result)
    {
        return string.Join(Environment.NewLine, result.Errors);
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

        throw new InvalidOperationException(
            "Could not locate repository root from the test output directory.");
    }
}
