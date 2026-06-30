using System.Text.Json;
using ArisenBuildTool.Models;
using ArisenBuildTool.Services;
using Xunit;

namespace ArisenBuildTool.Tests;

public sealed class PackageValidationFixtureTests
{
    [Fact]
    public void ValidMinimalWorkspaceSortsDependenciesBeforeConsumers()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage("com.test.foundation", layer: "foundation");
        workspace.AddPackage("com.test.app", layer: "user", dependencies: new Dictionary<string, string>
        {
            ["com.test.foundation"] = "1.0.0"
        });

        var result = workspace.Validate("com.test.app");

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(new[] { "com.test.foundation", "com.test.app" }, result.SortedPackages.Select(package => package.Manifest.Id));
    }

    [Fact]
    public void MissingDependencyFailsValidation()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage("com.test.app", layer: "user", dependencies: new Dictionary<string, string>
        {
            ["com.test.missing"] = "1.0.0"
        });

        var result = workspace.Validate("com.test.app");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("com.test.missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DependencyCycleFailsValidation()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage("com.test.a", layer: "domain", dependencies: new Dictionary<string, string>
        {
            ["com.test.b"] = "1.0.0"
        });
        workspace.AddPackage("com.test.b", layer: "domain", dependencies: new Dictionary<string, string>
        {
            ["com.test.a"] = "1.0.0"
        });

        var result = workspace.Validate("com.test.a");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingRequiredServiceFailsValidation()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage(
            "com.test.consumer",
            layer: "domain",
            services: new
            {
                requires = new object[] { "Com.Test.Contracts.IMissingService" }
            });

        var result = workspace.Validate("com.test.consumer");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("Com.Test.Contracts.IMissingService", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicatePackageIdWithConflictingMetadataFailsValidation()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage("com.test.app", layer: "user");

        var manifest = workspace.CreateManifest(
            new PackageRequirement { Id = "com.test.app", Url = "file://Local/com.test.app", Version = "1.0.0" },
            new PackageRequirement { Id = "com.test.app", Url = "file://Local/com.test.app", Version = "2.0.0" });

        var result = workspace.Validate(manifest);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("listed multiple times", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NativeTestsOutsideTestLayerFailValidation()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage(
            "com.test.native",
            layer: "driver",
            type: "native",
            nativeRuntimes: new Dictionary<string, object[]>
            {
                ["win-x64"] = new object[] { "Native.Tests.dll" }
            },
            nativeTests: new Dictionary<string, object[]>
            {
                ["win-x64"] = new object[] { new { library = "Native.Tests.dll", registerExport = "RegisterNativeTests" } }
            });

        var result = workspace.Validate("com.test.native");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("Native tests are only valid in test packages", StringComparison.Ordinal));
    }

    [Fact]
    public void NativeTestsLibraryMissingFromNativeRuntimesFailsValidation()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage(
            "com.test.native.test",
            layer: "test",
            type: "native",
            nativeRuntimes: new Dictionary<string, object[]>
            {
                ["win-x64"] = new object[] { "Other.Tests.dll" }
            },
            nativeTests: new Dictionary<string, object[]>
            {
                ["win-x64"] = new object[] { new { library = "Native.Tests.dll", registerExport = "RegisterNativeTests" } }
            });

        var result = workspace.Validate("com.test.native.test");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("must also be declared in nativeRuntimes", StringComparison.Ordinal));
    }

    private sealed class ValidationWorkspace : IDisposable
    {
        private static readonly JsonSerializerOptions s_JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly string m_Root;

        private ValidationWorkspace(string root)
        {
            m_Root = root;
            Directory.CreateDirectory(LocalPath);
        }

        private string LocalPath => Path.Combine(m_Root, "Local");

        public static ValidationWorkspace Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "ArisenBuildTool.Tests", Guid.NewGuid().ToString("N"));
            return new ValidationWorkspace(root);
        }

        public void AddPackage(
            string id,
            string layer,
            string? type = null,
            Dictionary<string, string>? dependencies = null,
            object? services = null,
            Dictionary<string, object[]>? nativeRuntimes = null,
            Dictionary<string, object[]>? nativeTests = null)
        {
            string packageDir = Path.Combine(LocalPath, id);
            Directory.CreateDirectory(packageDir);

            var manifest = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["name"] = id,
                ["version"] = "1.0.0",
                ["layer"] = layer
            };

            if (!string.IsNullOrWhiteSpace(type)) manifest["type"] = type;
            if (dependencies is { Count: > 0 }) manifest["dependencies"] = dependencies;
            if (services != null) manifest["services"] = services;
            if (nativeRuntimes is { Count: > 0 }) manifest["nativeRuntimes"] = nativeRuntimes;
            if (nativeTests is { Count: > 0 }) manifest["nativeTests"] = nativeTests;

            File.WriteAllText(Path.Combine(packageDir, "package.json"), JsonSerializer.Serialize(manifest, s_JsonOptions));
        }

        public ProjectManifest CreateManifest(params PackageRequirement[] packages)
        {
            return new ProjectManifest
            {
                Name = "ValidationFixture",
                Packages = packages.ToList(),
                Profiles = new Dictionary<string, ProfileDefinition>
                {
                    ["Development"] = new()
                }
            };
        }

        public PackageValidationResult Validate(params string[] packageIds)
        {
            var requirements = packageIds
                .Select(id => new PackageRequirement { Id = id, Url = $"file://Local/{id}" })
                .ToArray();

            return Validate(CreateManifest(requirements));
        }

        public PackageValidationResult Validate(ProjectManifest manifest)
        {
            return PackageValidationService.Validate(manifest, m_Root, m_Root, "Development");
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
