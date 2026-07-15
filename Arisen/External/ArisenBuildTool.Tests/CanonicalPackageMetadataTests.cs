using ArisenBuildTool.Services;
using Xunit;

namespace ArisenBuildTool.Tests;

public sealed class CanonicalPackageMetadataTests
{
    [Fact]
    public void EcsPackageDeclaresSceneSubsystem()
    {
        string packageDirectory = Path.Combine(
            FindRepositoryRoot(),
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.ecs");

        var manifest = PackageManifestService.ReadEffectiveManifest(packageDirectory);

        Assert.NotNull(manifest);
        var subsystem = Assert.Single(
            manifest!.Subsystems!,
            item => item.Class == "ArisenEngine.ECS.Lifecycle.SceneSubsystem");
        Assert.Equal("Init", subsystem.Phase);
        Assert.Equal(50, subsystem.Priority);
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
