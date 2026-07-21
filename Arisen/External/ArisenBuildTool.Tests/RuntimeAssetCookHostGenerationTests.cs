using ArisenBuildTool.Models;
using ArisenBuildTool.Services;
using Xunit;

namespace ArisenBuildTool.Tests;

public sealed class RuntimeAssetCookHostGenerationTests
{
    [Fact]
    public void ProductionEntryProjectDispatchesAndRunsPackageCookHost()
    {
        using var temp = new TempDirectory();
        string workspace = Path.Combine(temp.Path, "Workspace & Cook");
        string projects = Path.Combine(workspace, ".arisen", "Projects", "Production");
        string engine = Path.Combine(temp.Path, "Engine");
        Directory.CreateDirectory(projects);
        Directory.CreateDirectory(Path.Combine(engine, "ArisenKernel"));
        File.WriteAllText(
            Path.Combine(engine, "ArisenKernel", "ArisenKernel.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        List<PackageInfo> packages = CreatePackages(workspace);

        SolutionGeneratorService.Generate(
            workspace,
            projects,
            engine,
            packages,
            "PackageGame",
            new ProjectManifest { Name = "PackageGame" },
            "Production",
            isEditor: false,
            enableProfiler: false);

        string entryDirectory = Path.Combine(projects, "PackageGame");
        string project = File.ReadAllText(Path.Combine(entryDirectory, "PackageGame.csproj"));
        string program = File.ReadAllText(Path.Combine(entryDirectory, "Program.cs"));

        Assert.Contains(
            "..\\Com.Arisen.Core\\Com.Arisen.Core.csproj\" />",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Com.Arisen.Core.csproj\" ReferenceOutputAssembly=\"false\"",
            project,
            StringComparison.Ordinal);
        Assert.Contains("Name=\"ArisenCookRuntimeAssets\"", project, StringComparison.Ordinal);
        Assert.Contains("AfterTargets=\"Build\"", project, StringComparison.Ordinal);
        Assert.Contains("$(ArisenSkipAssetCook)", project, StringComparison.Ordinal);
        Assert.Contains(
            "&quot;$(TargetDir)$(AssemblyName).exe&quot;",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet &quot;$(TargetPath)&quot;", project, StringComparison.Ordinal);
        Assert.Contains("--arisen-cook-runtime-assets", project, StringComparison.Ordinal);
        Assert.Contains(
            "--output-root &quot;$(TargetDir).&quot;",
            project,
            StringComparison.Ordinal);
        Assert.Contains("deploy-runtime-metadata", project, StringComparison.Ordinal);
        Assert.Contains("--engine &quot;", project, StringComparison.Ordinal);
        Assert.True(
            project.IndexOf("--arisen-cook-runtime-assets", StringComparison.Ordinal) <
            project.IndexOf("deploy-runtime-metadata", StringComparison.Ordinal),
            "Runtime metadata must be finalized only after source-backed cooking succeeds.");
        Assert.Contains("Workspace &amp; Cook", project, StringComparison.Ordinal);
        Assert.Contains("RuntimeAssetCookHost.IsCookCommand", program, StringComparison.Ordinal);
        Assert.Contains("RuntimeAssetCookHost.Run", program, StringComparison.Ordinal);
        Assert.Contains("EngineBootstrapper.Run", program, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentEntryKeepsManualCookDispatchWithoutAutomaticTarget()
    {
        using var temp = new TempDirectory();
        string workspace = Path.Combine(temp.Path, "Workspace");
        string projects = Path.Combine(workspace, ".arisen", "Projects", "Development");
        string engine = Path.Combine(temp.Path, "Engine");
        Directory.CreateDirectory(projects);
        List<PackageInfo> packages = CreatePackages(workspace);

        SolutionGeneratorService.Generate(
            workspace,
            projects,
            engine,
            packages,
            "PackageGame",
            new ProjectManifest { Name = "PackageGame" },
            "Development",
            isEditor: false,
            enableProfiler: true);

        string entryDirectory = Path.Combine(projects, "PackageGame");
        string project = File.ReadAllText(Path.Combine(entryDirectory, "PackageGame.csproj"));
        string program = File.ReadAllText(Path.Combine(entryDirectory, "Program.cs"));

        Assert.DoesNotContain("ArisenCookRuntimeAssets", project, StringComparison.Ordinal);
        Assert.DoesNotContain("deploy-runtime-metadata", project, StringComparison.Ordinal);
        Assert.Contains("RuntimeAssetCookHost.IsCookCommand", program, StringComparison.Ordinal);
    }

    private static List<PackageInfo> CreatePackages(string workspace)
    {
        return
        [
            new PackageInfo
            {
                DirectoryPath = Path.Combine(workspace, "Local", "com.arisen.core"),
                Manifest = new PackageManifest
                {
                    Id = "com.arisen.core",
                    Name = "Core",
                    Version = "1.0.0",
                    Type = "managed"
                }
            },
            new PackageInfo
            {
                DirectoryPath = Path.Combine(workspace, "Local", "com.user.game"),
                Manifest = new PackageManifest
                {
                    Id = "com.user.game",
                    Name = "Game",
                    Version = "1.0.0",
                    Type = "managed"
                }
            }
        ];
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ArisenCookHostGenerationTests",
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
