using System.Text.Json;
using ArisenBuildTool.Models;
using ArisenBuildTool.Services;
using Xunit;

namespace ArisenBuildTool.Tests;

public sealed class RuntimePackageMetadataDeploymentTests
{
    [Fact]
    public void DeployWritesDeterministicRelocatableMetadataAndRemovesStalePackages()
    {
        using var temp = new TempDirectory();
        string output = Path.Combine(temp.Path, "Build Output");
        Directory.CreateDirectory(Path.Combine(output, "Packages", "com.stale.package"));
        File.WriteAllText(
            Path.Combine(output, "Packages", "com.stale.package", "package.json"),
            "{\"id\":\"com.stale.package\"}");
        ProjectManifest project = CreateProject();
        List<PackageInfo> packages = CreatePackages(temp.Path);

        RuntimePackageMetadataDeploymentResult first =
            RuntimePackageMetadataDeploymentService.Deploy(
                project,
                "Production",
                packages,
                output,
                "Debug");
        byte[] firstProject = File.ReadAllBytes(first.ProjectManifestPath);
        byte[] firstResolved = File.ReadAllBytes(first.ResolvedManifestPath);
        byte[] firstLaunch = File.ReadAllBytes(first.LaunchConfigPath);

        RuntimePackageMetadataDeploymentResult second =
            RuntimePackageMetadataDeploymentService.Deploy(
                project,
                "Production",
                packages,
                output,
                "Debug");

        Assert.Equal(2, second.PackageCount);
        Assert.False(Directory.Exists(
            Path.Combine(second.PackagesRoot, "com.stale.package")));
        Assert.Equal(firstProject, File.ReadAllBytes(second.ProjectManifestPath));
        Assert.Equal(firstResolved, File.ReadAllBytes(second.ResolvedManifestPath));
        Assert.Equal(firstLaunch, File.ReadAllBytes(second.LaunchConfigPath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(
            output,
            ".runtime-metadata-*",
            SearchOption.TopDirectoryOnly));

        using JsonDocument launch = JsonDocument.Parse(firstLaunch);
        Assert.Equal(2, launch.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("Deployed", launch.RootElement.GetProperty("Mode").GetString());
        Assert.Equal("Production", launch.RootElement.GetProperty("Profile").GetString());
        Assert.Equal("Debug", launch.RootElement.GetProperty("Configuration").GetString());
        Assert.False(launch.RootElement.TryGetProperty("Workspace", out _));

        using JsonDocument runtimeProject = JsonDocument.Parse(firstProject);
        Assert.Equal(
            "b1000000-0000-0000-0000-000000000003",
            runtimeProject.RootElement.GetProperty("StartupWorld").GetProperty("Guid").GetString());
        JsonElement projectPackages = runtimeProject.RootElement.GetProperty("Packages");
        Assert.Equal(2, projectPackages.GetArrayLength());
        Assert.Equal(
            "file://Packages/com.test.foundation/",
            projectPackages[0].GetProperty("Url").GetString());
        Assert.Equal(
            "file://Packages/com.test.game/",
            projectPackages[1].GetProperty("Url").GetString());

        using JsonDocument resolved = JsonDocument.Parse(firstResolved);
        Assert.False(resolved.RootElement.GetProperty("EnableProfiler").GetBoolean());
        Assert.Equal("Debug", resolved.RootElement.GetProperty("Configuration").GetString());
        Assert.True(resolved.RootElement.GetProperty("NativePayloadsFinalized").GetBoolean());
        Assert.Empty(resolved.RootElement.GetProperty("NativePayloads").EnumerateArray());
        JsonElement resolvedPackages = resolved.RootElement.GetProperty("ResolvedPackages");
        Assert.Equal(2, resolvedPackages.GetArrayLength());
        Assert.Equal(
            "ArisenEngine.Test.GameSubsystem",
            resolvedPackages[1]
                .GetProperty("Subsystems")[0]
                .GetProperty("class")
                .GetString());
        string resolvedText = File.ReadAllText(second.ResolvedManifestPath);
        Assert.DoesNotContain(temp.Path, resolvedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Local/", resolvedText, StringComparison.OrdinalIgnoreCase);

        string relocated = Path.Combine(temp.Path, "Relocated");
        Directory.CreateDirectory(relocated);
        CopyDirectory(output, relocated);
        foreach (JsonElement package in resolvedPackages.EnumerateArray())
        {
            string url = package.GetProperty("Url").GetString()!;
            string descriptor = Path.Combine(
                relocated,
                url["file://".Length..].Replace('/', Path.DirectorySeparatorChar),
                "package.json");
            Assert.True(File.Exists(descriptor), descriptor);
        }
    }

    [Fact]
    public void ProfilerDisabledDeploymentOmitsConditionalRuntimeFromPackageMetadata()
    {
        using var temp = new TempDirectory();
        string output = Path.Combine(temp.Path, "Output");
        ProjectManifest project = CreateProject();
        project.Profiles = new Dictionary<string, ProfileDefinition>
        {
            ["Production"] = new ProfileDefinition { EnableProfiler = false }
        };
        var package = new PackageInfo
        {
            DirectoryPath = Path.Combine(temp.Path, "Local", "com.test.profiler"),
            Manifest = new PackageManifest
            {
                Id = "com.test.profiler",
                Name = "Profiler Runtime",
                Version = "1.0.0",
                Type = "native",
                NativeRuntimes = new Dictionary<string, List<JsonElement>>
                {
                    ["win-x64"] =
                    [
                        JsonSerializer.SerializeToElement(new
                        {
                            path = "TracyClient.dll",
                            source = "buildOutput",
                            requiresProfiler = true
                        })
                    ]
                }
            }
        };

        RuntimePackageMetadataDeploymentResult result =
            RuntimePackageMetadataDeploymentService.Deploy(
                project,
                "Production",
                [package],
                output,
                "Debug");

        using JsonDocument descriptor = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            result.PackagesRoot,
            package.Manifest.Id,
            "package.json")));
        using JsonDocument resolved = JsonDocument.Parse(File.ReadAllBytes(result.ResolvedManifestPath));
        Assert.Empty(descriptor.RootElement
            .GetProperty("nativeRuntimes")
            .GetProperty("win-x64")
            .EnumerateArray());
        Assert.Empty(resolved.RootElement
            .GetProperty("ResolvedPackages")[0]
            .GetProperty("NativeRuntimes")
            .GetProperty("win-x64")
            .EnumerateArray());
        Assert.False(File.Exists(Path.Combine(output, "TracyClient.dll")));
    }

    [Fact]
    public void DuplicatePackageIdsFailBeforeExistingMetadataChanges()
    {
        using var temp = new TempDirectory();
        string output = Path.Combine(temp.Path, "Output");
        Directory.CreateDirectory(output);
        string launchPath = Path.Combine(output, "launch.config.json");
        File.WriteAllText(launchPath, "existing-launch-config");
        List<PackageInfo> packages = CreatePackages(temp.Path);
        packages.Add(new PackageInfo
        {
            DirectoryPath = Path.Combine(temp.Path, "Duplicate"),
            Manifest = new PackageManifest
            {
                Id = "COM.TEST.GAME",
                Name = "Duplicate",
                Version = "1.0.0",
                Type = "managed"
            }
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            RuntimePackageMetadataDeploymentService.Deploy(
                CreateProject(),
                "Production",
                packages,
                output,
                "Debug"));

        Assert.Contains("duplicate package id", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("existing-launch-config", File.ReadAllText(launchPath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(
            output,
            ".runtime-metadata-*",
            SearchOption.TopDirectoryOnly));
    }

    private static ProjectManifest CreateProject()
    {
        return new ProjectManifest
        {
            Name = "Runtime Metadata Test",
            EngineVersion = "Current",
            StartupScene = new ProjectAssetReference
            {
                Guid = Guid.Parse("b1000000-0000-0000-0000-000000000001"),
                PackageId = "com.test.game"
            },
            StartupWorld = new ProjectAssetReference
            {
                Guid = Guid.Parse("b1000000-0000-0000-0000-000000000003"),
                PackageId = "com.test.game"
            },
            RenderPipeline = new ProjectAssetReference
            {
                Guid = Guid.Parse("b1000000-0000-0000-0000-000000000002"),
                PackageId = "com.test.game"
            }
        };
    }

    private static List<PackageInfo> CreatePackages(string root)
    {
        return
        [
            new PackageInfo
            {
                DirectoryPath = Path.Combine(root, "Local", "com.test.foundation"),
                Manifest = new PackageManifest
                {
                    Id = "com.test.foundation",
                    Name = "Foundation",
                    Version = "1.0.0",
                    Type = "managed",
                    Entry = new PackageEntry
                    {
                        Assembly = "Com.Test.Foundation.dll",
                        Class = "ArisenEngine.Test.FoundationPackage"
                    }
                }
            },
            new PackageInfo
            {
                DirectoryPath = Path.Combine(root, "Local", "com.test.game"),
                Manifest = new PackageManifest
                {
                    Id = "com.test.game",
                    Name = "Game",
                    Version = "1.0.0",
                    Type = "managed",
                    Entry = new PackageEntry
                    {
                        Assembly = "Com.Test.Game.dll",
                        Class = "ArisenEngine.Test.GamePackage"
                    },
                    Dependencies = new Dictionary<string, string>
                    {
                        ["com.test.foundation"] = "1.0.0"
                    },
                    Subsystems =
                    [
                        new PackageSubsystem
                        {
                            Class = "ArisenEngine.Test.GameSubsystem",
                            Phase = "Running",
                            Priority = 50
                        }
                    ]
                }
            }
        ];
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string directory in Directory.EnumerateDirectories(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ArisenRuntimePackageMetadataTests",
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
