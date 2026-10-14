using ArisenKernel.Lifecycle;
using ArisenKernel.Packages;
using Xunit;

namespace ArisenKernel.Tests;

public sealed class StartupTargetOptionsTests
{
    private static readonly Guid s_ManifestSceneGuid = Guid.Parse("6c761a9d-2d06-562e-9779-7748e8afe4d3");
    private static readonly Guid s_ManifestWorldGuid = Guid.Parse("b50c4c80-0720-4b63-b89b-af2203063180");
    private static readonly Guid s_OverrideGuid = Guid.Parse("9a9b4db5-c0a8-4f2e-8929-89464bea9d51");

    [Fact]
    public void WithoutArgumentsTheManifestDecides()
    {
        StartupTargetOptions options = StartupTargetOptions.Parse(["--frames", "1"]);

        Assert.False(options.Specified);
        ResolvedStartupTarget target = options.Resolve(Manifest());

        Assert.Equal(s_ManifestSceneGuid, target.Scene!.Guid);
        Assert.Equal(s_ManifestWorldGuid, target.World!.Guid);
    }

    [Fact]
    public void WorldArgumentReplacesTheWorldAndKeepsTheManifestScene()
    {
        StartupTargetOptions options = StartupTargetOptions.Parse(
            [StartupTargetOptions.WorldArgument, $"com.arisen.samples:{s_OverrideGuid}"]);

        Assert.True(options.Specified);
        ResolvedStartupTarget target = options.Resolve(Manifest());

        Assert.Equal(s_ManifestSceneGuid, target.Scene!.Guid);
        Assert.Equal(s_OverrideGuid, target.World!.Guid);
    }

    [Fact]
    public void SceneArgumentSuppressesTheManifestWorld()
    {
        // A caller that names a scene asked for that scene, not for the workspace's default world.
        StartupTargetOptions options = StartupTargetOptions.Parse(
            [StartupTargetOptions.SceneArgument, $"com.arisen.samples:{s_OverrideGuid}"]);

        ResolvedStartupTarget target = options.Resolve(Manifest());

        Assert.Equal(s_OverrideGuid, target.Scene!.Guid);
        Assert.Null(target.World);
        Assert.True(target.HasTarget);
    }

    [Fact]
    public void ResolveWithoutAManifestReportsNoTarget()
    {
        ResolvedStartupTarget target = StartupTargetOptions.Parse([]).Resolve(null);

        Assert.False(target.HasTarget);
        Assert.Equal("<none>", target.Describe());
    }

    [Fact]
    public void ArgumentNamesAreCaseInsensitive()
    {
        StartupTargetOptions options = StartupTargetOptions.Parse(
            ["--STARTUP-WORLD", $"com.arisen.samples:{s_OverrideGuid}"]);

        Assert.Equal(s_OverrideGuid, options.World!.Guid);
    }

    [Theory]
    // The value is one token so it survives every launcher: <packageId>:<guid>.
    [InlineData("--startup-world")]
    [InlineData("--startup-world", "com.arisen.samples")]
    [InlineData("--startup-world", "com.arisen.samples:not-a-guid")]
    [InlineData("--startup-world", ":9a9b4db5-c0a8-4f2e-8929-89464bea9d51")]
    [InlineData("--startup-world", "9a9b4db5-c0a8-4f2e-8929-89464bea9d51")]
    [InlineData("--startup-world", "com.arisen.samples:9a9b4db5-c0a8-4f2e-8929-89464bea9d51", "--startup-world", "com.arisen.samples:b50c4c80-0720-4b63-b89b-af2203063180")]
    [InlineData("--startup-scene", "com.arisen.samples:6c761a9d-2d06-562e-9779-7748e8afe4d3", "--startup-world", "com.arisen.samples:b50c4c80-0720-4b63-b89b-af2203063180")]
    public void InvalidSelectionsFailLoudly(params string[] args)
    {
        Assert.Throws<ArgumentException>(() => StartupTargetOptions.Parse(args));
    }

    private static ProjectManifest Manifest() => new()
    {
        StartupScene = new ProjectAssetReference
        {
            Guid = s_ManifestSceneGuid,
            PackageId = "com.arisen.samples"
        },
        StartupWorld = new ProjectAssetReference
        {
            Guid = s_ManifestWorldGuid,
            PackageId = "com.arisen.samples"
        }
    };
}
