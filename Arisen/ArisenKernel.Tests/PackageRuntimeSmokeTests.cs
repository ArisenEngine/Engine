using System.Text.Json;
using System.Security.Cryptography;
using Arisen.Testing;
using ArisenKernel.Contracts;
using ArisenKernel.Diagnostics;
using ArisenKernel.Lifecycle;
using ArisenKernel.Packages;
using ArisenKernel.Services;
using Xunit;

namespace ArisenKernel.Tests;

public sealed class PackageRuntimeSmokeTests : IDisposable
{
    public PackageRuntimeSmokeTests()
    {
        LifecycleFaultInjection.Reset();
        TestPackageEvents.Reset();
        EngineKernel.Instance.Reset();
        KernelLog.InvalidateCache();
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
    public void EngineKernelLoadsPackageManifestWithCommentsAndTrailingCommas()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddRawPackageJson(
            "com.test.runtime.jsonc",
            $$"""
            {
              // Runtime package manifests accept comments.
              "id": "com.test.runtime.jsonc",
              "name": "Jsonc Runtime Package",
              "version": "1.0.0",
              "type": "managed",
              "entry": {
                "assembly": "{{typeof(PackageRuntimeSmokeTests).Assembly.GetName().Name}}.dll",
                "class": "{{typeof(ProviderPackageEntry).FullName}}",
              },
              "services": {
                "provides": [
                  "{{typeof(IRuntimeSmokeService).FullName}}",
                ],
              },
            }
            """);

        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { packagePath }
        });

        Assert.Contains("load:provider", TestPackageEvents.Events);
        Assert.True(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
    }

    [Fact]
    public void EngineKernelRunForFramesTicksAndShutsDownCleanly()
    {
        EngineKernel.Instance.RegisterSubsystem(new CountingTickSubsystem());

        int exitCode = EngineKernel.Instance.RunForFrames(3);

        Assert.Equal(0, exitCode);
        Assert.Equal(3u, EngineKernel.Instance.CurrentFrameIndex);
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.Equal(3, CountingTickSubsystem.TickCount);
        Assert.True(CountingTickSubsystem.WasShutdown);
    }

    [Fact]
    public void EngineKernelRunsStateDrivenSmokeThroughShutdown()
    {
        EngineKernel.Instance.RegisterSubsystem(new CountingTickSubsystem());
        var scenario = new CountingSmokeScenario(readyAfterFrames: 2);

        int exitCode = EngineKernel.Instance.RunSmokeScenario(
            scenario,
            maximumFrameCount: 8,
            maximumDuration: TimeSpan.FromSeconds(2));

        Assert.Equal(0, exitCode);
        Assert.Equal(2u, EngineKernel.Instance.CurrentFrameIndex);
        Assert.True(scenario.IsComplete);
        Assert.True(scenario.Succeeded);
        Assert.True(scenario.ShutdownObserved);
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
            PackageUrls = new List<string> { consumerPath, providerPath }
        });
        EngineKernel.Instance.Shutdown();

        Assert.Equal(
            new[] { "load:provider", "load:consumer", "unload:consumer", "unload:provider" },
            TestPackageEvents.Events);
    }

    [Fact]
    public void EngineKernelShutdownUnregistersPackageProvidedServices()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });

        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        });

        Assert.True(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));

        EngineKernel.Instance.Shutdown();

        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.DoesNotContain(EngineKernel.Instance.Services.GetRegisteredServices(), serviceInfo =>
            serviceInfo.ProviderPackageId == "com.test.runtime.provider");
    }

    [Fact]
    public void EngineKernelShutdownUnregistersKernelOwnedServicesWithoutPackageGraph()
    {
        EngineKernel.Instance.RegisterKernelOwnedService<IRuntimeSmokeService>(
            new RuntimeSmokeService());

        ServiceRegistrationInfo registration = Assert.Single(
            EngineKernel.Instance.Services.GetRegisteredServices());
        Assert.Equal(ServiceRegistry.KernelProviderId, registration.ProviderPackageId);

        EngineKernel.Instance.Shutdown();

        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        EngineShutdownOwnershipSnapshot ownership =
            EngineKernel.Instance.GetShutdownOwnershipSnapshot();
        Assert.True(ownership.IsClean, ownership.ToString());
    }

    [Fact]
    public void EngineKernelCanUnloadResetAndReloadSamePackageGraph()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });

        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        });
        Assert.True(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(
            out var firstService));

        EngineKernel.Instance.Shutdown();

        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));

        EngineKernel.Instance.Reset();
        Assert.Equal(EnginePhase.None, EngineKernel.Instance.CurrentPhase);
        Assert.Empty(EngineKernel.Instance.Services.GetRegisteredServices());

        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        });
        Assert.True(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(
            out var secondService));
        Assert.NotSame(firstService, secondService);

        PackageSubsystem packageSubsystem =
            Assert.IsType<PackageSubsystem>(EngineKernel.Instance.GetSubsystem<PackageSubsystem>());
        Assert.Single(packageSubsystem.GetAllPackages());

        EngineKernel.Instance.Shutdown();

        Assert.Empty(packageSubsystem.GetAllPackages());
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.Equal(
            new[]
            {
                "load:provider",
                "unload:provider",
                "load:provider",
                "unload:provider"
            },
            TestPackageEvents.Events);
    }

    [Fact]
    public void EngineKernel_BoundedBootAndPackageOnlyMountStressReturnsToBaseline()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        var config = new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        };
        const int cycleCount = 16;

        for (int cycle = 0; cycle < cycleCount; cycle++)
        {
            bool packageOnly = (cycle & 1) == 0;
            if (packageOnly)
            {
                EngineKernel.Instance.MountPackageGraph(config);
            }
            else
            {
                EngineKernel.Instance.Initialize(config);
            }

            PackageSubsystem packageSubsystem = Assert.IsType<PackageSubsystem>(
                EngineKernel.Instance.GetSubsystem<PackageSubsystem>());
            Assert.True(EngineKernel.Instance.IsPackageGraphMounted);
            Assert.Equal(
                packageOnly ? EnginePhase.None : EnginePhase.Running,
                EngineKernel.Instance.CurrentPhase);
            Assert.Single(packageSubsystem.GetAllPackages());
            Assert.True(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));

            EngineKernel.Instance.Shutdown();

            Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
            Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
            Assert.Empty(packageSubsystem.GetAllPackages());
            Assert.Equal(0, packageSubsystem.LoadedContextCount);
            Assert.Empty(EngineKernel.Instance.GetInitializedSubsystemDiagnostics());
            Assert.Empty(EngineKernel.Instance.Services.GetRegisteredServices());
            EngineShutdownOwnershipSnapshot ownership =
                EngineKernel.Instance.GetShutdownOwnershipSnapshot();
            Assert.True(ownership.IsClean, ownership.ToString());
            Assert.Equal(0, ownership.NativeRuntimeCount);

            EngineKernel.Instance.Reset();
            Assert.Equal(EnginePhase.None, EngineKernel.Instance.CurrentPhase);
            Assert.Empty(EngineKernel.Instance.Services.GetRegisteredServices());
        }

        Assert.Equal(cycleCount, TestPackageEvents.Events.Count(entry => entry == "load:provider"));
        Assert.Equal(cycleCount, TestPackageEvents.Events.Count(entry => entry == "unload:provider"));
    }

    [Fact]
    public void PackageOnlyMountRegistersServicesWithoutStartingSubsystemPhases()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });

        EngineKernel.Instance.MountPackageGraph(new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        });

        Assert.True(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.Equal(EnginePhase.None, EngineKernel.Instance.CurrentPhase);
        Assert.Contains("load:provider", TestPackageEvents.Events);
        Assert.True(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));

        EngineKernel.Instance.Shutdown();

        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.Equal(new[] { "load:provider", "unload:provider" }, TestPackageEvents.Events);
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
    }

    [Fact]
    public void BootstrapperHandsPackageOnlyHostOffBeforeSubsystemInitialization()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        workspace.AddPackage(
            "com.test.package-only-host",
            typeof(PackageOnlyHostPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IApplicationHost).FullName! }
            });
        workspace.WriteProjectManifest("com.test.package-only-host");

        EngineBootstrapper.Run(new[]
        {
            "--workspace", workspace.Root,
            "--profile", "Development",
            "--allow-manifest-fallback"
        });

        Assert.True(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.Equal(EnginePhase.None, EngineKernel.Instance.CurrentPhase);
        Assert.Contains("run:package-only-host", TestPackageEvents.Events);
        Assert.False(CountingTickSubsystem.WasInitialized);

        EngineKernel.Instance.Shutdown();
    }

    [Fact]
    public void EngineKernelRejectsResetFromPackageOnLoadBeforeMutation()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.reentrant-load-reset",
            typeof(ResetFromLoadPackageEntry));
        EngineShutdownOwnershipSnapshot before =
            EngineKernel.Instance.GetShutdownOwnershipSnapshot();
        RenderSurfaceRegistry surfaces = EngineKernel.Instance.RenderSurfaces;

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { packagePath }
            }));

        Assert.Contains(
            "cannot reset while package graph mounting is in progress",
            failure.Message,
            StringComparison.Ordinal);
        Assert.Equal(before, EngineKernel.Instance.GetShutdownOwnershipSnapshot());
        Assert.Same(surfaces, EngineKernel.Instance.RenderSurfaces);
        Assert.False(surfaces.IsDisposed);
        Assert.Equal(EnginePhase.None, EngineKernel.Instance.CurrentPhase);
        Assert.Null(EngineKernel.Instance.Config);
        Assert.Null(EngineKernel.Instance.GetSubsystem<PackageSubsystem>());
        Assert.Equal(new[] { "load:reentrant-reset" }, TestPackageEvents.Events);
    }

    [Fact]
    public void EngineKernelRejectsNestedMountFromPackageOnLoadBeforeMutation()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.reentrant-load-mount",
            typeof(NestedMountFromLoadPackageEntry));
        EngineShutdownOwnershipSnapshot before =
            EngineKernel.Instance.GetShutdownOwnershipSnapshot();
        RenderSurfaceRegistry surfaces = EngineKernel.Instance.RenderSurfaces;

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { packagePath }
            }));

        Assert.Contains(
            "cannot mount packages while package graph mounting is in progress",
            failure.Message,
            StringComparison.Ordinal);
        Assert.Equal(before, EngineKernel.Instance.GetShutdownOwnershipSnapshot());
        Assert.Same(surfaces, EngineKernel.Instance.RenderSurfaces);
        Assert.False(surfaces.IsDisposed);
        Assert.Equal(EnginePhase.None, EngineKernel.Instance.CurrentPhase);
        Assert.Null(EngineKernel.Instance.Config);
        Assert.Null(EngineKernel.Instance.GetSubsystem<PackageSubsystem>());
        Assert.Equal(new[] { "load:reentrant-mount" }, TestPackageEvents.Events);
    }

    [Fact]
    public void BootstrapperFailureDiagnosticsCannotSuppressCleanupDrain()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.retryable",
            typeof(RetryableOwnershipPackageEntry));
        var logger = new SelectiveThrowingLogger(
            "[Host] FATAL ERROR: Engine startup failed:",
            "[Host] Package shutdown after initialization failure also failed:");

        try
        {
            EngineKernel.Instance.RegisterKernelOwnedService<ILogger>(logger);
            KernelLog.InvalidateCache();
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { packagePath }
            });

            EngineBootstrapper.HandleInitializationFailure(
                EngineKernel.Instance,
                new InvalidOperationException("injected initialization failure"));

            Assert.Equal(2, logger.ThrowCount);
            Assert.False(EngineKernel.Instance.HasPendingPackageCleanup);
            Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
            Assert.Equal(
                new[]
                {
                    "load:retryable",
                    "unload:retryable:1",
                    "unload:retryable:2"
                },
                TestPackageEvents.Events);
            Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
        }
        finally
        {
            KernelLog.InvalidateCache();
        }
    }

    [Fact]
    public void BootstrapperDrainsRetainedStartupOwnershipBeforeRequestingExit()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        workspace.AddPackage(
            "com.test.runtime.partial-retryable",
            typeof(PartialLoadRetryableOwnershipPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IPartialLoadService).FullName! }
            });
        workspace.WriteProjectManifest("com.test.runtime.partial-retryable");
        int? requestedExitCode = null;

        EngineBootstrapper.Run(
            new[]
            {
                "--workspace", workspace.Root,
                "--profile", "Development",
                "--allow-manifest-fallback"
            },
            exitCode =>
            {
                Assert.Equal(
                    new[]
                    {
                        "load:partial-retryable",
                        "unload:partial-retryable:1",
                        "unload:partial-retryable:2"
                    },
                    TestPackageEvents.Events);
                Assert.False(EngineKernel.Instance.HasPendingPackageCleanup);
                Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
                Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
                requestedExitCode = exitCode;
            });

        Assert.Equal(1, requestedExitCode);
    }

    [Fact]
    public void BootstrapperCleansKernelOwnershipAfterCleanMountFailure()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        workspace.AddPackage(
            "com.test.runtime.failing-load",
            typeof(BareThrowingLoadPackageEntry));
        workspace.WriteProjectManifest("com.test.runtime.failing-load");
        LifecycleFaultInjection.Arm(
            LifecycleFaultStage.PackageLoad,
            "injected clean package load failure");
        int? requestedExitCode = null;

        EngineBootstrapper.Run(
            new[]
            {
                "--workspace", workspace.Root,
                "--profile", "Development",
                "--allow-manifest-fallback"
            },
            exitCode =>
            {
                Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
                Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
                requestedExitCode = exitCode;
            });

        Assert.Equal(1, requestedExitCode);
        Assert.Equal(new[] { "load:failing" }, TestPackageEvents.Events);
        LifecycleFaultInjection.EnsureFullyConsumed();
    }

    [Fact]
    public void BootstrapperReportsStalledStartupCleanupWithoutRequestingExit()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        workspace.AddPackage(
            "com.test.runtime.always-pending",
            typeof(AlwaysPendingOwnershipPackageEntry),
            subsystems: new object[]
            {
                new
                {
                    @class = typeof(ThrowingInitializeSubsystem).FullName!,
                    phase = "PreInit",
                    priority = 0
                }
            });
        workspace.WriteProjectManifest("com.test.runtime.always-pending");
        LifecycleFaultInjection.Arm(
            LifecycleFaultStage.SubsystemInitialize,
            "injected startup subsystem failure");
        bool exitRequested = false;

        try
        {
            AggregateException failure = Assert.Throws<AggregateException>(() =>
                EngineBootstrapper.Run(
                    new[]
                    {
                        "--workspace", workspace.Root,
                        "--profile", "Development",
                        "--allow-manifest-fallback"
                    },
                    _ => exitRequested = true));

            Assert.Contains(
                "startup cleanup stalled",
                failure.Message,
                StringComparison.OrdinalIgnoreCase);
            Assert.False(exitRequested);
            Assert.Equal(2, AlwaysPendingOwnershipPackageEntry.UnloadAttemptCount);
            Assert.True(EngineKernel.Instance.HasPendingPackageCleanup);
            Assert.True(EngineKernel.Instance.HasPackageRuntimeOwnership);
            Assert.Equal(EnginePhase.PreShutdown, EngineKernel.Instance.CurrentPhase);
            LifecycleFaultInjection.EnsureFullyConsumed();
        }
        finally
        {
            AlwaysPendingOwnershipPackageEntry.AllowCleanup = true;
            EngineKernel.Instance.Shutdown();
        }

        Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
    }

    [Fact]
    public void BootstrapperManifestFailureRequestsExitWithoutInstallingKernelOwnership()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        workspace.WriteProjectManifest();
        EngineShutdownOwnershipSnapshot before =
            EngineKernel.Instance.GetShutdownOwnershipSnapshot();
        int? requestedExitCode = null;

        EngineBootstrapper.Run(
            new[]
            {
                "--workspace", workspace.Root,
                "--profile", "Development",
                "--allow-manifest-fallback"
            },
            exitCode =>
            {
                Assert.Equal(before, EngineKernel.Instance.GetShutdownOwnershipSnapshot());
                Assert.Null(EngineKernel.Instance.GetSubsystem<PackageSubsystem>());
                Assert.Empty(EngineKernel.Instance.Services.GetRegisteredServices());
                requestedExitCode = exitCode;
            });

        Assert.Equal(1, requestedExitCode);
    }

    [Fact]
    public void BootstrapperCompensatesFailureAfterInstallingKernelOwnership()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry));
        workspace.WriteProjectManifest("com.test.runtime.provider");
        var logger = new SelectiveThrowingLogger(
            "Diagnostic source-asset selection is enabled");
        int? requestedExitCode = null;

        try
        {
            EngineKernel.Instance.RegisterKernelOwnedService<ILogger>(logger);
            KernelLog.InvalidateCache();

            EngineBootstrapper.Run(
                new[]
                {
                    "--workspace", workspace.Root,
                    "--profile", "Development",
                    "--allow-manifest-fallback",
                    RuntimeAssetOptions.SourceDiagnosticsArgument
                },
                exitCode =>
                {
                    Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
                    Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
                    requestedExitCode = exitCode;
                });

            Assert.Equal(1, requestedExitCode);
            Assert.Equal(1, logger.ThrowCount);
        }
        finally
        {
            KernelLog.InvalidateCache();
        }
    }

    [Fact]
    public void BootstrapperCompensatesFailureDuringStartupCompletionDiagnostics()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry));
        workspace.WriteProjectManifest("com.test.runtime.provider");
        var logger = new SelectiveThrowingLogger(
            "[Host] Kernel Initialization Complete.");
        int? requestedExitCode = null;

        try
        {
            EngineKernel.Instance.RegisterKernelOwnedService<ILogger>(logger);
            KernelLog.InvalidateCache();

            EngineBootstrapper.Run(
                new[]
                {
                    "--workspace", workspace.Root,
                    "--profile", "Development",
                    "--allow-manifest-fallback"
                },
                exitCode =>
                {
                    Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
                    Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
                    requestedExitCode = exitCode;
                });

            Assert.Equal(1, requestedExitCode);
            Assert.Equal(1, logger.ThrowCount);
            Assert.Equal(
                new[] { "load:provider", "unload:provider" },
                TestPackageEvents.Events);
        }
        finally
        {
            KernelLog.InvalidateCache();
        }
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
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.Null(EngineKernel.Instance.Config);
        Assert.Null(EngineKernel.Instance.GetSubsystem<PackageSubsystem>());
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.Equal(new[] { "load:provider", "unload:provider" }, TestPackageEvents.Events);
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

    [Fact]
    public void EngineKernelRollsBackCurrentPackageWhenProviderValidationFails()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.invalid-provider",
            typeof(InvalidProviderPackageEntry),
            services: new
            {
                provides = new object[]
                {
                    typeof(IRuntimeSmokeService).FullName!,
                    typeof(IMissingRuntimeService).FullName!
                }
            });
        var packageSubsystem = new PackageSubsystem();
        EngineKernel.Instance.RegisterSubsystem(packageSubsystem);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { packagePath }
            }));

        Assert.Contains(typeof(IMissingRuntimeService).FullName!, exception.Message);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.Null(EngineKernel.Instance.Config);
        Assert.Empty(packageSubsystem.GetAllPackages());
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.Null(EngineKernel.Instance.GetSubsystem<RollbackProbeSubsystem>());
        Assert.Equal(
            new[] { "load:invalid-provider", "unload:invalid-provider" },
            TestPackageEvents.Events);

        string recoveryPath = workspace.AddPackage(
            "com.test.runtime.recovery-provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        EngineKernel.Instance.MountPackageGraph(new EngineConfig
        {
            PackageUrls = new List<string> { recoveryPath }
        });

        Assert.True(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.True(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        EngineKernel.Instance.Shutdown();
    }

    [Fact]
    public void EngineKernelRollsBackEarlierPackagesWhenLaterPackageLoadFails()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        string failingPath = workspace.AddPackage(
            "com.test.runtime.failing-load",
            typeof(ThrowingLoadPackageEntry));
        var packageSubsystem = new PackageSubsystem();
        EngineKernel.Instance.RegisterSubsystem(packageSubsystem);
        LifecycleFaultInjection.Arm(
            LifecycleFaultStage.PackageLoad,
            "injected package load failure");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { providerPath, failingPath }
            }));

        Assert.Contains("injected package load failure", exception.Message);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.Empty(packageSubsystem.GetAllPackages());
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.False(EngineKernel.Instance.Services.TryGetService<IPartialLoadService>(out _));
        Assert.Null(EngineKernel.Instance.GetSubsystem<RollbackProbeSubsystem>());
        Assert.Equal(
            new[] { "load:provider", "load:failing", "unload:provider" },
            TestPackageEvents.Events);
        LifecycleFaultInjection.EnsureFullyConsumed();
    }

    [Fact]
    public void EngineKernelInitializationFailureShutsDownStartedSubsystemsAndPackages()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        EngineKernel.Instance.RegisterSubsystem(new InitializationFollowerSubsystem());
        EngineKernel.Instance.RegisterSubsystem(new ThrowingInitializeSubsystem());
        LifecycleFaultInjection.Arm(
            LifecycleFaultStage.SubsystemInitialize,
            "injected subsystem initialization failure");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EngineKernel.Instance.Initialize(new EngineConfig
            {
                PackageUrls = new List<string> { providerPath }
            }));

        Assert.Contains("injected subsystem initialization failure", exception.Message);
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.Equal(
            new[]
            {
                "load:provider",
                "initialize:follower",
                "initialize:throwing",
                "shutdown:throwing-initialize",
                "shutdown:follower",
                "unload:provider"
            },
            TestPackageEvents.Events);
        LifecycleFaultInjection.EnsureFullyConsumed();
    }

    [Fact]
    public void EngineKernelShutdownContinuesAfterSubsystemFailureAndReportsItAgain()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        EngineKernel.Instance.RegisterSubsystem(new ShutdownFollowerSubsystem());
        EngineKernel.Instance.RegisterSubsystem(new ThrowingShutdownSubsystem());
        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        });
        LifecycleFaultInjection.Arm(
            LifecycleFaultStage.SubsystemShutdown,
            "injected subsystem shutdown failure");

        var firstFailure = Assert.Throws<AggregateException>(() => EngineKernel.Instance.Shutdown());

        Assert.Contains(firstFailure.InnerExceptions, error =>
            error.Message.Contains("injected subsystem shutdown failure", StringComparison.Ordinal));
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.Contains("shutdown:throwing", TestPackageEvents.Events);
        Assert.Contains("shutdown:following", TestPackageEvents.Events);
        Assert.Contains("unload:provider", TestPackageEvents.Events);
        int eventCountAfterFirstShutdown = TestPackageEvents.Events.Count;

        var repeatedFailure = Assert.Throws<AggregateException>(() => EngineKernel.Instance.Shutdown());

        Assert.Same(firstFailure, repeatedFailure);
        Assert.Equal(eventCountAfterFirstShutdown, TestPackageEvents.Events.Count);
        LifecycleFaultInjection.EnsureFullyConsumed();
    }

    [Fact]
    public void EngineKernelRejectsReentrantResetAndCompletesOuterShutdown()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        EngineKernel.Instance.RegisterSubsystem(new ShutdownFollowerSubsystem());
        EngineKernel.Instance.RegisterSubsystem(
            new ReentrantResetShutdownSubsystem(EngineKernel.Instance));
        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        });

        AggregateException failure = Assert.Throws<AggregateException>(
            () => EngineKernel.Instance.Shutdown());

        Assert.Contains(
            failure.InnerExceptions,
            error => error.Message.Contains(
                "cannot reset while shutdown is in progress",
                StringComparison.Ordinal));
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.Contains("shutdown:reentrant-reset", TestPackageEvents.Events);
        Assert.Contains("shutdown:following", TestPackageEvents.Events);
        Assert.Contains("unload:provider", TestPackageEvents.Events);
        EngineShutdownOwnershipSnapshot ownership =
            EngineKernel.Instance.GetShutdownOwnershipSnapshot();
        Assert.True(ownership.IsClean, ownership.ToString());
    }

    [Fact]
    public void EngineKernelRejectsReentrantInitializeAndRequiresResetAfterShutdown()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        var config = new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        };
        EngineKernel.Instance.RegisterSubsystem(new ShutdownFollowerSubsystem());
        EngineKernel.Instance.RegisterSubsystem(
            new ReentrantInitializeShutdownSubsystem(EngineKernel.Instance, config));
        EngineKernel.Instance.Initialize(config);

        AggregateException failure = Assert.Throws<AggregateException>(
            () => EngineKernel.Instance.Shutdown());

        Assert.Contains(
            failure.InnerExceptions,
            error => error.Message.Contains(
                "cannot initialize while shutdown is in progress",
                StringComparison.Ordinal));
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.Contains("shutdown:reentrant-initialize", TestPackageEvents.Events);
        Assert.Contains("shutdown:following", TestPackageEvents.Events);
        Assert.Contains("unload:provider", TestPackageEvents.Events);
        EngineShutdownOwnershipSnapshot ownership =
            EngineKernel.Instance.GetShutdownOwnershipSnapshot();
        Assert.True(ownership.IsClean, ownership.ToString());

        InvalidOperationException terminalFailure = Assert.Throws<InvalidOperationException>(
            () => EngineKernel.Instance.Initialize(config));
        Assert.Contains("call Reset", terminalFailure.Message, StringComparison.Ordinal);

        EngineKernel.Instance.Reset();
        EngineKernel.Instance.Initialize(config);
        Assert.Equal(EnginePhase.Running, EngineKernel.Instance.CurrentPhase);
        EngineKernel.Instance.Shutdown();
    }

    [Fact]
    public void EngineKernelRejectsShutdownFromSubsystemInitializeAndCompensates()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        EngineKernel.Instance.RegisterSubsystem(
            new ShutdownFromInitializeSubsystem(EngineKernel.Instance));

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            EngineKernel.Instance.Initialize(new EngineConfig
            {
                PackageUrls = new List<string> { providerPath }
            }));

        Assert.Contains(
            "cannot shutdown while initialization is in progress",
            failure.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            new[]
            {
                "load:provider",
                "initialize:reentrant-shutdown",
                "shutdown:reentrant-shutdown",
                "unload:provider"
            },
            TestPackageEvents.Events);
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
    }

    [Fact]
    public void EngineKernelRejectsResetFromSubsystemInitializeAndCompensates()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        EngineKernel.Instance.RegisterSubsystem(
            new ResetFromInitializeSubsystem(EngineKernel.Instance));

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            EngineKernel.Instance.Initialize(new EngineConfig
            {
                PackageUrls = new List<string> { providerPath }
            }));

        Assert.Contains(
            "cannot reset while initialization is in progress",
            failure.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            new[]
            {
                "load:provider",
                "initialize:reentrant-reset",
                "shutdown:reentrant-reset-initialize",
                "unload:provider"
            },
            TestPackageEvents.Events);
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
    }

    [Fact]
    public async Task EngineKernelKeepsExclusiveAdmissionThroughInitializationCompensation()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        var config = new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        };
        using var subsystem = new BlockingFailedInitializeSubsystem();
        EngineKernel.Instance.RegisterSubsystem(subsystem);

        Task<Exception> initialization = Task.Run(() =>
            Record.Exception(() => EngineKernel.Instance.Initialize(config)));
        try
        {
            Assert.True(subsystem.ShutdownEntered.Wait(TimeSpan.FromSeconds(10)));

            Assert.Contains(
                "cannot reset while shutdown is in progress",
                Assert.Throws<InvalidOperationException>(EngineKernel.Instance.Reset).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "cannot mount packages while shutdown is in progress",
                Assert.Throws<InvalidOperationException>(() =>
                    EngineKernel.Instance.MountPackageGraph(new EngineConfig())).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "cannot initialize while shutdown is in progress",
                Assert.Throws<InvalidOperationException>(() =>
                    EngineKernel.Instance.Initialize(config)).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "cannot run the engine while shutdown is in progress",
                Assert.Throws<InvalidOperationException>(() => EngineKernel.Instance.Run()).Message,
                StringComparison.Ordinal);
        }
        finally
        {
            subsystem.AllowShutdown.Set();
        }

        Exception failure = Assert.IsType<InvalidOperationException>(
            await initialization.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Contains("injected blocking initialization failure", failure.Message);
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
    }

    [Fact]
    public void EngineKernelRejectsLifecycleMutationFromFrameCallback()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        var subsystem = new ResetFromTickSubsystem(EngineKernel.Instance);
        EngineKernel.Instance.RegisterSubsystem(subsystem);
        EngineKernel.Instance.MountPackageGraph(new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        });

        int exitCode = EngineKernel.Instance.RunForFrames(1);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, subsystem.TickCount);
        InvalidOperationException failure = Assert.IsType<InvalidOperationException>(
            subsystem.ResetFailure);
        Assert.Contains(
            "cannot reset while engine run loop is in progress",
            failure.Message,
            StringComparison.Ordinal);
        Assert.True(subsystem.WasShutdown);
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
    }

    [Fact]
    public void EngineKernelRejectsLifecycleMutationFromDirectTickCallback()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        var subsystem = new ResetFromTickSubsystem(EngineKernel.Instance);
        EngineKernel.Instance.RegisterSubsystem(subsystem);
        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        });

        EngineKernel.Instance.Tick(1.0f / 60.0f);

        Assert.Equal(1, subsystem.TickCount);
        InvalidOperationException failure = Assert.IsType<InvalidOperationException>(
            subsystem.ResetFailure);
        Assert.Contains(
            "cannot reset while engine run loop is in progress",
            failure.Message,
            StringComparison.Ordinal);
        Assert.Equal(1u, EngineKernel.Instance.CurrentFrameIndex);
        Assert.False(subsystem.WasShutdown);
        Assert.Equal(EnginePhase.Running, EngineKernel.Instance.CurrentPhase);

        EngineKernel.Instance.Shutdown();

        Assert.True(subsystem.WasShutdown);
        Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
    }

    [Fact]
    public async Task EngineKernelKeepsRegistrationClosedUntilResetCompletes()
    {
        using var subsystem = new BlockingShutdownSubsystem();
        EngineKernel.Instance.RegisterSubsystem(subsystem);
        EngineKernel.Instance.Initialize(new EngineConfig());

        Task reset = Task.Run(EngineKernel.Instance.Reset);
        try
        {
            Assert.True(subsystem.ShutdownEntered.Wait(TimeSpan.FromSeconds(10)));

            InvalidOperationException serviceFailure = Assert.Throws<InvalidOperationException>(() =>
                EngineKernel.Instance.RegisterKernelOwnedService<IPartialLoadService>(
                    new PartialLoadService()));
            Assert.Contains(
                "registration is closed",
                serviceFailure.Message,
                StringComparison.Ordinal);
            InvalidOperationException subsystemFailure = Assert.Throws<InvalidOperationException>(() =>
                EngineKernel.Instance.RegisterSubsystem(new CountingTickSubsystem()));
            Assert.Contains(
                "while reset is in progress",
                subsystemFailure.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            subsystem.AllowShutdown.Set();
        }

        await reset.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(EnginePhase.None, EngineKernel.Instance.CurrentPhase);
        Assert.Empty(EngineKernel.Instance.Services.GetRegisteredServices());
        EngineKernel.Instance.RegisterKernelOwnedService<IPartialLoadService>(
            new PartialLoadService());
        Assert.True(EngineKernel.Instance.Services.TryGetService<IPartialLoadService>(out _));
        EngineKernel.Instance.Shutdown();
        Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
    }

    [Fact]
    public async Task EngineKernelExcludesLifecycleEntryDuringSubsystemRegistration()
    {
        using var subsystem = new BlockingRegistrationSubsystem();
        Task registration = Task.Run(() =>
            EngineKernel.Instance.RegisterSubsystem(subsystem));
        try
        {
            Assert.True(subsystem.RegistrationEntered.Wait(TimeSpan.FromSeconds(10)));

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                EngineKernel.Instance.Reset);
            Assert.Contains(
                "cannot reset while subsystem registration is in progress",
                failure.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            subsystem.AllowRegistration.Set();
        }

        await registration.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Same(subsystem, EngineKernel.Instance.GetSubsystem<BlockingRegistrationSubsystem>());

        EngineKernel.Instance.Reset();

        Assert.Null(EngineKernel.Instance.GetSubsystem<BlockingRegistrationSubsystem>());
        Assert.Equal(EnginePhase.None, EngineKernel.Instance.CurrentPhase);
    }

    [Fact]
    public void EngineKernelTracksSubsystemMetadataByReferenceIdentity()
    {
        var subsystem = new ThrowingValueEqualitySubsystem();

        EngineKernel.Instance.RegisterSubsystem(subsystem);

        Assert.Same(
            subsystem,
            EngineKernel.Instance.GetSubsystem<ThrowingValueEqualitySubsystem>());
        EngineKernel.Instance.Reset();
        Assert.Null(EngineKernel.Instance.GetSubsystem<ThrowingValueEqualitySubsystem>());
    }

    [Fact]
    public void EngineKernelRejectsRunFromSubsystemShutdownAndCompletesOuterShutdown()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        EngineKernel.Instance.RegisterSubsystem(new ShutdownFollowerSubsystem());
        EngineKernel.Instance.RegisterSubsystem(
            new RunFromShutdownSubsystem(EngineKernel.Instance));
        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        });

        AggregateException failure = Assert.Throws<AggregateException>(
            () => EngineKernel.Instance.Shutdown());

        Assert.Contains(
            failure.InnerExceptions,
            error => error.Message.Contains(
                "cannot run the engine while shutdown is in progress",
                StringComparison.Ordinal));
        Assert.Equal(0u, EngineKernel.Instance.CurrentFrameIndex);
        Assert.Contains("shutdown:reentrant-run", TestPackageEvents.Events);
        Assert.Contains("shutdown:following", TestPackageEvents.Events);
        Assert.Contains("unload:provider", TestPackageEvents.Events);
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
    }

    [Fact]
    public void PackageSubsystemRejectsDirectReentrantShutdownAndDrainsOnce()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        string reentrantPath = workspace.AddPackage(
            "com.test.runtime.reentrant-package-shutdown",
            typeof(ReentrantPackageShutdownEntry),
            dependencies: new Dictionary<string, string>
            {
                ["com.test.runtime.provider"] = "1.0.0"
            },
            services: new
            {
                provides = new object[] { typeof(IPartialLoadService).FullName! }
            });
        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { reentrantPath, providerPath }
        });

        AggregateException failure = Assert.Throws<AggregateException>(
            () => EngineKernel.Instance.Shutdown());

        Assert.Contains(
            failure.Flatten().InnerExceptions,
            error => error.Message.Contains(
                "cannot shutdown while package shutdown is in progress",
                StringComparison.Ordinal));
        Assert.Equal(
            new[]
            {
                "load:provider",
                "load:reentrant-package-shutdown",
                "unload:reentrant-package-shutdown",
                "unload:provider"
            },
            TestPackageEvents.Events);
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.False(EngineKernel.Instance.Services.TryGetService<IPartialLoadService>(out _));
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
    }

    [Fact]
    public void PackageSubsystemRejectsDirectShutdownDuringMountRollback()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string reentrantPath = workspace.AddPackage(
            "com.test.runtime.reentrant-package-shutdown",
            typeof(ReentrantPackageShutdownEntry),
            services: new
            {
                provides = new object[] { typeof(IPartialLoadService).FullName! }
            });
        string failingPath = workspace.AddPackage(
            "com.test.runtime.failing-load",
            typeof(BareThrowingLoadPackageEntry),
            dependencies: new Dictionary<string, string>
            {
                ["com.test.runtime.reentrant-package-shutdown"] = "1.0.0"
            });
        LifecycleFaultInjection.Arm(
            LifecycleFaultStage.PackageLoad,
            "injected package load failure");
        EngineShutdownOwnershipSnapshot before =
            EngineKernel.Instance.GetShutdownOwnershipSnapshot();

        AggregateException failure = Assert.Throws<AggregateException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { failingPath, reentrantPath }
            }));

        Assert.Contains(
            failure.Flatten().InnerExceptions,
            error => error.Message.Contains(
                "injected package load failure",
                StringComparison.Ordinal));
        Assert.Contains(
            failure.Flatten().InnerExceptions,
            error => error.Message.Contains(
                "cannot shutdown while package mounting is in progress",
                StringComparison.Ordinal));
        Assert.Equal(
            new[]
            {
                "load:reentrant-package-shutdown",
                "load:failing",
                "unload:reentrant-package-shutdown"
            },
            TestPackageEvents.Events);
        Assert.Equal(before, EngineKernel.Instance.GetShutdownOwnershipSnapshot());
        Assert.Null(EngineKernel.Instance.Config);
        Assert.Null(EngineKernel.Instance.GetSubsystem<PackageSubsystem>());
        Assert.False(EngineKernel.Instance.Services.TryGetService<IPartialLoadService>(out _));
        LifecycleFaultInjection.EnsureFullyConsumed();
    }

    [Fact]
    public void EngineKernelMountCompletionDiagnosticFailureRollsBackMountedGraph()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        var config = new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        };
        var logger = new SelectiveThrowingLogger(
            "[EngineKernel] Package mount complete.");

        try
        {
            EngineKernel.Instance.RegisterKernelOwnedService<ILogger>(logger);
            KernelLog.InvalidateCache();
            EngineShutdownOwnershipSnapshot before =
                EngineKernel.Instance.GetShutdownOwnershipSnapshot();

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => EngineKernel.Instance.MountPackageGraph(config));

            Assert.Contains("Injected logger failure", failure.Message, StringComparison.Ordinal);
            Assert.Equal(1, logger.ThrowCount);
            Assert.Equal(
                new[] { "load:provider", "unload:provider" },
                TestPackageEvents.Events);
            Assert.Equal(before, EngineKernel.Instance.GetShutdownOwnershipSnapshot());
            Assert.Null(EngineKernel.Instance.Config);
            Assert.Null(EngineKernel.Instance.GetSubsystem<PackageSubsystem>());
            Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));

            EngineKernel.Instance.MountPackageGraph(config);
            Assert.True(EngineKernel.Instance.IsPackageGraphMounted);
            Assert.True(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
            EngineKernel.Instance.Shutdown();
            Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
        }
        finally
        {
            KernelLog.InvalidateCache();
        }
    }

    [Fact]
    public void EngineKernelInitializePreservesCleanMountFailureRetry()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string failingPath = workspace.AddPackage(
            "com.test.runtime.failing-load",
            typeof(BareThrowingLoadPackageEntry));
        LifecycleFaultInjection.Arm(
            LifecycleFaultStage.PackageLoad,
            "injected clean initialization mount failure");
        RenderSurfaceRegistry surfaces = EngineKernel.Instance.RenderSurfaces;

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            EngineKernel.Instance.Initialize(new EngineConfig
            {
                PackageUrls = new List<string> { failingPath }
            }));

        Assert.Contains("injected clean initialization mount failure", failure.Message);
        Assert.Equal(EnginePhase.None, EngineKernel.Instance.CurrentPhase);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.Null(EngineKernel.Instance.Config);
        Assert.Same(surfaces, EngineKernel.Instance.RenderSurfaces);
        Assert.False(surfaces.IsDisposed);
        Assert.Null(EngineKernel.Instance.GetSubsystem<PackageSubsystem>());
        LifecycleFaultInjection.EnsureFullyConsumed();

        string providerPath = workspace.AddPackage(
            "com.test.runtime.recovery-provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { providerPath }
        });
        Assert.Equal(EnginePhase.Running, EngineKernel.Instance.CurrentPhase);
        EngineKernel.Instance.Shutdown();
        Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
    }

    [Fact]
    public void EngineKernelFinalRunningDiagnosticFailureCompensatesInitialization()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        var logger = new SelectiveThrowingLogger(
            "[EngineKernel] Engine is now Running.");

        try
        {
            EngineKernel.Instance.RegisterKernelOwnedService<ILogger>(logger);
            KernelLog.InvalidateCache();
            EngineKernel.Instance.RegisterSubsystem(new ShutdownFollowerSubsystem());

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                EngineKernel.Instance.Initialize(new EngineConfig
                {
                    PackageUrls = new List<string> { providerPath }
                }));

            Assert.Contains("Injected logger failure", failure.Message, StringComparison.Ordinal);
            Assert.Equal(1, logger.ThrowCount);
            Assert.Contains("shutdown:following", TestPackageEvents.Events);
            Assert.Contains("unload:provider", TestPackageEvents.Events);
            Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
            Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
        }
        finally
        {
            KernelLog.InvalidateCache();
        }
    }

    [Fact]
    public void EngineKernelClosesServiceRegistrationBeforeShutdownDiagnostics()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        var logger = new MutatingShutdownLogger();

        try
        {
            EngineKernel.Instance.RegisterKernelOwnedService<ILogger>(logger);
            KernelLog.InvalidateCache();
            EngineKernel.Instance.Initialize(new EngineConfig
            {
                PackageUrls = new List<string> { providerPath }
            });

            EngineKernel.Instance.Shutdown();

            InvalidOperationException failure = Assert.IsType<InvalidOperationException>(
                logger.RegistrationFailure);
            Assert.Contains(
                "registration is closed during engine shutdown",
                failure.Message,
                StringComparison.Ordinal);
            Assert.False(EngineKernel.Instance.Services.TryGetService<IPartialLoadService>(out _));
            Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
        }
        finally
        {
            KernelLog.InvalidateCache();
        }
    }

    [Fact]
    public void PackageMountRollsBackEarlierNativeRuntimeWhenLaterLoadFails()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.native-rollback",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            },
            nativeRuntimes: new Dictionary<string, object[]>
            {
                ["win-x64"] = new object[]
                {
                    new { path = "First.Native.dll", initExport = "Init", shutdownExport = "Shutdown" },
                    new { path = "Second.Native.dll", initExport = "Init", shutdownExport = "Shutdown" }
                }
            });
        var nativeApi = new FakeNativePackageRuntimeApi { FailLoadAttempt = 2 };
        var packageSubsystem = new PackageSubsystem(nativeApi);
        EngineKernel.Instance.RegisterSubsystem(packageSubsystem);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { packagePath }
            }));

        Assert.Contains("injected native load failure", exception.Message);
        Assert.Empty(nativeApi.LiveHandles);
        Assert.Equal(
            new[]
            {
                "load:First.Native.dll",
                "init:First.Native.dll",
                "load:Second.Native.dll",
                "shutdown:First.Native.dll",
                "free:First.Native.dll"
            },
            nativeApi.Events);
        Assert.Equal(new[] { "load:provider", "unload:provider" }, TestPackageEvents.Events);
        Assert.Empty(packageSubsystem.GetAllPackages());
        Assert.Equal(0, packageSubsystem.LoadedContextCount);
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
    }

    [Fact]
    public void PackageShutdownFreesNativeRuntimeAndContinuesAfterShutdownHookFailure()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.native-shutdown-failure",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            },
            nativeRuntimes: new Dictionary<string, object[]>
            {
                ["win-x64"] = new object[]
                {
                    new { path = "Stable.Native.dll", initExport = "Init", shutdownExport = "Shutdown" },
                    new { path = "ShutdownFailure.Native.dll", initExport = "Init", shutdownExport = "Shutdown" }
                }
            });
        var nativeApi = new FakeNativePackageRuntimeApi
        {
            FailShutdownPath = "ShutdownFailure.Native.dll"
        };
        var packageSubsystem = new PackageSubsystem(nativeApi);
        EngineKernel.Instance.RegisterSubsystem(packageSubsystem);
        EngineKernel.Instance.MountPackageGraph(new EngineConfig
        {
            PackageUrls = new List<string> { packagePath }
        });

        var exception = Assert.Throws<AggregateException>(() => EngineKernel.Instance.Shutdown());

        Assert.Contains(exception.Flatten().InnerExceptions, error =>
            error.Message.Contains("injected native shutdown failure", StringComparison.Ordinal));
        Assert.Empty(nativeApi.LiveHandles);
        Assert.Contains("free:ShutdownFailure.Native.dll", nativeApi.Events);
        Assert.Contains("shutdown:Stable.Native.dll", nativeApi.Events);
        Assert.Contains("free:Stable.Native.dll", nativeApi.Events);
        Assert.Empty(packageSubsystem.GetAllPackages());
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
    }

    [Fact]
    public void PackageShutdownContinuesAfterEntryUnloadFailure()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        string failingPath = workspace.AddPackage(
            "com.test.runtime.throwing-unload",
            typeof(ThrowingUnloadPackageEntry));

        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { providerPath, failingPath }
        });
        LifecycleFaultInjection.Arm(
            LifecycleFaultStage.PackageUnload,
            "injected package unload failure");

        var exception = Assert.Throws<AggregateException>(() => EngineKernel.Instance.Shutdown());

        Assert.Contains(exception.Flatten().InnerExceptions, error =>
            error.Message.Contains("injected package unload failure", StringComparison.Ordinal));
        Assert.Equal(
            new[]
            {
                "load:provider",
                "load:throwing-unload",
                "unload:throwing-unload",
                "unload:provider"
            },
            TestPackageEvents.Events);
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.False(EngineKernel.Instance.HasPendingPackageCleanup);
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        int eventCountAfterShutdown = TestPackageEvents.Events.Count;

        AggregateException repeatedFailure = Assert.Throws<AggregateException>(
            () => EngineKernel.Instance.Shutdown());

        Assert.Same(exception, repeatedFailure);
        Assert.Equal(eventCountAfterShutdown, TestPackageEvents.Events.Count);
        LifecycleFaultInjection.EnsureFullyConsumed();
    }

    [Fact]
    public void EngineKernelShutdownRetainsPendingPackageAndDependencyClosureUntilRetryCompletes()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        string retryablePath = workspace.AddPackage(
            "com.test.runtime.retryable",
            typeof(RetryableOwnershipPackageEntry),
            dependencies: new Dictionary<string, string>
            {
                ["com.test.runtime.provider"] = "1.0.0"
            });
        string leafPath = workspace.AddPackage(
            "com.test.runtime.leaf",
            typeof(LeafPackageEntry),
            dependencies: new Dictionary<string, string>
            {
                ["com.test.runtime.retryable"] = "1.0.0"
            });
        var config = new EngineConfig
        {
            PackageUrls = new List<string> { leafPath, retryablePath, providerPath }
        };

        EngineKernel.Instance.Initialize(config);

        AggregateException firstFailure = Assert.Throws<AggregateException>(
            () => EngineKernel.Instance.Shutdown());

        Assert.Contains(
            firstFailure.Flatten().InnerExceptions,
            error => error.Message.Contains("pending ownership", StringComparison.Ordinal));
        Assert.Equal(EnginePhase.PreShutdown, EngineKernel.Instance.CurrentPhase);
        Assert.True(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.True(EngineKernel.Instance.HasPendingPackageCleanup);
        Assert.Same(config, EngineKernel.Instance.Config);
        Assert.True(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.Equal(
            new[] { "com.test.runtime.provider", "com.test.runtime.retryable" },
            EngineKernel.Instance.GetSubsystem<PackageSubsystem>()!
                .GetLoadedPackagesInOrder()
                .Select(package => package.Id));
        Assert.Equal(
            new[]
            {
                "load:provider",
                "load:retryable",
                "load:leaf",
                "unload:leaf",
                "unload:retryable:1"
            },
            TestPackageEvents.Events);

        EngineKernel.Instance.Shutdown();

        Assert.Equal(
            new[]
            {
                "load:provider",
                "load:retryable",
                "load:leaf",
                "unload:leaf",
                "unload:retryable:1",
                "unload:retryable:2",
                "unload:provider"
            },
            TestPackageEvents.Events);
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.False(EngineKernel.Instance.HasPendingPackageCleanup);
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.Empty(EngineKernel.Instance.GetSubsystem<PackageSubsystem>()!.GetAllPackages());
        Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
    }

    [Fact]
    public void EngineKernelPreservesTerminalPackageFailureAcrossPendingCleanupRetry()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        string retryablePath = workspace.AddPackage(
            "com.test.runtime.retryable",
            typeof(RetryableOwnershipPackageEntry),
            dependencies: new Dictionary<string, string>
            {
                ["com.test.runtime.provider"] = "1.0.0"
            });
        string failingLeafPath = workspace.AddPackage(
            "com.test.runtime.throwing-unload",
            typeof(ThrowingUnloadPackageEntry),
            dependencies: new Dictionary<string, string>
            {
                ["com.test.runtime.retryable"] = "1.0.0"
            });

        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string>
            {
                failingLeafPath,
                retryablePath,
                providerPath
            }
        });
        LifecycleFaultInjection.Arm(
            LifecycleFaultStage.PackageUnload,
            "injected terminal package unload failure");

        AggregateException firstFailure = Assert.Throws<AggregateException>(
            () => EngineKernel.Instance.Shutdown());

        Assert.Contains(
            firstFailure.Flatten().InnerExceptions,
            error => error.Message.Contains(
                "injected terminal package unload failure",
                StringComparison.Ordinal));
        Assert.True(EngineKernel.Instance.HasPendingPackageCleanup);
        Assert.Equal(EnginePhase.PreShutdown, EngineKernel.Instance.CurrentPhase);

        AggregateException terminalFailure = Assert.Throws<AggregateException>(
            () => EngineKernel.Instance.Shutdown());

        Assert.Contains(
            terminalFailure.Flatten().InnerExceptions,
            error => error.Message.Contains(
                "injected terminal package unload failure",
                StringComparison.Ordinal));
        Assert.False(EngineKernel.Instance.HasPendingPackageCleanup);
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
        int eventCountAfterCompletion = TestPackageEvents.Events.Count;

        AggregateException repeatedFailure = Assert.Throws<AggregateException>(
            () => EngineKernel.Instance.Shutdown());

        Assert.Same(terminalFailure, repeatedFailure);
        Assert.Equal(eventCountAfterCompletion, TestPackageEvents.Events.Count);
        LifecycleFaultInjection.EnsureFullyConsumed();
    }

    [Fact]
    public void EngineKernelPreservesShutdownDiagnosticFailureAcrossPendingCleanupRetry()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.retryable",
            typeof(RetryableOwnershipPackageEntry));
        var logger = new SelectiveThrowingLogger(
            "[EngineKernel] Package shutdown failed:");

        try
        {
            EngineKernel.Instance.RegisterKernelOwnedService<ILogger>(logger);
            KernelLog.InvalidateCache();
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { packagePath }
            });

            AggregateException firstFailure = Assert.Throws<AggregateException>(
                () => EngineKernel.Instance.Shutdown());

            Assert.Contains(
                "Injected logger failure",
                firstFailure.ToString(),
                StringComparison.Ordinal);
            Assert.True(EngineKernel.Instance.HasPendingPackageCleanup);
            Assert.Equal(EnginePhase.PreShutdown, EngineKernel.Instance.CurrentPhase);
            Assert.Equal(1, logger.ThrowCount);

            AggregateException terminalFailure = Assert.Throws<AggregateException>(
                () => EngineKernel.Instance.Shutdown());

            Assert.Contains(
                "Injected logger failure",
                terminalFailure.ToString(),
                StringComparison.Ordinal);
            Assert.False(EngineKernel.Instance.HasPendingPackageCleanup);
            Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
            Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);

            AggregateException repeatedFailure = Assert.Throws<AggregateException>(
                () => EngineKernel.Instance.Shutdown());

            Assert.Same(terminalFailure, repeatedFailure);
            Assert.Equal(1, logger.ThrowCount);
        }
        finally
        {
            KernelLog.InvalidateCache();
        }
    }

    [Fact]
    public void EngineKernelRetainsProvisionalFailedLoadUntilShutdownCompletesCleanup()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.partial-retryable",
            typeof(PartialLoadRetryableOwnershipPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IPartialLoadService).FullName! }
            });
        var config = new EngineConfig
        {
            PackageUrls = new List<string> { packagePath }
        };

        AggregateException mountFailure = Assert.Throws<AggregateException>(
            () => EngineKernel.Instance.MountPackageGraph(config));

        Assert.Contains(
            mountFailure.Flatten().InnerExceptions,
            error => error.Message.Contains("injected partial load failure", StringComparison.Ordinal));
        Assert.Contains(
            mountFailure.Flatten().InnerExceptions,
            error => error.Message.Contains("injected partial rollback failure", StringComparison.Ordinal));
        Assert.Equal(EnginePhase.None, EngineKernel.Instance.CurrentPhase);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.True(EngineKernel.Instance.HasPendingPackageCleanup);
        Assert.Same(config, EngineKernel.Instance.Config);
        Assert.True(EngineKernel.Instance.Services.TryGetService<IPartialLoadService>(out _));
        Assert.NotNull(EngineKernel.Instance.GetSubsystem<RollbackProbeSubsystem>());
        Assert.Single(
            EngineKernel.Instance.GetSubsystem<PackageSubsystem>()!.GetAllPackages());
        Assert.Equal(
            new[] { "load:partial-retryable", "unload:partial-retryable:1" },
            TestPackageEvents.Events);

        EngineKernel.Instance.Shutdown();

        Assert.Equal(
            new[]
            {
                "load:partial-retryable",
                "unload:partial-retryable:1",
                "unload:partial-retryable:2"
            },
            TestPackageEvents.Events);
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.False(EngineKernel.Instance.HasPendingPackageCleanup);
        Assert.False(EngineKernel.Instance.Services.TryGetService<IPartialLoadService>(out _));
        Assert.Null(EngineKernel.Instance.GetSubsystem<RollbackProbeSubsystem>());
        Assert.Empty(EngineKernel.Instance.GetSubsystem<PackageSubsystem>()!.GetAllPackages());
        Assert.True(EngineKernel.Instance.GetShutdownOwnershipSnapshot().IsClean);
    }

    [Fact]
    public void PackageMountRollsBackManifestSubsystemsWhenLaterRegistrationFails()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.subsystem-rollback",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            },
            subsystems: new object[]
            {
                new
                {
                    @class = typeof(RollbackProbeSubsystem).FullName!,
                    phase = "Init",
                    priority = 0
                },
                new
                {
                    @class = "ArisenKernel.Tests.MissingSubsystem",
                    phase = "Init",
                    priority = 1
                }
            });
        var packageSubsystem = new PackageSubsystem();
        EngineKernel.Instance.RegisterSubsystem(packageSubsystem);

        var exception = Assert.Throws<TypeLoadException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { packagePath }
            }));

        Assert.Contains("MissingSubsystem", exception.Message);
        Assert.Empty(packageSubsystem.GetAllPackages());
        Assert.Null(EngineKernel.Instance.GetSubsystem<RollbackProbeSubsystem>());
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.Equal(new[] { "load:provider", "unload:provider" }, TestPackageEvents.Events);
    }

    [Fact]
    public void PackageMountRollsBackGraphWhenFinalRequiredServiceValidationFails()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.missing-requirement",
            typeof(MissingRequirementPackageEntry),
            services: new
            {
                requires = new object[] { typeof(IMissingRuntimeService).FullName! }
            });
        var packageSubsystem = new PackageSubsystem();
        EngineKernel.Instance.RegisterSubsystem(packageSubsystem);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { packagePath }
            }));

        Assert.Contains(typeof(IMissingRuntimeService).FullName!, exception.Message);
        Assert.Empty(packageSubsystem.GetAllPackages());
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.Equal(
            new[] { "load:missing-requirement", "unload:missing-requirement" },
            TestPackageEvents.Events);
    }

    [Fact]
    public void PackageEntryUnloadsAfterPackageOwnedPreInitSubsystemShutsDown()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.preinit-order",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            },
            subsystems: new object[]
            {
                new
                {
                    @class = typeof(PackageOwnedPreInitSubsystem).FullName!,
                    phase = "PreInit",
                    priority = 0
                }
            });

        EngineKernel.Instance.Initialize(new EngineConfig
        {
            PackageUrls = new List<string> { packagePath }
        });
        EngineKernel.Instance.Shutdown();

        Assert.Equal(
            new[]
            {
                "load:provider",
                "initialize:package-preinit",
                "shutdown:package-preinit",
                "unload:provider"
            },
            TestPackageEvents.Events);
    }

    [Fact]
    public void PackageMountReleasesCollectibleContextWhenGraphValidationFails()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string isolatedAssemblyName = $"IsolatedPackage.{Guid.NewGuid():N}.dll";
        string packagePath = workspace.AddPackage(
            "com.test.runtime.collectible-rollback",
            typeof(MissingRequirementPackageEntry),
            services: new
            {
                requires = new object[] { typeof(IMissingRuntimeService).FullName! }
            },
            entryAssembly: isolatedAssemblyName);
        string managedDirectory = Path.Combine(packagePath, "Managed");
        Directory.CreateDirectory(managedDirectory);
        File.Copy(
            typeof(PackageRuntimeSmokeTests).Assembly.Location,
            Path.Combine(managedDirectory, isolatedAssemblyName));
        var packageSubsystem = new PackageSubsystem();
        EngineKernel.Instance.RegisterSubsystem(packageSubsystem);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { packagePath }
            }));

        Assert.Contains(typeof(IMissingRuntimeService).FullName!, exception.Message);
        Assert.Empty(packageSubsystem.GetAllPackages());
        Assert.Equal(0, packageSubsystem.LoadedContextCount);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
    }

    [Fact]
    public void PackageMountReportsOriginalAndRollbackFailuresAfterCleaningRegistrations()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.rollback-failure",
            typeof(InvalidProviderThrowingUnloadEntry),
            services: new
            {
                provides = new object[]
                {
                    typeof(IRuntimeSmokeService).FullName!,
                    typeof(IMissingRuntimeService).FullName!
                }
            });
        var packageSubsystem = new PackageSubsystem();
        EngineKernel.Instance.RegisterSubsystem(packageSubsystem);

        var exception = Assert.Throws<AggregateException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { packagePath }
            }));
        IReadOnlyCollection<Exception> failures = exception.Flatten().InnerExceptions;

        Assert.Contains(failures, error =>
            error.Message.Contains(typeof(IMissingRuntimeService).FullName!, StringComparison.Ordinal));
        Assert.Contains(failures, error =>
            error.Message.Contains("injected rollback unload failure", StringComparison.Ordinal));
        Assert.Empty(packageSubsystem.GetAllPackages());
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.Null(EngineKernel.Instance.GetSubsystem<RollbackProbeSubsystem>());
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.Null(EngineKernel.Instance.Config);
    }

    [Fact]
    public void EngineInitializationReportsInitializationAndCleanupFailuresAfterShutdown()
    {
        EngineKernel.Instance.RegisterSubsystem(new InitializationFollowerSubsystem());
        EngineKernel.Instance.RegisterSubsystem(new ThrowingInitializeAndShutdownSubsystem());

        var exception = Assert.Throws<AggregateException>(() =>
            EngineKernel.Instance.Initialize(new EngineConfig()));
        IReadOnlyCollection<Exception> failures = exception.Flatten().InnerExceptions;

        Assert.Contains(failures, error =>
            error.Message.Contains("injected combined initialization failure", StringComparison.Ordinal));
        Assert.Contains(failures, error =>
            error.Message.Contains("injected combined shutdown failure", StringComparison.Ordinal));
        Assert.Equal(EnginePhase.Shutdown, EngineKernel.Instance.CurrentPhase);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
        Assert.Contains("shutdown:follower", TestPackageEvents.Events);

        var repeatedFailure = Assert.Throws<AggregateException>(() => EngineKernel.Instance.Shutdown());
        Assert.Same(exception, repeatedFailure);
    }

    [Fact]
    public void PackageMountRollsBackEarlierPackagesWhenManagedEntryResolutionFails()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddPackage(
            "com.test.runtime.provider",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            });
        string failingPath = workspace.AddRawPackageJson(
            "com.test.runtime.missing-entry",
            $$"""
            {
              "id": "com.test.runtime.missing-entry",
              "name": "Missing Entry Package",
              "version": "1.0.0",
              "type": "managed",
              "entry": {
                "assembly": "{{typeof(PackageRuntimeSmokeTests).Assembly.GetName().Name}}.dll",
                "class": "ArisenKernel.Tests.MissingPackageEntry"
              }
            }
            """);
        var packageSubsystem = new PackageSubsystem();
        EngineKernel.Instance.RegisterSubsystem(packageSubsystem);

        var exception = Assert.Throws<TypeLoadException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { providerPath, failingPath }
            }));

        Assert.Contains("MissingPackageEntry", exception.Message);
        Assert.Empty(packageSubsystem.GetAllPackages());
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.Equal(new[] { "load:provider", "unload:provider" }, TestPackageEvents.Events);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
    }

    [Fact]
    public void PackageMountFreesNativeRuntimeWithoutShutdownWhenInitFails()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddPackage(
            "com.test.runtime.native-init-failure",
            typeof(ProviderPackageEntry),
            services: new
            {
                provides = new object[] { typeof(IRuntimeSmokeService).FullName! }
            },
            nativeRuntimes: new Dictionary<string, object[]>
            {
                ["win-x64"] = new object[]
                {
                    new { path = "InitFailure.Native.dll", initExport = "Init", shutdownExport = "Shutdown" }
                }
            });
        var nativeApi = new FakeNativePackageRuntimeApi
        {
            FailInitPath = "InitFailure.Native.dll"
        };
        var packageSubsystem = new PackageSubsystem(nativeApi);
        EngineKernel.Instance.RegisterSubsystem(packageSubsystem);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = new List<string> { packagePath }
            }));

        Assert.Contains("injected native init failure", exception.Message);
        Assert.Empty(nativeApi.LiveHandles);
        Assert.Equal(
            new[]
            {
                "load:InitFailure.Native.dll",
                "init:InitFailure.Native.dll",
                "free:InitFailure.Native.dll"
            },
            nativeApi.Events);
        Assert.DoesNotContain("shutdown:InitFailure.Native.dll", nativeApi.Events);
        Assert.Equal(new[] { "load:provider", "unload:provider" }, TestPackageEvents.Events);
        Assert.False(EngineKernel.Instance.Services.TryGetService<IRuntimeSmokeService>(out _));
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
    }

    [Fact]
    public void PackageRequiringNewerEngineFailsBeforeEntryLoad()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string packagePath = workspace.AddRawPackageJson(
            "com.test.future",
            $$"""
            {
              "id": "com.test.future",
              "name": "Future Package",
              "version": "1.0.0",
              "type": "managed",
              "engine": {
                "minVersion": "99.0.0"
              },
              "entry": {
                "assembly": "{{typeof(PackageRuntimeSmokeTests).Assembly.GetName().Name}}.dll",
                "class": "{{typeof(ProviderPackageEntry).FullName}}"
              }
            }
            """);
        EngineKernel.Instance.RegisterSubsystem(new PackageSubsystem());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = [packagePath]
            }));

        Assert.Contains("requires engine version '99.0.0'", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("load:provider", TestPackageEvents.Events);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
    }

    [Fact]
    public void RuntimePackageGraphRejectsIncompatibleDependencyBeforeAnyEntryLoads()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string providerPath = workspace.AddRawPackageJson(
            "com.test.provider",
            $$"""
            {
              "id": "com.test.provider",
              "name": "Provider",
              "version": "1.0.0",
              "type": "managed",
              "entry": {
                "assembly": "{{typeof(PackageRuntimeSmokeTests).Assembly.GetName().Name}}.dll",
                "class": "{{typeof(ProviderPackageEntry).FullName}}"
              }
            }
            """);
        string consumerPath = workspace.AddRawPackageJson(
            "com.test.consumer",
            $$"""
            {
              "id": "com.test.consumer",
              "name": "Consumer",
              "version": "1.0.0",
              "type": "managed",
              "dependencies": {
                "com.test.provider": "^2.0.0"
              },
              "entry": {
                "assembly": "{{typeof(PackageRuntimeSmokeTests).Assembly.GetName().Name}}.dll",
                "class": "{{typeof(ConsumerPackageEntry).FullName}}"
              }
            }
            """);
        EngineKernel.Instance.RegisterSubsystem(new PackageSubsystem());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = [providerPath, consumerPath]
            }));

        Assert.Contains("com.test.consumer -> com.test.provider", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("^2.0.0", exception.Message, StringComparison.Ordinal);
        Assert.Empty(TestPackageEvents.Events);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
    }

    [Fact]
    public void RuntimePackageGraphRejectsDependencyCycleBeforeAnyEntryLoads()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        string leftPath = workspace.AddPackage(
            "com.test.left",
            typeof(ProviderPackageEntry),
            dependencies: new Dictionary<string, string>
            {
                ["com.test.right"] = "1.0.0"
            });
        string rightPath = workspace.AddPackage(
            "com.test.right",
            typeof(ProviderPackageEntry),
            dependencies: new Dictionary<string, string>
            {
                ["com.test.left"] = "1.0.0"
            });
        EngineKernel.Instance.RegisterSubsystem(new PackageSubsystem());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = [leftPath, rightPath]
            }));

        Assert.Contains(
            "com.test.left -> com.test.right -> com.test.left",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Empty(TestPackageEvents.Events);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
    }

    [Fact]
    public void RawWorkspaceRequirementRejectsSelectedPackageBeforeAnyEntryLoads()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        workspace.AddPackage("com.test.provider", typeof(ProviderPackageEntry));
        workspace.WriteProjectManifestWithVersion("^2.0.0", "com.test.provider");
        EnginePackageGraphResolution resolution = EngineBootstrapper.ResolvePackageGraph(
            workspace.Root,
            "Development");
        EngineKernel.Instance.RegisterSubsystem(new PackageSubsystem());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = resolution.PackageUrls.ToList(),
                PackageRequirements = resolution.PackageRequirements.ToList()
            }));

        Assert.Contains("workspace base Packages entry 0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("^2.0.0", exception.Message, StringComparison.Ordinal);
        Assert.Empty(TestPackageEvents.Events);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
    }

    [Fact]
    public void ResolvedPackageVersionMustMatchLoadedPackageMetadata()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        workspace.AddPackage("com.test.provider", typeof(ProviderPackageEntry));
        workspace.WriteProjectManifestWithVersion("*", "com.test.provider");
        string resolvedManifestPath = Path.Combine(workspace.Root, "resolved-version.json");
        File.WriteAllText(
            resolvedManifestPath,
            JsonSerializer.Serialize(
                new
                {
                    SchemaVersion = 2,
                    Profile = "Development",
                    ResolvedPackages = new[]
                    {
                        new
                        {
                            Id = "com.test.provider",
                            Version = "2.0.0",
                            Dependencies = new Dictionary<string, string>(),
                            Url = "file://com.test.provider/"
                        }
                    }
                },
                new JsonSerializerOptions { WriteIndented = true }));
        EnginePackageGraphResolution resolution = EngineBootstrapper.ResolvePackageGraph(
            workspace.Root,
            "Development",
            resolvedManifestPathOverride: resolvedManifestPath);
        EngineKernel.Instance.RegisterSubsystem(new PackageSubsystem());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            EngineKernel.Instance.MountPackageGraph(new EngineConfig
            {
                PackageUrls = resolution.PackageUrls.ToList(),
                PackageRequirements = resolution.PackageRequirements.ToList()
            }));

        Assert.Contains("resolved manifest 'resolved-version.json'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("2.0.0", exception.Message, StringComparison.Ordinal);
        Assert.Empty(TestPackageEvents.Events);
        Assert.False(EngineKernel.Instance.IsPackageGraphMounted);
    }

    [Fact]
    public void ResolvedManifestRejectsTamperedNativePayloadBeforeReturningPackageGraph()
    {
        using var workspace = RuntimePackageWorkspace.Create();
        workspace.AddRawPackageJson(
            "com.test.native",
            """
            {
              "id": "com.test.native",
              "name": "Native Package",
              "version": "1.0.0",
              "type": "native",
              "nativeRuntimes": {
                "win-x64": [
                  {
                    "path": "Native.Debug.dll",
                    "configurations": [ "Debug" ]
                  },
                  {
                    "path": "Native.Release.dll",
                    "configurations": [ "Release" ]
                  }
                ]
              }
            }
            """);
        workspace.WriteProjectManifest("com.test.native");
        string payloadPath = Path.Combine(workspace.Root, "Native.Debug.dll");
        File.WriteAllText(payloadPath, "GOOD");
        string hash;
        using (FileStream stream = File.OpenRead(payloadPath))
        {
            hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        string resolvedManifestPath = Path.Combine(workspace.Root, "manifest.resolved.json");
        File.WriteAllText(
            resolvedManifestPath,
            JsonSerializer.Serialize(
                new
                {
                    SchemaVersion = 2,
                    Profile = "Development",
                    Configuration = "Debug",
                    NativePayloadsFinalized = true,
                    NativePayloads = new[]
                    {
                        new
                        {
                            RuntimeIdentifier = "win-x64",
                            FileName = "Native.Debug.dll",
                            Size = 4,
                            Sha256 = hash,
                            Owners = new[] { "com.test.native" }
                        }
                    },
                    ResolvedPackages = new[]
                    {
                        new
                        {
                            Id = "com.test.native",
                            Version = "1.0.0",
                            Dependencies = new Dictionary<string, string>(),
                            NativeRuntimes = new Dictionary<string, object[]>
                            {
                                ["win-x64"] =
                                [
                                    new
                                    {
                                        path = "Native.Debug.dll",
                                        configurations = new[] { "Debug" }
                                    },
                                    new
                                    {
                                        path = "Native.Release.dll",
                                        configurations = new[] { "Release" }
                                    }
                                ]
                            },
                            Url = "file://com.test.native/"
                        }
                    }
                },
                new JsonSerializerOptions { WriteIndented = true }));

        EnginePackageGraphResolution valid = EngineBootstrapper.ResolvePackageGraph(
            workspace.Root,
            "Development",
            resolvedManifestPathOverride: resolvedManifestPath);
        Assert.Single(valid.PackageUrls);

        string sourceResolvedManifestPath = Path.Combine(
            workspace.Root,
            "manifest.source.resolved.json");
        using (JsonDocument runtimeManifest = JsonDocument.Parse(
                   File.ReadAllText(resolvedManifestPath)))
        {
            File.WriteAllText(
                sourceResolvedManifestPath,
                JsonSerializer.Serialize(
                    new
                    {
                        SchemaVersion = 2,
                        Profile = "Development",
                        NativePayloadsFinalized = false,
                        NativePayloads = Array.Empty<object>(),
                        ResolvedPackages = runtimeManifest.RootElement
                            .GetProperty("ResolvedPackages")
                            .Clone()
                    },
                    new JsonSerializerOptions { WriteIndented = true }));
        }

        EnginePackageGraphResolution buildStage =
            EngineBootstrapper.ResolveBuildStagePackageGraph(
                workspace.Root,
                "Development",
                sourceResolvedManifestPath,
                resolvedManifestPath);
        Assert.Single(buildStage.PackageUrls);

        File.WriteAllText(payloadPath, "EVIL");
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            EngineBootstrapper.ResolvePackageGraph(
                workspace.Root,
                "Development",
                allowResolvedManifestFallback: true,
                resolvedManifestPathOverride: resolvedManifestPath));

        Assert.Contains("SHA-256 mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SelectiveThrowingLogger : ILogger
    {
        private readonly HashSet<string> m_PendingFailureFragments;

        public SelectiveThrowingLogger(params string[] failureFragments)
        {
            m_PendingFailureFragments = new HashSet<string>(
                failureFragments,
                StringComparer.Ordinal);
        }

        public int ThrowCount { get; private set; }

        public void Log(string message) => Publish(message);
        public void LogFormat(string format, params object[] args) =>
            Publish(string.Format(format, args));
        public void Warning(string message) => Publish(message);
        public void WarningFormat(string format, params object[] args) =>
            Publish(string.Format(format, args));
        public void Error(string message) => Publish(message);
        public void ErrorFormat(string format, params object[] args) =>
            Publish(string.Format(format, args));
        public void Fatal(string message) => Publish(message);
        public void FatalFormat(string format, params object[] args) =>
            Publish(string.Format(format, args));

        public void Assert(bool condition, string message = "")
        {
            if (!condition) Publish(message);
        }

        public void AssertFormat(bool condition, string format, params object[] args)
        {
            if (!condition) Publish(string.Format(format, args));
        }

        private void Publish(string message)
        {
            string? matchedFragment = null;
            foreach (string fragment in m_PendingFailureFragments)
            {
                if (message.Contains(fragment, StringComparison.Ordinal))
                {
                    matchedFragment = fragment;
                    break;
                }
            }

            if (matchedFragment == null)
            {
                return;
            }

            m_PendingFailureFragments.Remove(matchedFragment);
            ThrowCount++;
            throw new InvalidOperationException(
                $"Injected logger failure for '{matchedFragment}'.");
        }
    }

    private sealed class MutatingShutdownLogger : ILogger
    {
        private bool m_AttemptedRegistration;

        public Exception? RegistrationFailure { get; private set; }

        public void Log(string message) => Publish(message);
        public void LogFormat(string format, params object[] args) =>
            Publish(string.Format(format, args));
        public void Warning(string message) => Publish(message);
        public void WarningFormat(string format, params object[] args) =>
            Publish(string.Format(format, args));
        public void Error(string message) => Publish(message);
        public void ErrorFormat(string format, params object[] args) =>
            Publish(string.Format(format, args));
        public void Fatal(string message) => Publish(message);
        public void FatalFormat(string format, params object[] args) =>
            Publish(string.Format(format, args));

        public void Assert(bool condition, string message = "")
        {
            if (!condition) Publish(message);
        }

        public void AssertFormat(bool condition, string format, params object[] args)
        {
            if (!condition) Publish(string.Format(format, args));
        }

        private void Publish(string message)
        {
            if (m_AttemptedRegistration ||
                !message.Contains("[EngineKernel] Shutdown baseline:", StringComparison.Ordinal))
            {
                return;
            }

            m_AttemptedRegistration = true;
            try
            {
                EngineKernel.Instance.RegisterKernelOwnedService<IPartialLoadService>(
                    new PartialLoadService());
            }
            catch (Exception error)
            {
                RegistrationFailure = error;
            }
        }
    }

    public void Dispose()
    {
        LifecycleFaultInjection.Reset();
        try
        {
            EngineKernel.Instance.Reset();
        }
        finally
        {
            KernelLog.InvalidateCache();
            TestPackageEvents.Reset();
        }
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

        public string Root => m_Root;

        public string AddPackage(
            string id,
            Type entryType,
            Dictionary<string, string>? dependencies = null,
            object? services = null,
            Dictionary<string, object[]>? nativeRuntimes = null,
            object[]? subsystems = null,
            string? entryAssembly = null)
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
                    assembly = entryAssembly ?? $"{typeof(PackageRuntimeSmokeTests).Assembly.GetName().Name}.dll",
                    @class = entryType.FullName
                }
            };

            if (dependencies is { Count: > 0 }) manifest["dependencies"] = dependencies;
            if (services != null) manifest["services"] = services;
            if (nativeRuntimes is { Count: > 0 }) manifest["nativeRuntimes"] = nativeRuntimes;
            if (subsystems is { Length: > 0 }) manifest["subsystems"] = subsystems;

            File.WriteAllText(Path.Combine(packageDir, "package.json"), JsonSerializer.Serialize(manifest, s_JsonOptions));
            return packageDir;
        }

        public string AddRawPackageJson(string id, string json)
        {
            string packageDir = Path.Combine(m_Root, id);
            Directory.CreateDirectory(packageDir);
            File.WriteAllText(Path.Combine(packageDir, "package.json"), json);
            return packageDir;
        }

        public void WriteProjectManifest(params string[] packageIds)
        {
            WriteProjectManifestWithVersion("1.0.0", packageIds);
        }

        public void WriteProjectManifestWithVersion(
            string version,
            params string[] packageIds)
        {
            var manifest = new
            {
                Name = "ArisenKernel Package-Only Host Test",
                EngineVersion = "Current",
                Packages = packageIds.Select(id => new
                {
                    Id = id,
                    Url = $"file://{id}",
                    Version = version
                })
            };

            File.WriteAllText(
                Path.Combine(m_Root, "manifest.json"),
                JsonSerializer.Serialize(manifest, s_JsonOptions));
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

    private sealed class CountingSmokeScenario : IRuntimeSmokeScenario
    {
        private readonly int m_ReadyAfterFrames;
        private int m_Frames;

        public CountingSmokeScenario(int readyAfterFrames)
        {
            m_ReadyAfterFrames = readyAfterFrames;
        }

        public string Name => "counting";
        public string OutputPath => "counting.json";
        public bool IsReadyForShutdown => m_Frames >= m_ReadyAfterFrames || FailureMessage != null;
        public bool IsComplete { get; private set; }
        public bool Succeeded => IsComplete && FailureMessage == null && ShutdownObserved;
        public string? FailureMessage { get; private set; }
        public bool ShutdownObserved { get; private set; }

        public void Start(uint initialFrameIndex)
        {
        }

        public void BeforeFrame(uint frameIndex)
        {
        }

        public void AfterFrame(uint frameIndex)
        {
            m_Frames++;
        }

        public void ReportFailure(string message)
        {
            FailureMessage ??= message;
        }

        public void AfterShutdown()
        {
            ShutdownObserved = EngineKernel.Instance.CurrentPhase == EnginePhase.Shutdown;
            IsComplete = true;
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

public interface IMissingRuntimeService
{
}

public interface IPartialLoadService
{
}

public sealed class PartialLoadService : IPartialLoadService
{
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

public sealed class ResetFromLoadPackageEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("load:reentrant-reset");
        EngineKernel.Instance.Reset();
    }

    public void OnUnload(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("unload:reentrant-reset");
    }
}

public sealed class NestedMountFromLoadPackageEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("load:reentrant-mount");
        EngineKernel.Instance.MountPackageGraph(new EngineConfig());
    }

    public void OnUnload(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("unload:reentrant-mount");
    }
}

public sealed class ReentrantPackageShutdownEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("load:reentrant-package-shutdown");
        services.RegisterService<IPartialLoadService>(new PartialLoadService());
    }

    public void OnUnload(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("unload:reentrant-package-shutdown");
        EngineKernel.Instance.GetSubsystem<PackageSubsystem>()!.Shutdown();
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

public sealed class InvalidProviderPackageEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("load:invalid-provider");
        services.RegisterService<IRuntimeSmokeService>(new RuntimeSmokeService());
        EngineKernel.Instance.RegisterSubsystem(new RollbackProbeSubsystem());
    }

    public void OnUnload(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("unload:invalid-provider");
    }
}

public sealed class InvalidProviderThrowingUnloadEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        services.RegisterService<IRuntimeSmokeService>(new RuntimeSmokeService());
        EngineKernel.Instance.RegisterSubsystem(new RollbackProbeSubsystem());
    }

    public void OnUnload(IServiceRegistry services)
    {
        throw new InvalidOperationException("injected rollback unload failure");
    }
}

public sealed class ThrowingLoadPackageEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("load:failing");
        services.RegisterService<IPartialLoadService>(new PartialLoadService());
        EngineKernel.Instance.RegisterSubsystem(new RollbackProbeSubsystem());
        LifecycleFaultInjection.ThrowIfArmed(LifecycleFaultStage.PackageLoad);
    }

    public void OnUnload(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("unload:failing");
    }
}

public sealed class BareThrowingLoadPackageEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("load:failing");
        LifecycleFaultInjection.ThrowIfArmed(LifecycleFaultStage.PackageLoad);
    }

    public void OnUnload(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("unload:failing");
    }
}

public sealed class ThrowingUnloadPackageEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("load:throwing-unload");
    }

    public void OnUnload(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("unload:throwing-unload");
        LifecycleFaultInjection.ThrowIfArmed(LifecycleFaultStage.PackageUnload);
    }
}

public sealed class RetryableOwnershipPackageEntry : IPackageEntry
{
    private static bool s_HasPendingOwnership;
    private static int s_UnloadAttemptCount;

    public bool HasPendingOwnership => s_HasPendingOwnership;

    public void OnLoad(IServiceRegistry services)
    {
        s_HasPendingOwnership = true;
        TestPackageEvents.Events.Add("load:retryable");
    }

    public void OnUnload(IServiceRegistry services)
    {
        s_UnloadAttemptCount++;
        TestPackageEvents.Events.Add($"unload:retryable:{s_UnloadAttemptCount}");
        if (s_UnloadAttemptCount >= 2)
        {
            s_HasPendingOwnership = false;
        }
    }

    public static void Reset()
    {
        s_HasPendingOwnership = false;
        s_UnloadAttemptCount = 0;
    }
}

public sealed class AlwaysPendingOwnershipPackageEntry : IPackageEntry
{
    public static bool AllowCleanup { get; set; }
    public static int UnloadAttemptCount { get; private set; }

    public bool HasPendingOwnership => !AllowCleanup;

    public void OnLoad(IServiceRegistry services)
    {
        AllowCleanup = false;
        TestPackageEvents.Events.Add("load:always-pending");
    }

    public void OnUnload(IServiceRegistry services)
    {
        UnloadAttemptCount++;
        TestPackageEvents.Events.Add($"unload:always-pending:{UnloadAttemptCount}");
    }

    public static void Reset()
    {
        AllowCleanup = false;
        UnloadAttemptCount = 0;
    }
}

public sealed class LeafPackageEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("load:leaf");
    }

    public void OnUnload(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("unload:leaf");
    }
}

public sealed class PartialLoadRetryableOwnershipPackageEntry : IPackageEntry
{
    private static bool s_HasPendingOwnership;
    private static int s_UnloadAttemptCount;

    public bool HasPendingOwnership => s_HasPendingOwnership;

    public void OnLoad(IServiceRegistry services)
    {
        s_HasPendingOwnership = true;
        TestPackageEvents.Events.Add("load:partial-retryable");
        services.RegisterService<IPartialLoadService>(new PartialLoadService());
        EngineKernel.Instance.RegisterSubsystem(new RollbackProbeSubsystem());
        throw new InvalidOperationException("injected partial load failure");
    }

    public void OnUnload(IServiceRegistry services)
    {
        s_UnloadAttemptCount++;
        TestPackageEvents.Events.Add($"unload:partial-retryable:{s_UnloadAttemptCount}");
        if (s_UnloadAttemptCount == 1)
        {
            throw new InvalidOperationException("injected partial rollback failure");
        }

        s_HasPendingOwnership = false;
    }

    public static void Reset()
    {
        s_HasPendingOwnership = false;
        s_UnloadAttemptCount = 0;
    }
}

public sealed class MissingRequirementPackageEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("load:missing-requirement");
    }

    public void OnUnload(IServiceRegistry services)
    {
        TestPackageEvents.Events.Add("unload:missing-requirement");
    }
}

public sealed class PackageOnlyHostPackageEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        EngineKernel.Instance.RegisterSubsystem(new CountingTickSubsystem());
        services.RegisterService<IApplicationHost>(new PackageOnlyTestHost());
    }

    public void OnUnload(IServiceRegistry services)
    {
    }
}

public sealed class PackageOnlyTestHost : IApplicationHost
{
    public bool RequiresEngineInitialization => false;

    public void Run(string[] args)
    {
        TestPackageEvents.Events.Add("run:package-only-host");
    }
}

internal static class TestPackageEvents
{
    public static List<string> Events { get; } = new();

    public static void Reset()
    {
        Events.Clear();
        CountingTickSubsystem.Reset();
        RetryableOwnershipPackageEntry.Reset();
        AlwaysPendingOwnershipPackageEntry.Reset();
        PartialLoadRetryableOwnershipPackageEntry.Reset();
    }
}

internal enum LifecycleFaultStage
{
    PackageLoad,
    PackageUnload,
    SubsystemInitialize,
    SubsystemShutdown
}

internal static class LifecycleFaultInjection
{
    private static readonly DeterministicFaultInjector<LifecycleFaultStage> s_Injector = new();

    public static void Arm(LifecycleFaultStage stage, string message)
    {
        s_Injector.Arm(stage, () => new InvalidOperationException(message));
    }

    public static void ThrowIfArmed(LifecycleFaultStage stage)
    {
        s_Injector.ThrowIfArmed(stage);
    }

    public static void EnsureFullyConsumed()
    {
        s_Injector.EnsureFullyConsumed();
    }

    public static void Reset()
    {
        s_Injector.Reset();
    }
}

public sealed class CountingTickSubsystem : ITickableSubsystem
{
    public static int TickCount { get; private set; }
    public static bool WasInitialized { get; private set; }
    public static bool WasShutdown { get; private set; }

    public int Priority => 0;
    public EnginePhase InitPhase => EnginePhase.Running;

    public static void Reset()
    {
        TickCount = 0;
        WasInitialized = false;
        WasShutdown = false;
    }

    public void Initialize()
    {
        WasInitialized = true;
    }

    public void Tick(float deltaTime)
    {
        TickCount++;
    }

    public void Shutdown()
    {
        WasShutdown = true;
    }

    public void Dispose()
    {
        Shutdown();
    }
}

public sealed class RollbackProbeSubsystem : IEngineSubsystem
{
    public int Priority => 0;
    public EnginePhase InitPhase => EnginePhase.Init;
    public void Initialize() { }
    public void Shutdown() { }
    public void Dispose() { }
}

public sealed class InitializationFollowerSubsystem : IEngineSubsystem
{
    public int Priority => 0;
    public EnginePhase InitPhase => EnginePhase.PreInit;

    public void Initialize()
    {
        TestPackageEvents.Events.Add("initialize:follower");
    }

    public void Shutdown()
    {
        TestPackageEvents.Events.Add("shutdown:follower");
    }

    public void Dispose() { }
}

public sealed class ThrowingInitializeSubsystem : IEngineSubsystem
{
    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.PreInit;

    public void Initialize()
    {
        TestPackageEvents.Events.Add("initialize:throwing");
        LifecycleFaultInjection.ThrowIfArmed(LifecycleFaultStage.SubsystemInitialize);
    }

    public void Shutdown()
    {
        TestPackageEvents.Events.Add("shutdown:throwing-initialize");
    }

    public void Dispose() { }
}

public sealed class ThrowingInitializeAndShutdownSubsystem : IEngineSubsystem
{
    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.PreInit;

    public void Initialize()
    {
        throw new InvalidOperationException("injected combined initialization failure");
    }

    public void Shutdown()
    {
        throw new InvalidOperationException("injected combined shutdown failure");
    }

    public void Dispose() { }
}

public sealed class ShutdownFollowerSubsystem : IEngineSubsystem
{
    public int Priority => 0;
    public EnginePhase InitPhase => EnginePhase.Running;
    public void Initialize() { }

    public void Shutdown()
    {
        TestPackageEvents.Events.Add("shutdown:following");
    }

    public void Dispose() { }
}

public sealed class ShutdownFromInitializeSubsystem : IEngineSubsystem
{
    private readonly EngineKernel m_Kernel;

    public ShutdownFromInitializeSubsystem(EngineKernel kernel)
    {
        m_Kernel = kernel;
    }

    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Running;

    public void Initialize()
    {
        TestPackageEvents.Events.Add("initialize:reentrant-shutdown");
        m_Kernel.Shutdown();
    }

    public void Shutdown()
    {
        TestPackageEvents.Events.Add("shutdown:reentrant-shutdown");
    }

    public void Dispose() { }
}

public sealed class ResetFromInitializeSubsystem : IEngineSubsystem
{
    private readonly EngineKernel m_Kernel;

    public ResetFromInitializeSubsystem(EngineKernel kernel)
    {
        m_Kernel = kernel;
    }

    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Running;

    public void Initialize()
    {
        TestPackageEvents.Events.Add("initialize:reentrant-reset");
        m_Kernel.Reset();
    }

    public void Shutdown()
    {
        TestPackageEvents.Events.Add("shutdown:reentrant-reset-initialize");
    }

    public void Dispose() { }
}

public sealed class RunFromShutdownSubsystem : IEngineSubsystem
{
    private readonly EngineKernel m_Kernel;

    public RunFromShutdownSubsystem(EngineKernel kernel)
    {
        m_Kernel = kernel;
    }

    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Running;
    public void Initialize() { }

    public void Shutdown()
    {
        TestPackageEvents.Events.Add("shutdown:reentrant-run");
        m_Kernel.Run();
    }

    public void Dispose() { }
}

public sealed class BlockingFailedInitializeSubsystem : IEngineSubsystem
{
    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Running;
    public ManualResetEventSlim ShutdownEntered { get; } = new(false);
    public ManualResetEventSlim AllowShutdown { get; } = new(false);

    public void Initialize()
    {
        throw new InvalidOperationException("injected blocking initialization failure");
    }

    public void Shutdown()
    {
        ShutdownEntered.Set();
        AllowShutdown.Wait();
    }

    public void Dispose()
    {
        ShutdownEntered.Dispose();
        AllowShutdown.Dispose();
    }
}

public sealed class ResetFromTickSubsystem : ITickableSubsystem
{
    private readonly EngineKernel m_Kernel;

    public ResetFromTickSubsystem(EngineKernel kernel)
    {
        m_Kernel = kernel;
    }

    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Running;
    public int TickCount { get; private set; }
    public Exception? ResetFailure { get; private set; }
    public bool WasShutdown { get; private set; }
    public void Initialize() { }

    public void Tick(float deltaTime)
    {
        TickCount++;
        try
        {
            m_Kernel.Reset();
        }
        catch (Exception error)
        {
            ResetFailure = error;
        }
    }

    public void Shutdown()
    {
        WasShutdown = true;
    }

    public void Dispose() { }
}

public sealed class BlockingShutdownSubsystem : IEngineSubsystem, IDisposable
{
    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Running;
    public ManualResetEventSlim ShutdownEntered { get; } = new(false);
    public ManualResetEventSlim AllowShutdown { get; } = new(false);

    public void Initialize() { }

    public void Shutdown()
    {
        ShutdownEntered.Set();
        AllowShutdown.Wait();
    }

    public void Dispose()
    {
        ShutdownEntered.Dispose();
        AllowShutdown.Dispose();
    }
}

public sealed class BlockingRegistrationSubsystem : IEngineSubsystem, IDisposable
{
    private int m_ShouldBlock = 1;

    public int Priority
    {
        get
        {
            if (Interlocked.Exchange(ref m_ShouldBlock, 0) != 0)
            {
                RegistrationEntered.Set();
                AllowRegistration.Wait();
            }

            return 10;
        }
    }

    public EnginePhase InitPhase => EnginePhase.Running;
    public ManualResetEventSlim RegistrationEntered { get; } = new(false);
    public ManualResetEventSlim AllowRegistration { get; } = new(false);

    public void Initialize() { }
    public void Shutdown() { }

    public void Dispose()
    {
        RegistrationEntered.Dispose();
        AllowRegistration.Dispose();
    }
}

public sealed class ThrowingValueEqualitySubsystem : IEngineSubsystem
{
    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Running;
    public void Initialize() { }
    public void Shutdown() { }
    public void Dispose() { }
    public override int GetHashCode() =>
        throw new InvalidOperationException("Subsystem value equality must not be used.");
    public override bool Equals(object? obj) =>
        throw new InvalidOperationException("Subsystem value equality must not be used.");
}

public sealed class ReentrantResetShutdownSubsystem : IEngineSubsystem
{
    private readonly EngineKernel m_Kernel;

    public ReentrantResetShutdownSubsystem(EngineKernel kernel)
    {
        m_Kernel = kernel;
    }

    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Running;
    public void Initialize() { }

    public void Shutdown()
    {
        TestPackageEvents.Events.Add("shutdown:reentrant-reset");
        m_Kernel.Reset();
    }

    public void Dispose() { }
}

public sealed class ReentrantInitializeShutdownSubsystem : IEngineSubsystem
{
    private readonly EngineKernel m_Kernel;
    private readonly EngineConfig m_Config;

    public ReentrantInitializeShutdownSubsystem(EngineKernel kernel, EngineConfig config)
    {
        m_Kernel = kernel;
        m_Config = config;
    }

    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Running;
    public void Initialize() { }

    public void Shutdown()
    {
        TestPackageEvents.Events.Add("shutdown:reentrant-initialize");
        m_Kernel.Initialize(m_Config);
    }

    public void Dispose() { }
}

public sealed class ThrowingShutdownSubsystem : IEngineSubsystem
{
    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Running;
    public void Initialize() { }

    public void Shutdown()
    {
        TestPackageEvents.Events.Add("shutdown:throwing");
        LifecycleFaultInjection.ThrowIfArmed(LifecycleFaultStage.SubsystemShutdown);
    }

    public void Dispose() { }
}

public sealed class PackageOwnedPreInitSubsystem : IEngineSubsystem
{
    public int Priority => 0;
    public EnginePhase InitPhase => EnginePhase.PreInit;

    public void Initialize()
    {
        TestPackageEvents.Events.Add("initialize:package-preinit");
    }

    public void Shutdown()
    {
        TestPackageEvents.Events.Add("shutdown:package-preinit");
    }

    public void Dispose() { }
}

internal sealed class FakeNativePackageRuntimeApi : INativePackageRuntimeApi
{
    private readonly Dictionary<IntPtr, string> m_LiveHandles = new();
    private int m_LoadAttempt;
    private long m_NextHandle;

    public int FailLoadAttempt { get; init; }
    public string? FailInitPath { get; init; }
    public string? FailShutdownPath { get; init; }
    public List<string> Events { get; } = new();
    public IReadOnlyCollection<IntPtr> LiveHandles => m_LiveHandles.Keys;

    public IntPtr Load(string packageId, string runtimePath)
    {
        m_LoadAttempt++;
        Events.Add($"load:{runtimePath}");
        if (m_LoadAttempt == FailLoadAttempt)
        {
            throw new InvalidOperationException("injected native load failure");
        }

        var handle = new IntPtr(++m_NextHandle);
        m_LiveHandles.Add(handle, runtimePath);
        return handle;
    }

    public int InvokeLifecycle(
        string packageId,
        IntPtr libraryHandle,
        string runtimePath,
        string exportName,
        string phase)
    {
        Assert.True(m_LiveHandles.ContainsKey(libraryHandle));
        Events.Add($"{phase}:{runtimePath}");
        if (phase == "init" &&
            string.Equals(runtimePath, FailInitPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("injected native init failure");
        }

        if (phase == "shutdown" &&
            string.Equals(runtimePath, FailShutdownPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("injected native shutdown failure");
        }

        return 0;
    }

    public void Free(IntPtr libraryHandle)
    {
        Assert.True(m_LiveHandles.Remove(libraryHandle, out string? runtimePath));
        Events.Add($"free:{runtimePath}");
    }
}
