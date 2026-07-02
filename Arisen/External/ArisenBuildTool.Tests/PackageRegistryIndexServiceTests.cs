using System.IO.Compression;
using System.Text.Json;
using ArisenBuildTool.Models;
using ArisenBuildTool.Services;
using Xunit;

namespace ArisenBuildTool.Tests;

public sealed class PackageRegistryIndexServiceTests
{
    [Fact]
    public void BuildScansPackageArchivesAndAddsIntegrityMetadata()
    {
        using var workspace = RegistryWorkspace.Create();
        workspace.PackPackage("com.test.zeta", "2.0.0");
        workspace.PackPackage("com.test.alpha", "1.0.0");

        var index = PackageRegistryIndexService.Build(workspace.OutputPath, "https://packages.example.test/arisen");

        Assert.Equal(2, index.Packages.Count);
        Assert.Equal("com.test.alpha", index.Packages[0].Id);
        Assert.Equal("1.0.0", index.Packages[0].Version);
        Assert.Equal("https://packages.example.test/arisen/com.test.alpha-1.0.0.zip", index.Packages[0].Archive.Url);
        Assert.Equal(64, index.Packages[0].Archive.Sha256.Length);
        Assert.True(index.Packages[0].Archive.SizeBytes > 0);
        Assert.Equal("com.test.zeta", index.Packages[1].Id);
    }

    [Fact]
    public void WriteProducesDeterministicJson()
    {
        using var workspace = RegistryWorkspace.Create();
        workspace.PackPackage("com.test.beta", "1.0.0");
        workspace.PackPackage("com.test.alpha", "1.0.0");

        string firstPath = Path.Combine(workspace.RootPath, "registry-a.json");
        string secondPath = Path.Combine(workspace.RootPath, "registry-b.json");

        PackageRegistryIndexService.Write(workspace.OutputPath, firstPath, "./packages");
        PackageRegistryIndexService.Write(workspace.OutputPath, secondPath, "./packages");

        Assert.Equal(File.ReadAllText(firstPath), File.ReadAllText(secondPath));

        using var document = JsonDocument.Parse(File.ReadAllText(firstPath));
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("generatedAtUtc", out _));
    }

    [Fact]
    public void BuildFailsWhenArchiveHasNoRootPackageJson()
    {
        using var workspace = RegistryWorkspace.Create();
        Directory.CreateDirectory(workspace.OutputPath);
        string archivePath = Path.Combine(workspace.OutputPath, "broken.zip");

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("README.md");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("missing package manifest");
        }

        Assert.Throws<InvalidDataException>(() => PackageRegistryIndexService.Build(workspace.OutputPath));
    }

    private sealed class RegistryWorkspace : IDisposable
    {
        private RegistryWorkspace(string root)
        {
            RootPath = root;
            OutputPath = Path.Combine(root, "out");
        }

        public string RootPath { get; }

        public string OutputPath { get; }

        public static RegistryWorkspace Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "ArisenBuildTool.Registry.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new RegistryWorkspace(root);
        }

        public string PackPackage(string id, string version)
        {
            var package = AddPackage(id, version);
            return PackagePackService.Pack(package, OutputPath);
        }

        private PackageInfo AddPackage(string id, string version)
        {
            string packageDir = Path.Combine(RootPath, "Local", id);
            Directory.CreateDirectory(Path.Combine(packageDir, "Source"));

            File.WriteAllText(Path.Combine(packageDir, "package.json"), $$"""
{
  "id": "{{id}}",
  "name": "{{id}}",
  "version": "{{version}}",
  "description": "Test package {{id}}",
  "layer": "user",
  "type": "managed",
  "dependencies": {}
}
""");
            File.WriteAllText(Path.Combine(packageDir, "Source", "Feature.cs"), "namespace TestPackage; public sealed class Feature { }");

            return new PackageInfo
            {
                DirectoryPath = packageDir,
                Manifest = new PackageManifest
                {
                    Id = id,
                    Name = id,
                    Version = version,
                    Description = $"Test package {id}",
                    Layer = "user",
                    Type = "managed"
                }
            };
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup; failed deletion should not mask test results.
            }
        }
    }
}
