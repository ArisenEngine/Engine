using System.Text.Json;
using ArisenBuildTool.Models;
using ArisenBuildTool.Services;
using ArisenBuildTool.Utils;
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
    public void WorkspaceManifestParserAllowsCommentsAndTrailingCommas()
    {
        var manifest = ManifestJson.Deserialize<ProjectManifest>(
            """
            {
              // Human-authored workspace manifest comments are allowed.
              "Name": "JsoncFixture",
              "EngineVersion": "Current",
              "StartupScene": {
                "Guid": "11111111-2222-3333-4444-555555555555",
                "PackageId": "com.test.app",
              },
              "Packages": [
                {
                  "Id": "com.test.app",
                  "Url": "file://Local/com.test.app",
                  "Version": "1.0.0",
                },
              ],
              "Profiles": {
                "Development": {
                  "Packages": [],
                },
              },
            }
            """);

        Assert.NotNull(manifest);
        Assert.Equal("JsoncFixture", manifest!.Name);
        Assert.Equal(Guid.Parse("11111111-2222-3333-4444-555555555555"), manifest.StartupScene!.Guid);
        Assert.Equal("com.test.app", manifest.StartupScene.PackageId);
        Assert.Equal("com.test.app", manifest.Packages.Single().Id);
    }

    [Fact]
    public void WorkspaceManifestParserRejectsFullJson5UnquotedKeys()
    {
        Assert.Throws<JsonException>(() => ManifestJson.Deserialize<ProjectManifest>(
            """
            {
              Name: "Json5Fixture",
              EngineVersion: "Current",
              Packages: [],
            }
            """));
    }

    [Fact]
    public void PackageManifestAllowsCommentsAndTrailingCommas()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddRawPackageJson(
            "com.test.app",
            """
            {
              // Human-authored package manifest comments are allowed.
              "id": "com.test.app",
              "name": "Commented Package",
              "version": "1.0.0",
              "layer": "user",
              "dependencies": {
              },
            }
            """);

        var result = workspace.Validate("com.test.app");

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal("Commented Package", result.PackageMap["com.test.app"].Manifest.Name);
    }

    [Fact]
    public void PackageManifestRejectsFullJson5UnquotedKeys()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddRawPackageJson(
            "com.test.app",
            """
            {
              id: "com.test.app",
              name: "Json5 Package",
              version: "1.0.0",
              layer: "user",
            }
            """);

        var result = workspace.Validate("com.test.app");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("Failed to parse package", StringComparison.OrdinalIgnoreCase));
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
    public void MissingWorkspaceRequiredFieldsFailValidation()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage("com.test.app", layer: "user");

        var manifest = workspace.CreateManifest(new PackageRequirement
        {
            Id = "com.test.app",
            Url = "file://Local/com.test.app",
            Version = "1.0.0"
        });
        manifest.Name = string.Empty;
        manifest.EngineVersion = string.Empty;

        var result = workspace.Validate(manifest);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("missing required Name", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("missing required EngineVersion", StringComparison.Ordinal));
    }

    [Fact]
    public void IncompleteWorkspaceStartupSceneFailsValidation()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage("com.test.app", layer: "user");

        var manifest = workspace.CreateManifest(new PackageRequirement
        {
            Id = "com.test.app",
            Url = "file://Local/com.test.app",
            Version = "1.0.0"
        });
        manifest.StartupScene = new ProjectAssetReference();

        var result = workspace.Validate(manifest);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("StartupScene", StringComparison.Ordinal)
            && error.Contains("Guid", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("StartupScene", StringComparison.Ordinal)
            && error.Contains("PackageId", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingWorkspacePackageVersionFailsValidation()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage("com.test.app", layer: "user");

        var result = workspace.Validate(workspace.CreateManifest(new PackageRequirement
        {
            Id = "com.test.app",
            Url = "file://Local/com.test.app"
        }));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("missing required Version", StringComparison.Ordinal)
            && error.Contains("com.test.app", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingPackageRequiredFieldsFailValidation()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddRawPackage("com.test.app", new Dictionary<string, object?>
        {
            ["id"] = "com.test.app",
            ["layer"] = "user"
        });

        var result = workspace.Validate("com.test.app");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("missing required name", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("missing required version", StringComparison.Ordinal));
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
    public void RequiredServiceCapabilityIsSatisfiedByProviderCapability()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage(
            "com.test.rhi.vulkan",
            layer: "driver",
            services: new
            {
                provides = new object[]
                {
                    new
                    {
                        @interface = "Com.Test.Contracts.IRHIBackend",
                        capabilities = new[] { "vulkan" }
                    }
                }
            });
        workspace.AddPackage(
            "com.test.app",
            layer: "user",
            dependencies: new Dictionary<string, string>
            {
                ["com.test.rhi.vulkan"] = "1.0.0"
            },
            services: new
            {
                requires = new object[]
                {
                    new
                    {
                        @interface = "Com.Test.Contracts.IRHIBackend",
                        capabilities = new[] { "vulkan" }
                    }
                }
            });

        var result = workspace.Validate("com.test.app");

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void RequiredServiceCapabilityMismatchFailsValidation()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage(
            "com.test.rhi.dx12",
            layer: "driver",
            services: new
            {
                provides = new object[]
                {
                    new
                    {
                        @interface = "Com.Test.Contracts.IRHIBackend",
                        capabilities = new[] { "dx12" }
                    }
                }
            });
        workspace.AddPackage(
            "com.test.app",
            layer: "user",
            dependencies: new Dictionary<string, string>
            {
                ["com.test.rhi.dx12"] = "1.0.0"
            },
            services: new
            {
                requires = new object[]
                {
                    new
                    {
                        @interface = "Com.Test.Contracts.IRHIBackend",
                        capabilities = new[] { "vulkan" }
                    }
                }
            });

        var result = workspace.Validate("com.test.app");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("capabilities [vulkan]", StringComparison.OrdinalIgnoreCase)
            && error.Contains("com.test.rhi.dx12", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UnknownKernelServiceContractFailsValidation()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage(
            "com.test.app",
            layer: "user",
            services: new
            {
                requires = new object[] { "ArisenKernel.Contracts.IMissingKernelContract" }
            });

        var result = workspace.Validate("com.test.app");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("unknown kernel service contract", StringComparison.OrdinalIgnoreCase)
            && error.Contains("ArisenKernel.Contracts.IMissingKernelContract", StringComparison.Ordinal));
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
    public void RemoteRequirementUsesCacheWhenLocalPackageWithSameIdExists()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage("com.test.remote", layer: "user", description: "local copy");
        workspace.AddCachedPackage("com.test.remote", layer: "user", description: "cached remote copy");

        var manifest = workspace.CreateManifest(new PackageRequirement
        {
            Id = "com.test.remote",
            Url = "https://packages.example.test/registry.json",
            Version = "1.0.0"
        });

        var result = workspace.Validate(manifest);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        Assert.Contains(Path.Combine(".Cache", "com.test.remote"), result.PackageMap["com.test.remote"].DirectoryPath);
        Assert.Equal("cached remote copy", result.PackageMap["com.test.remote"].Manifest.Description);
        Assert.Contains(result.Warnings, warning => warning.Contains("local folder", StringComparison.OrdinalIgnoreCase)
            && warning.Contains("ignored", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FileUrlRequirementUsesLocalOverrideWhenCachePackageWithSameIdExists()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage("com.test.override", layer: "user", description: "local override");
        workspace.AddCachedPackage("com.test.override", layer: "user", description: "cached remote copy");

        var manifest = workspace.CreateManifest(new PackageRequirement
        {
            Id = "com.test.override",
            Url = "file://Local/com.test.override",
            Version = "1.0.0"
        });

        var result = workspace.Validate(manifest);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        Assert.Contains(Path.Combine("Local", "com.test.override"), result.PackageMap["com.test.override"].DirectoryPath);
        Assert.Equal("local override", result.PackageMap["com.test.override"].Manifest.Description);
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

    [Fact]
    public void NativeRuntimeLifecycleExportMustBeNonEmptyString()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage(
            "com.test.native",
            layer: "driver",
            type: "native",
            nativeRuntimes: new Dictionary<string, object[]>
            {
                ["win-x64"] = new object[]
                {
                    new { path = "Native.Runtime.dll", initExport = "" },
                    new { path = "Native.Runtime.dll", shutdownExport = 42 }
                }
            });

        var result = workspace.Validate("com.test.native");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("empty initExport", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("invalid 'shutdownExport'", StringComparison.Ordinal));
    }

    [Fact]
    public void NativeOutputValidationFailsWhenResolvedPayloadIsMissing()
    {
        using var workspace = ValidationWorkspace.Create();
        string outputDir = Path.Combine(workspace.RootPath, "bin");
        Directory.CreateDirectory(outputDir);
        string resolvedManifestPath = Path.Combine(outputDir, "manifest.resolved.json");
        File.WriteAllText(
            resolvedManifestPath,
            """
            {
              "ResolvedPackages": [
                {
                  "Id": "com.test.native",
                  "NativeRuntimes": {
                    "win-x64": [
                      "Native.Runtime.dll"
                    ]
                  }
                }
              ]
            }
            """);

        var result = NativeOutputValidationService.Validate(resolvedManifestPath, outputDir);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("Native.Runtime.dll", StringComparison.Ordinal)
            && error.Contains("deployed native runtime", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NativeOutputValidationFailsWhenDeclaredExportIsMissing()
    {
        using var workspace = ValidationWorkspace.Create();
        string outputDir = Path.Combine(workspace.RootPath, "bin");
        Directory.CreateDirectory(outputDir);
        string resolvedManifestPath = Path.Combine(outputDir, "manifest.resolved.json");
        string deployedDllPath = Path.Combine(outputDir, "Native.Runtime.dll");
        File.Copy(typeof(PackageValidationFixtureTests).Assembly.Location, deployedDllPath);
        File.WriteAllText(
            resolvedManifestPath,
            """
            {
              "ResolvedPackages": [
                {
                  "Id": "com.test.native",
                  "NativeRuntimes": {
                    "win-x64": [
                      {
                        "path": "Native.Runtime.dll",
                        "exports": [
                          "MissingNativeExport"
                        ]
                      }
                    ]
                  }
                }
              ]
            }
            """);

        var result = NativeOutputValidationService.Validate(resolvedManifestPath, outputDir);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("MissingNativeExport", StringComparison.Ordinal)
            && error.Contains("missing declared export", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NativeOutputValidationFiltersConfigurationSpecificPayloads()
    {
        using var workspace = ValidationWorkspace.Create();
        string outputDir = Path.Combine(workspace.RootPath, "bin");
        Directory.CreateDirectory(outputDir);
        string resolvedManifestPath = Path.Combine(outputDir, "manifest.resolved.json");
        File.WriteAllText(Path.Combine(outputDir, "Native.Debug.dll"), string.Empty);
        File.WriteAllText(
            resolvedManifestPath,
            """
            {
              "ResolvedPackages": [
                {
                  "Id": "com.test.native",
                  "NativeRuntimes": {
                    "win-x64": [
                      {
                        "path": "Native.Debug.dll",
                        "configurations": [
                          "Debug"
                        ]
                      },
                      {
                        "path": "Native.Release.dll",
                        "configurations": [
                          "Release"
                        ]
                      }
                    ]
                  }
                }
              ]
            }
            """);

        var result = NativeOutputValidationService.Validate(resolvedManifestPath, outputDir, "Debug");

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void EditorSharedTextureViewportGraphRequiresRenderingPlatformAndRhiBackend()
    {
        using var workspace = ValidationWorkspace.Create();
        workspace.AddPackage(
            "com.test.core",
            layer: "foundation",
            services: new
            {
                provides = new object[]
                {
                    "ArisenEngine.Core.Assets.IAssetDatabase"
                }
            });
        workspace.AddPackage("com.test.ecs", layer: "domain", dependencies: new Dictionary<string, string>
        {
            ["com.test.core"] = "1.0.0"
        });
        workspace.AddPackage("com.test.platform.desktop", layer: "foundation", services: new
        {
            provides = new object[]
            {
                "ArisenKernel.Contracts.IWindowProvider"
            }
        });
        workspace.AddPackage("com.test.rhi.vulkan.native", layer: "driver", services: new
        {
            provides = new object[]
            {
                new
                {
                    @interface = "ArisenKernel.Contracts.IRHIBackend",
                    capabilities = new[] { "vulkan" }
                },
                new
                {
                    @interface = "ArisenKernel.Contracts.IRHIDevice",
                    capabilities = new[] { "vulkan" },
                    deferred = true
                }
            }
        });
        workspace.AddPackage(
            "com.test.rendering",
            layer: "domain",
            dependencies: new Dictionary<string, string>
            {
                ["com.test.core"] = "1.0.0",
                ["com.test.ecs"] = "1.0.0"
            },
            services: new
            {
                requires = new object[]
                {
                    "ArisenKernel.Contracts.IWindowProvider",
                    "ArisenKernel.Contracts.IRHIBackend",
                    new
                    {
                        @interface = "ArisenKernel.Contracts.IRHIDevice",
                        deferred = true
                    }
                }
            });
        workspace.AddPackage(
            "com.test.editor",
            layer: "tooling",
            dependencies: new Dictionary<string, string>
            {
                ["com.test.core"] = "1.0.0",
                ["com.test.ecs"] = "1.0.0",
                ["com.test.rendering"] = "1.0.0",
                ["com.test.platform.desktop"] = "1.0.0"
            },
            services: new
            {
                provides = new object[]
                {
                    "ArisenKernel.Contracts.IApplicationHost"
                },
                requires = new object[]
                {
                    new
                    {
                        @interface = "ArisenKernel.Contracts.IRHIBackend",
                        capabilities = new[] { "vulkan" }
                    }
                }
            });

        var result = workspace.Validate("com.test.editor", "com.test.rhi.vulkan.native");

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        Assert.Contains(result.SortedPackages, package => package.Manifest.Id == "com.test.rendering");
        Assert.Contains(result.SortedPackages, package => package.Manifest.Id == "com.test.platform.desktop");
        Assert.Contains(result.SortedPackages, package => package.Manifest.Id == "com.test.rhi.vulkan.native");
    }

    [Fact]
    public void AssetReferenceGeneratorEmitsMaterialSlotConstants()
    {
        using var workspace = ValidationWorkspace.Create();
        string packageDir = workspace.AddPackage(
            "com.test.renderpipeline",
            layer: "domain",
            dependencies: new Dictionary<string, string>
            {
                ["com.arisen.core"] = "1.0.0"
            },
            entryClass: "Com.Test.RenderPipelinePackage");
        string materialDir = Path.Combine(packageDir, "Assets", "Materials");
        string meshDir = Path.Combine(packageDir, "Assets", "Meshes");
        string modelDir = Path.Combine(packageDir, "Assets", "Models");
        string shaderDir = Path.Combine(packageDir, "Assets", "Shaders");
        string textureDir = Path.Combine(packageDir, "Assets", "Textures");
        string environmentDir = Path.Combine(packageDir, "Assets", "Environments");
        string sceneDir = Path.Combine(packageDir, "Assets", "Scenes");
        Directory.CreateDirectory(materialDir);
        Directory.CreateDirectory(meshDir);
        Directory.CreateDirectory(modelDir);
        Directory.CreateDirectory(shaderDir);
        Directory.CreateDirectory(textureDir);
        Directory.CreateDirectory(environmentDir);
        Directory.CreateDirectory(sceneDir);
        File.WriteAllText(
            Path.Combine(materialDir, "SmokeMaterial.arismaterial.meta"),
            """
            Guid: 11111111-2222-3333-4444-555555555555
            AssetType: Material
            Importer: ArisenMaterialImporter
            """);
        File.WriteAllText(
            Path.Combine(materialDir, "SmokeMaterial.arismaterial.meta.meta"),
            """
            Guid: aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
            AssetType: meta
            Importer: metaImporter
            """);
        File.WriteAllText(
            Path.Combine(materialDir, "SmokeMaterial.arismaterial"),
            """
            Name: Test/Smoke
            Shader:
              Guid: 33333333-4444-5555-6666-777777777777
            Texture2DRefs:
            - Name: BaseColor
              Slot: 0
            ScalarProperties:
            - Name: MetallicFactor
              Value: 0
            - Name: RoughnessFactor
              Value: 1
            Vector4Properties:
            - Name: BaseColorFactor
              Value:
                X: 1
                Y: 1
                Z: 1
                W: 1
            """);
        File.WriteAllText(
            Path.Combine(meshDir, "TexturedQuad.obj.meta"),
            """
            Guid: 22222222-3333-4444-5555-666666666666
            AssetType: Mesh
            Importer: ObjMeshImporter
            """);
        File.WriteAllText(Path.Combine(meshDir, "TexturedQuad.obj"), string.Empty);
        File.WriteAllText(
            Path.Combine(modelDir, "HeroModel.arismodel.meta"),
            """
            Guid: 77777777-8888-9999-aaaa-bbbbbbbbbbbb
            AssetType: Model
            Importer: ArisenModelImporter
            """);
        File.WriteAllText(Path.Combine(modelDir, "HeroModel.arismodel"), string.Empty);
        File.WriteAllText(
            Path.Combine(meshDir, "TexturedQuad.bin.meta"),
            """
            Guid: 55555555-6666-7777-8888-999999999999
            AssetType: AssetDependency
            Importer: GltfBufferDependency
            """);
        File.WriteAllText(Path.Combine(meshDir, "TexturedQuad.bin"), string.Empty);
        File.WriteAllText(
            Path.Combine(shaderDir, "SmokeTriangle.hlsl.meta"),
            """
            Guid: 33333333-4444-5555-6666-777777777777
            AssetType: ShaderSource
            Importer: HlslShaderImporter
            """);
        File.WriteAllText(
            Path.Combine(shaderDir, "SmokeTriangle.hlsl"),
            """
            // @arisen.material.texture2d NormalMap
            // @arisen.material.scalar AlphaCutoff
            // @arisen.material.vector4 EmissiveFactor
            """);
        File.WriteAllText(
            Path.Combine(textureDir, "SmokeChecker.ppm.meta"),
            """
            Guid: 44444444-5555-6666-7777-888888888888
            AssetType: Texture2D
            Importer: PpmTextureImporter
            """);
        File.WriteAllText(Path.Combine(textureDir, "SmokeChecker.ppm"), string.Empty);
        File.WriteAllText(
            Path.Combine(environmentDir, "Studio.arienvironment.meta"),
            """
            Guid: 88888888-9999-aaaa-bbbb-cccccccccccc
            AssetType: EnvironmentTexture
            Importer: ArisenEnvironmentTextureImporter
            """);
        File.WriteAllText(Path.Combine(environmentDir, "Studio.arienvironment"), string.Empty);
        File.WriteAllText(
            Path.Combine(sceneDir, "SmokeScene.arisenscene.meta"),
            """
            Guid: 66666666-7777-8888-9999-aaaaaaaaaaaa
            AssetType: Scene
            Importer: ArisenSceneImporter
            """);
        File.WriteAllText(Path.Combine(sceneDir, "SmokeScene.arisenscene"), string.Empty);

        string projectDir = Path.Combine(workspace.RootPath, ".arisen", "Projects", "Development", "Com.Test.Renderpipeline");
        AssetReferenceGeneratorService.Generate(
            projectDir,
            "Com.Test.Renderpipeline",
            new PackageInfo
            {
                DirectoryPath = packageDir,
                Manifest = ManifestJson.Deserialize<PackageManifest>(File.ReadAllText(Path.Combine(packageDir, "package.json")))!
            });

        string generated = File.ReadAllText(Path.Combine(projectDir, "Generated", "RenderPipelineAssetRefs.g.cs"));
        Assert.Contains("public static readonly Guid SmokeMaterialGuid", generated);
        Assert.Contains("using ArisenEngine.Core.Assets;", generated);
        Assert.Contains("public static readonly AssetRef<MaterialSourceAsset> SmokeMaterialRef", generated);
        Assert.Contains("public static readonly AssetRef<MeshSourceAsset> TexturedQuadMeshRef", generated);
        Assert.Contains("public static readonly AssetRef<ModelSourceAsset> HeroModelRef", generated);
        Assert.Contains("public static readonly AssetRef<ShaderSourceAsset> SmokeTriangleShaderRef", generated);
        Assert.Contains("public static readonly AssetRef<Texture2DSourceAsset> SmokeCheckerTextureRef", generated);
        Assert.Contains("public static readonly AssetRef<EnvironmentTextureSourceAsset> StudioEnvironmentTextureRef", generated);
        Assert.Contains("public static readonly AssetRef<SceneSourceAsset> SmokeSceneRef", generated);
        Assert.DoesNotContain("SmokeMaterial_arismaterialMetaGuid", generated);
        Assert.DoesNotContain("TexturedQuadAssetDependencyGuid", generated);
        Assert.DoesNotContain("55555555-6666-7777-8888-999999999999", generated);
        Assert.Contains("public static class SmokeMaterial", generated);
        Assert.Contains("public static readonly AssetRef<MaterialSourceAsset> Ref = SmokeMaterialRef;", generated);
        Assert.Contains("public static class TexturedQuadMesh", generated);
        Assert.Contains("public static readonly AssetRef<MeshSourceAsset> Ref = TexturedQuadMeshRef;", generated);
        Assert.Contains("public static class HeroModel", generated);
        Assert.Contains("public static readonly AssetRef<ModelSourceAsset> Ref = HeroModelRef;", generated);
        Assert.Contains("public static class SmokeScene", generated);
        Assert.Contains("public static readonly AssetRef<SceneSourceAsset> Ref = SmokeSceneRef;", generated);
        Assert.Contains("public static class StudioEnvironmentTexture", generated);
        Assert.Contains("public static readonly AssetRef<EnvironmentTextureSourceAsset> Ref = StudioEnvironmentTextureRef;", generated);
        Assert.Contains("public const string BaseColor = \"BaseColor\";", generated);
        Assert.Contains("public const string MetallicFactor = \"MetallicFactor\";", generated);
        Assert.Contains("public const string RoughnessFactor = \"RoughnessFactor\";", generated);
        Assert.Contains("public const string BaseColorFactor = \"BaseColorFactor\";", generated);
        Assert.Contains("public const string NormalMap = \"NormalMap\";", generated);
        Assert.Contains("public const string AlphaCutoff = \"AlphaCutoff\";", generated);
        Assert.Contains("public const string EmissiveFactor = \"EmissiveFactor\";", generated);
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
            Directory.CreateDirectory(KernelContractsPath);
            File.WriteAllText(Path.Combine(KernelContractsPath, "IApplicationHost.cs"), "namespace ArisenKernel.Contracts; public interface IApplicationHost { }");
            File.WriteAllText(Path.Combine(KernelContractsPath, "IRHIBackend.cs"), "namespace ArisenKernel.Contracts; public interface IRHIBackend { }");
            File.WriteAllText(Path.Combine(KernelContractsPath, "IRHIDevice.cs"), "namespace ArisenKernel.Contracts; public interface IRHIDevice { }");
            File.WriteAllText(Path.Combine(KernelContractsPath, "IWindowProvider.cs"), "namespace ArisenKernel.Contracts; public interface IWindowProvider { }");
        }

        private string LocalPath => Path.Combine(m_Root, "Local");

        public string RootPath => m_Root;

        private string KernelContractsPath => Path.Combine(m_Root, "ArisenKernel", "Contracts");

        public static ValidationWorkspace Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "ArisenBuildTool.Tests", Guid.NewGuid().ToString("N"));
            return new ValidationWorkspace(root);
        }

        public string AddPackage(
            string id,
            string layer,
            string? type = null,
            string? description = null,
            Dictionary<string, string>? dependencies = null,
            object? services = null,
            Dictionary<string, object[]>? nativeRuntimes = null,
            Dictionary<string, object[]>? nativeTests = null,
            string? entryClass = null)
        {
            string packageDir = Path.Combine(LocalPath, id);
            WritePackage(packageDir, id, layer, type, description, dependencies, services, nativeRuntimes, nativeTests, entryClass);
            return packageDir;
        }

        public void AddCachedPackage(string id, string layer, string? type = null, string? description = null)
        {
            WritePackage(Path.Combine(m_Root, ".Cache", id), id, layer, type, description);
        }

        public void AddRawPackage(string id, Dictionary<string, object?> manifest)
        {
            string packageDir = Path.Combine(LocalPath, id);
            Directory.CreateDirectory(packageDir);
            File.WriteAllText(Path.Combine(packageDir, "package.json"), JsonSerializer.Serialize(manifest, s_JsonOptions));
        }

        public void AddRawPackageJson(string id, string json)
        {
            string packageDir = Path.Combine(LocalPath, id);
            Directory.CreateDirectory(packageDir);
            File.WriteAllText(Path.Combine(packageDir, "package.json"), json);
        }

        private static void WritePackage(
            string packageDir,
            string id,
            string layer,
            string? type = null,
            string? description = null,
            Dictionary<string, string>? dependencies = null,
            object? services = null,
            Dictionary<string, object[]>? nativeRuntimes = null,
            Dictionary<string, object[]>? nativeTests = null,
            string? entryClass = null)
        {
            Directory.CreateDirectory(packageDir);

            var manifest = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["name"] = id,
                ["version"] = "1.0.0",
                ["layer"] = layer
            };

            if (!string.IsNullOrWhiteSpace(type)) manifest["type"] = type;
            if (!string.IsNullOrWhiteSpace(description)) manifest["description"] = description;
            if (!string.IsNullOrWhiteSpace(entryClass)) manifest["entry"] = new { assembly = "Com.Test.dll", @class = entryClass };
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
                EngineVersion = "Current",
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
                .Select(id => new PackageRequirement { Id = id, Url = $"file://Local/{id}", Version = "1.0.0" })
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
