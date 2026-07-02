using System.Text.Json;
using ArisenKernel.Lifecycle;
using ArisenKernel.Packages;
using ArisenKernel.Services;
using Xunit;

namespace ArisenKernel.Tests;

public sealed class PackageRuntimeSmokeTests : IDisposable
{
    public PackageRuntimeSmokeTests()
    {
        TestPackageEvents.Reset();
        EngineKernel.Instance.Reset();
    }

    [Fact]
    public void EngineKernelLoadsPackageEntryAndRegistersDeclaredService()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });

        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { packagePath }
        });

        var packageSubsystem = EngineKernel.Instance.GetSubsystem<PackageSubsystem>();

        Assert.NotNull(packageSubsystem);
        Assert.Contains("load:provider", TestPackageEvents.Events);
        Assert.True(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out var service));
        Assert.Equal("provider", service.Name);
        Assert.Contains(packageSubsystem.GetAllPackages(), package => package.Id == "com.test.runtime.provider");
        Assert.Contains(EngineKernel.Instance.Services.GetRegisteredServices(), serviceInfo =>
            serviceInfo.ContractName == typeof(IRuntimeSmokeService).FullName
            && serviceInfo.ProviderPackageId == "com.test.runtime.provider");
    }

    [Fact]
    public void EngineKernelShutdownUnloadsPackageEntriesInReverseMountOrder()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        string consumerPath = workspace.AddPackage(
            "com.test.runtime.consumer",
            typeof(ConsumerPackageEntry),
            dependencies: new Dictionary<string, string>
            {
                ["com.test.runtime.provider"] = "1.0.0"
            },
            services: new
            {
                requires = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });

        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { providerPath, consumerPath }
        });
        EngineKernel.Instance.Shutdown();

        Assert.Equal(
            new[] { "load:provider", "load:consumer", "unload:consumer", "unload:provider" },
            TestPackageEvents.Events);
    }

    [Fact]
    public void EngineKernelFailsWhenNativeLifecycleLibraryIsMissing()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.native",
            typeof(ProviderPackageEntry),
            nativeRuntimes: new Dictionary<string, object[]>
            {
                ["win-x64"] = new object[]
                {
                    new
                    {
                        path = "Missing.Native.dll",
                        initExport = "ArisenNativeInit"
                    }
                }
            });

        var exception = Assert.Throws<FileNotFoundException>(() =>
            EngineKernel.Instance.Initialize(new EngineConfig
            {
                PackageUrls = new List<string> { packagePath }
            }));

        Assert.Contains("native lifecycle hooks", exception.Message);
        Assert.Contains("Missing.Native.dll", exception.Message);
    }

    [Fact]
    public void EngineKernelIgnoresMissingNativeRuntimeWithoutLifecycleHooks()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.native-passive",
            typeof(ProviderPackageEntry),
            nativeRuntimes: new Dictionary<string, object[]>
            {
                ["win-x64"] = new object[] { "Missing.Passive.dll" }
            });

        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { packagePath }
        });

        Assert.Contains("load:provider", TestPackageEvents.Events);
    }

    public void Dispose()
    {
        EngineKernel.Instance.Reset();
        TestPackageEvents.Reset();
    }

    private sealed class RuntimePackageWorkspace : IDisposable
    {
        private static readonly JsonSerializerOptions s_JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly string m_Root;

        private RuntimePackageWorkspace(string root)
        {
            m_Root = root;
            Directory.CreateDirectory(m_Root);
        }

        public static RuntimePackageWorkspace Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "ArisenKernel.Tests", Guid.NewGuid().ToString("N"));
            return new RuntimePackageWorkspace(root);
        }

        public string AddPackage(
            string id,
            Type entryType,
            Dictionary<string, string>? dependencies = null,
            object? services = null,
            Dictionary<string, object[]>? nativeRuntimes = null)
        {
            string packageDir = Path.Combine(m_Root, id);
            Directory.CreateDirectory(packageDir);

            var manifest = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["name"] = id,
                ["version"] = "1.0.0",
                ["type"] = "managed",
                ["entry"] = new
                {
                    assembly = $"{typeof(PackageRuntimeSmokeTests).Assembly.GetName().Name}.dll",
                    @class = entryType.FullName
                }
            };

            if (dependencies is { Count: > 0 }) manifest["dependencies"] = dependencies;
            if (services != null) manifest["services"] = services;
            if (nativeRuntimes is { Count: > 0 }) manifest["nativeRuntimes"] = nativeRuntimes;

            File.WriteAllText(Path.Combine(packageDir, "package.json"), JsonSerializer.Serialize(manifest, s_JsonOptions));
            return packageDir;
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

public interface IRuntimeSmokeService
{
    string Name { get; }
}

public sealed class RuntimeSmokeService : IRuntimeSmokeService
{
    public string Name => "provider";
}

public sealed class ProviderPackageEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("load:provider");
        services.RegisterService<IRuntimeSmokeService>(new RuntimeSmokeService());
    }

    public void OnUnload(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("unload:provider");
    }
}

public sealed class ConsumerPackageEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        _ = services.GetService<IRuntimeSmokeService>();
        TestPackageEvents.Events.Add("load:consumer");
    }

    public void OnUnload(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("unload:consumer");
    }
}

internal static class TestPackageEvents
{
    public static List<string> Events { get; } = new();

    public static void Reset()
    {
        Events.Clear();
    }
}
