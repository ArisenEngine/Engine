using System.IO.Compression;
using ArisenBuildTool.Models;
using ArisenBuildTool.Services;
using Xunit;

namespace ArisenBuildTool.Tests;

public sealed class PackagePackServiceTests
{
    [Fact]
    public void PackCreatesArchiveWithPackageJsonAtRoot()
    {
        using var workspace = PackWorkspace.Create();
        var package = workspace.AddPackage("com.test.pack", "1.2.3");

        string archivePath = PackagePackService.Pack(package, workspace.OutputPath);

        using var archive = ZipFile.OpenRead(archivePath);
        Assert.Equal("com.test.pack-1.2.3.zip", Path.GetFileName(archivePath));
        Assert.Contains(archive.Entries, entry => entry.FullName == "package.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "Source/Feature.cs");
    }

    [Fact]
    public void PackExcludesBuildAndGeneratedDirectories()
    {
        using var workspace = PackWorkspace.Create();
        var package = workspace.AddPackage("com.test.clean", "1.0.0");
        File.WriteAllText(Path.Combine(package.DirectoryPath, "bin", "Debug", "ignored.dll"), "bin");
        File.WriteAllText(Path.Combine(package.DirectoryPath, "obj", "ignored.obj"), "obj");
        File.WriteAllText(Path.Combine(package.DirectoryPath, ".arisen", "ignored.json"), "generated");

        string archivePath = PackagePackService.Pack(package, workspace.OutputPath);

        using var archive = ZipFile.OpenRead(archivePath);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("bin/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("obj/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith(".arisen/", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class PackWorkspace : IDisposable
    {
        private readonly string m_Root;

        private PackWorkspace(string root)
        {
            m_Root = root;
            OutputPath = Path.Combine(root, "out");
        }

        public string OutputPath { get; }

        public static PackWorkspace Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "ArisenBuildTool.Pack.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new PackWorkspace(root);
        }

        public PackageInfo AddPackage(string id, string version)
        {
            string packageDir = Path.Combine(m_Root, "Local", id);
            Directory.CreateDirectory(Path.Combine(packageDir, "Source"));
            Directory.CreateDirectory(Path.Combine(packageDir, "bin", "Debug"));
            Directory.CreateDirectory(Path.Combine(packageDir, "obj"));
            Directory.CreateDirectory(Path.Combine(packageDir, ".arisen"));

            File.WriteAllText(Path.Combine(packageDir, "package.json"), $$"""
{
  "id": "{{id}}",
  "name": "{{id}}",
  "version": "{{version}}",
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
                    Layer = "user",
                    Type = "managed"
                }
            };
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(m_Root))
                {
                    Directory.Delete(m_Root, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup; failed deletion should not mask test results.
            }
        }
    }
}
