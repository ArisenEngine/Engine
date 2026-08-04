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

    [Fact]
    public void VegetationFreeEditorCompositionResolvesWithoutVegetationPackages()
    {
        var result = ValidateComposition(
            isEditor: true,
            "com.arisen.editor",
            "com.arisen.generic-renderpipeline",
            "com.arisen.rhi.vulkan.native");

        Assert.True(result.Success, FormatErrors(result));
        Assert.DoesNotContain(result.PackageMap.Keys, IsVegetationPackage);
    }

    [Fact]
    public void VegetationRuntimeCompositionResolvesWithoutAdaptersOrVulkan()
    {
        var result = ValidateComposition(isEditor: false, "com.arisen.vegetation");

        Assert.True(result.Success, FormatErrors(result));
        Assert.Contains(
            "com.arisen.vegetation",
            result.PackageMap.Keys,
            StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            result.PackageMap.Keys,
            packageId => string.Equals(
                packageId,
                "com.arisen.generic-renderpipeline",
                StringComparison.OrdinalIgnoreCase));
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
                "com.arisen.rhi.vulkan.native",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VegetationPackageDependenciesPreserveOptionalAdapterBoundaries()
    {
        var genericRenderPipeline = ReadPackageManifest("com.arisen.generic-renderpipeline");
        var editor = ReadPackageManifest("com.arisen.editor");
        var vegetation = ReadPackageManifest("com.arisen.vegetation");
        var vegetationGenericRenderPipeline =
            ReadPackageManifest("com.arisen.vegetation.generic-renderpipeline");
        var vegetationEditor = ReadPackageManifest("com.arisen.vegetation.editor");

        Assert.DoesNotContain(genericRenderPipeline.Dependencies!.Keys, IsVegetationPackage);
        Assert.DoesNotContain(editor.Dependencies!.Keys, IsVegetationPackage);
        Assert.DoesNotContain(
            vegetation.Dependencies!.Keys,
            packageId =>
                IsVegetationAdapter(packageId) ||
                string.Equals(
                    packageId,
                    "com.arisen.rhi.vulkan.native",
                    StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            vegetationGenericRenderPipeline.Dependencies!.Keys,
            packageId => string.Equals(
                packageId,
                "com.arisen.vegetation",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            vegetationEditor.Dependencies!.Keys,
            packageId => string.Equals(
                packageId,
                "com.arisen.vegetation",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            vegetationGenericRenderPipeline.Dependencies.Keys,
            packageId => string.Equals(
                packageId,
                "com.arisen.rhi.vulkan.native",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            vegetationEditor.Dependencies.Keys,
            packageId => string.Equals(
                packageId,
                "com.arisen.rhi.vulkan.native",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VegetationDependsOnTerrainWithoutReversingTheDomainBoundary()
    {
        var vegetation = ReadPackageManifest("com.arisen.vegetation");
        var terrain = ReadPackageManifest("com.arisen.terrain");

        Assert.Contains(
            vegetation.Dependencies!.Keys,
            packageId => string.Equals(
                packageId,
                "com.arisen.terrain",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(terrain.Dependencies!.Keys, IsVegetationPackage);
    }

    [Fact]
    public void VegetationAdaptersLoadAfterTheirOwnedProviders()
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
            loadOrder["com.arisen.vegetation"] <
            loadOrder["com.arisen.vegetation.generic-renderpipeline"]);
        Assert.True(
            loadOrder["com.arisen.generic-renderpipeline"] <
            loadOrder["com.arisen.vegetation.generic-renderpipeline"]);
        Assert.True(
            loadOrder["com.arisen.vegetation"] <
            loadOrder["com.arisen.vegetation.editor"]);
        Assert.True(
            loadOrder["com.arisen.editor"] <
            loadOrder["com.arisen.vegetation.editor"]);
    }

    [Theory]
    [InlineData("Editor", true)]
    [InlineData("Development", false)]
    [InlineData("Production", false)]
    public void RuntimeProfilesSelectExpectedVegetationAdapters(
        string profile,
        bool expectEditorAdapter)
    {
        PackageValidationResult result = ValidateCanonicalProfile(profile);

        Assert.True(result.Success, FormatErrors(result));
        Assert.Contains(
            "com.arisen.vegetation",
            result.PackageMap.Keys,
            StringComparer.OrdinalIgnoreCase);
        Assert.Contains(
            "com.arisen.vegetation.generic-renderpipeline",
            result.PackageMap.Keys,
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal(
            expectEditorAdapter,
            result.PackageMap.Keys.Contains(
                "com.arisen.vegetation.editor",
                StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void RhiTestingProfileDoesNotSelectVegetationPackages()
    {
        PackageValidationResult result = ValidateCanonicalProfile("RHIVulkanTesting");

        Assert.True(result.Success, FormatErrors(result));
        Assert.DoesNotContain(result.PackageMap.Keys, IsVegetationPackage);
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

    private static PackageValidationResult ValidateCanonicalProfile(string profile)
    {
        string repositoryRoot = FindRepositoryRoot();
        string engineRoot = Path.Combine(repositoryRoot, "Arisen");
        string workspace = GetWorkspaceDirectory(repositoryRoot);
        var manifest = ManifestJson.DeserializeFile<ProjectManifest>(
            Path.Combine(workspace, "manifest.json"));

        Assert.NotNull(manifest);
        return PackageValidationService.Validate(manifest!, workspace, engineRoot, profile);
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

    private static bool IsVegetationPackage(string packageId)
    {
        return packageId.StartsWith("com.arisen.vegetation", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVegetationAdapter(string packageId)
    {
        return string.Equals(
                packageId,
                "com.arisen.vegetation.generic-renderpipeline",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                packageId,
                "com.arisen.vegetation.editor",
                StringComparison.OrdinalIgnoreCase);
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
