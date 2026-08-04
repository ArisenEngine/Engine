using ArisenEngine.Core.Assets;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RuntimeAssetResidencyTests : IDisposable
{
    private const string PackageId = "com.arisen.test";
    private static readonly Guid s_WorldGuid =
        Guid.Parse("84000000-0000-0000-0000-000000000001");
    private readonly string m_Root;
    private readonly TestAssetDatabase m_Database;

    public RuntimeAssetResidencyTests()
    {
        m_Root = Path.Combine(
            Path.GetTempPath(),
            "ArisenRuntimeAssetResidencyTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(m_Root);
        m_Database = new TestAssetDatabase(
            AssetSourceAccessMode.Diagnostic,
            Path.Combine(m_Root, "Cooked"));
    }

    [Fact]
    public void PreparedProviderRegistrationStatusUsesExactInstanceIdentity()
    {
        var registered = new FakePreparedProvider("Mesh", gpuBytes: 64);
        var collision = new FakePreparedProvider("Mesh", gpuBytes: 64);
        using var residency = CreateResidency(maxInactiveResources: 0);

        residency.RegisterPreparedProvider(registered);

        Assert.True(residency.IsPreparedProviderRegistered(registered));
        Assert.False(residency.IsPreparedProviderRegistered(collision));
        Assert.Throws<InvalidOperationException>(() =>
            residency.RegisterPreparedProvider(collision));
        Assert.True(residency.IsPreparedProviderRegistered(registered));
        Assert.False(residency.IsPreparedProviderRegistered(collision));

        Assert.True(residency.UnregisterPreparedProvider(registered.ProviderId));
        Assert.False(residency.IsPreparedProviderRegistered(registered));
    }

    [Fact]
    public void SharedOwnersRetainOneCpuAndGpuResourceUntilFinalRelease()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 256);
        m_Database.UseReadOnlyRuntime();
        var provider = new FakePreparedProvider("Mesh", gpuBytes: 1024);
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        CookedSceneDependency[] dependencies = [Dependency(meshGuid, "Mesh", required: true)];

        using RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1), dependencies, pinned: false);
        using RuntimeAssetResidencyLease second = residency.AcquireSceneDependencies(
            CellOwner(2, generation: 1), dependencies, pinned: false);
        residency.ProcessAtFrameBoundary();

        Assert.Equal(RuntimePreparedAssetState.Ready, first.State);
        Assert.Equal(RuntimePreparedAssetState.Ready, second.State);
        Assert.Equal(1, provider.PrepareCount);
        Assert.Single(m_Database.GetLoadedCookedAssetDiagnostics());
        Assert.Equal(2, residency.GetResources().Single().OwnerCount);

        first.Dispose();
        residency.ProcessAtFrameBoundary();
        Assert.Single(residency.GetResources());
        Assert.Equal(1, residency.GetResources().Single().OwnerCount);
        Assert.Equal(0, provider.ReleaseCount);

        second.Dispose();
        residency.ProcessAtFrameBoundary();
        Assert.Empty(residency.GetResources());
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
        Assert.Equal(1, provider.ReleaseCount);
    }

    [Fact]
    public void SetupBudgetKeepsOwnerWaitingUntilEveryRequiredResourceIsReady()
    {
        Guid firstGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 128);
        Guid secondGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 128);
        m_Database.UseReadOnlyRuntime();
        var provider = new FakePreparedProvider("Mesh", gpuBytes: 512);
        using var residency = CreateResidency(maxSetupsPerFrame: 1, maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(firstGuid, "Mesh", true), Dependency(secondGuid, "Mesh", true)],
            pinned: false);

        residency.ProcessAtFrameBoundary();
        Assert.Equal(RuntimePreparedAssetState.Waiting, lease.State);
        Assert.Equal(1, residency.GetMetrics().WaitingAssetCount);
        Assert.Equal(1, residency.GetMetrics().ReadyAssetCount);

        residency.ProcessAtFrameBoundary();
        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
        Assert.Equal(2, provider.PrepareCount);
    }

    [Fact]
    public async Task ProviderInvalidationRejectsAnInFlightStaleReadyResult()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 128);
        m_Database.UseReadOnlyRuntime();
        using var provider = new BlockingPreparedProvider("Mesh", gpuBytes: 512);
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);

        Task process = Task.Run(residency.ProcessAtFrameBoundary);
        Task<bool>? invalidation = null;
        try
        {
            Assert.True(
                provider.PrepareEntered.Wait(TimeSpan.FromSeconds(10)),
                "The provider did not enter the in-flight setup call.");

            invalidation = Task.Run(() => residency.InvalidatePreparedProvider(
                provider.ProviderId,
                "Prepared mesh resources were invalidated for the test."));
            Assert.True(SpinWait.SpinUntil(
                () => residency.GetResources().Single().Diagnostic.Contains(
                    "invalidated",
                    StringComparison.OrdinalIgnoreCase),
                TimeSpan.FromSeconds(10)));
            Assert.False(invalidation.IsCompleted);
            Assert.False(provider.ReleaseWhilePrepareIsInFlight);
        }
        finally
        {
            provider.AllowPrepare.Set();
            Task[] completions = invalidation == null
                ? [process]
                : [process, invalidation];
            await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.True(await invalidation!);

        RuntimeAssetResidencySnapshot stale = Assert.Single(residency.GetResources());
        Assert.Equal(RuntimePreparedAssetState.Waiting, stale.PreparedState);
        Assert.Equal(string.Empty, stale.ProviderId);
        Assert.Contains("invalidated", stale.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, provider.PreparedResourceCount);

        residency.ProcessAtFrameBoundary();

        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
        Assert.Equal(2, provider.PrepareCount);
        Assert.Equal(1, provider.ReleaseCount);
    }

    [Fact]
    public async Task ProviderUnregisterDefersInFlightReleaseUntilPrepareCompletes()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 128);
        m_Database.UseReadOnlyRuntime();
        using var provider = new BlockingPreparedProvider("Mesh", gpuBytes: 512);
        var replacement = new FakePreparedProvider("Mesh", gpuBytes: 256);
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);

        Task process = Task.Run(residency.ProcessAtFrameBoundary);
        Task<bool>? unregister = null;
        try
        {
            Assert.True(
                provider.PrepareEntered.Wait(TimeSpan.FromSeconds(10)),
                "The provider did not enter the in-flight setup call.");

            unregister = Task.Run(() =>
                residency.UnregisterPreparedProvider(provider.ProviderId));
            Assert.True(SpinWait.SpinUntil(
                () => residency.GetResources().Single().Diagnostic.Contains(
                    "unregistered",
                    StringComparison.OrdinalIgnoreCase),
                TimeSpan.FromSeconds(10)));
            Assert.False(unregister.IsCompleted);
            Assert.False(provider.ReleaseWhilePrepareIsInFlight);
            RuntimeAssetResidencySnapshot invalidated = Assert.Single(residency.GetResources());
            Assert.Equal(RuntimePreparedAssetState.Waiting, invalidated.PreparedState);
            Assert.Equal(string.Empty, invalidated.ProviderId);
        }
        finally
        {
            provider.AllowPrepare.Set();
            Task[] completions = unregister == null
                ? [process]
                : [process, unregister];
            await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.True(await unregister!);
        residency.RegisterPreparedProvider(replacement);
        residency.ProcessAtFrameBoundary();

        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
        Assert.Equal(1, provider.ReleaseCount);
        Assert.Equal(1, replacement.PrepareCount);
    }

    [Fact]
    public async Task DisposeDrainsInFlightProviderCallsBeforeReleasingOwnership()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 128);
        m_Database.UseReadOnlyRuntime();
        using var provider = new BlockingPreparedProvider("Mesh", gpuBytes: 512);
        var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);

        Task process = Task.Run(residency.ProcessAtFrameBoundary);
        Task? dispose = null;
        try
        {
            Assert.True(
                provider.PrepareEntered.Wait(TimeSpan.FromSeconds(10)),
                "The provider did not enter the in-flight setup call.");

            dispose = Task.Run(residency.Dispose);
            Assert.True(SpinWait.SpinUntil(
                () => residency.IsDisposed,
                TimeSpan.FromSeconds(10)));
            Assert.False(dispose.IsCompleted);
            Assert.False(provider.ReleaseWhilePrepareIsInFlight);
        }
        finally
        {
            provider.AllowPrepare.Set();
            Task[] completions = dispose == null
                ? [process]
                : [process, dispose];
            await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(1, provider.ReleaseCount);
        Assert.Equal(0, provider.PreparedResourceCount);
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public async Task ProviderUnregisterWaitsForEvictionReleaseAndCookedHandleCleanup()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 128);
        m_Database.UseReadOnlyRuntime();
        using var provider = new BlockingReleasePreparedProvider("Mesh", gpuBytes: 512);
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);
        residency.ProcessAtFrameBoundary();
        lease.Dispose();

        Task eviction = Task.Run(residency.ProcessAtFrameBoundary);
        Task<bool>? unregister = null;
        using var unregisterStarted = new ManualResetEventSlim(false);
        try
        {
            Assert.True(
                provider.ReleaseEntered.Wait(TimeSpan.FromSeconds(10)),
                "Eviction did not enter the provider release callback.");
            unregister = Task.Run(() =>
            {
                unregisterStarted.Set();
                return residency.UnregisterPreparedProvider(provider.ProviderId);
            });
            Assert.True(unregisterStarted.Wait(TimeSpan.FromSeconds(10)));
            Assert.False(unregister.IsCompleted);
            Assert.True(provider.ReleaseIsInFlight);
            Assert.Single(m_Database.GetLoadedCookedAssetDiagnostics());
        }
        finally
        {
            provider.AllowRelease.Set();
            Task[] completions = unregister == null
                ? [eviction]
                : [eviction, unregister];
            await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.True(await unregister!);
        Assert.Equal(1, provider.ReleaseCount);
        Assert.False(provider.ReleaseAfterDispose);
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public async Task PrepareCallbackLifecycleReentryRejectsWithoutDeadlock()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        var provider = new FakePreparedProvider("Mesh", gpuBytes: 64);
        using var residency = CreateResidency(maxInactiveResources: 0);
        Exception? rejection = null;
        provider.PrepareCallback = _ => rejection = Record.Exception(() =>
        {
            residency.InvalidatePreparedProvider(
                provider.ProviderId,
                "Prepare callback attempted recursive invalidation.");
        });
        residency.RegisterPreparedProvider(provider);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);

        await Task.Run(residency.ProcessAtFrameBoundary)
            .WaitAsync(TimeSpan.FromSeconds(10));

        InvalidOperationException failure = Assert.IsType<InvalidOperationException>(rejection);
        Assert.Contains("Prepare callback", failure.Message, StringComparison.Ordinal);
        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
        Assert.Equal(1, provider.PrepareCount);
    }

    [Fact]
    public async Task ReleaseCallbackLifecycleRecursionRejectsWithoutDeadlock()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        var provider = new FakePreparedProvider("Mesh", gpuBytes: 64);
        using var residency = CreateResidency(maxInactiveResources: 0);
        Exception? rejection = null;
        provider.ReleaseCallback = _ => rejection = Record.Exception(() =>
        {
            residency.UnregisterPreparedProvider(provider.ProviderId);
        });
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);
        residency.ProcessAtFrameBoundary();
        lease.Dispose();

        await Task.Run(residency.ProcessAtFrameBoundary)
            .WaitAsync(TimeSpan.FromSeconds(10));

        InvalidOperationException failure = Assert.IsType<InvalidOperationException>(rejection);
        Assert.Contains("lifecycle callback", failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, provider.ReleaseCount);
        Assert.Empty(residency.GetResources());
    }

    [Fact]
    public async Task GetMetricsCannotOverlapProviderRelease()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var provider = new BlockingReleasePreparedProvider("Mesh", gpuBytes: 64);
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);
        residency.ProcessAtFrameBoundary();
        lease.Dispose();

        Task eviction = Task.Run(residency.ProcessAtFrameBoundary);
        Task<RuntimeAssetResidencyMetrics>? metrics = null;
        using var metricsStarted = new ManualResetEventSlim(false);
        try
        {
            Assert.True(
                provider.ReleaseEntered.Wait(TimeSpan.FromSeconds(10)),
                "Eviction did not enter the provider release callback.");
            metrics = Task.Run(() =>
            {
                metricsStarted.Set();
                return residency.GetMetrics();
            });
            Assert.True(metricsStarted.Wait(TimeSpan.FromSeconds(10)));
            Assert.False(metrics.IsCompleted);
            Assert.False(provider.MetricsWhileReleaseIsInFlight);
        }
        finally
        {
            provider.AllowRelease.Set();
            Task[] completions = metrics == null
                ? [eviction]
                : [eviction, metrics];
            await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(10));
        }

        _ = await metrics!;
        Assert.False(provider.MetricsWhileReleaseIsInFlight);
        Assert.Equal(1, provider.ReleaseCount);
    }

    [Fact]
    public async Task GetMetricsDrainsInFlightPrepareBeforeSamplingProvider()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var provider = new BlockingPreparedProvider("Mesh", gpuBytes: 64);
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);

        Task process = Task.Run(residency.ProcessAtFrameBoundary);
        Task<RuntimeAssetResidencyMetrics>? metrics = null;
        using var metricsStarted = new ManualResetEventSlim(false);
        try
        {
            Assert.True(
                provider.PrepareEntered.Wait(TimeSpan.FromSeconds(10)),
                "The provider did not enter the in-flight setup call.");
            metrics = Task.Run(() =>
            {
                metricsStarted.Set();
                return residency.GetMetrics();
            });
            Assert.True(metricsStarted.Wait(TimeSpan.FromSeconds(10)));
            Assert.False(provider.MetricsEntered.IsSet);
            Assert.False(metrics.IsCompleted);
            Assert.False(provider.MetricsWhilePrepareIsInFlight);
        }
        finally
        {
            provider.AllowPrepare.Set();
            Task[] completions = metrics == null
                ? [process]
                : [process, metrics];
            await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(10));
        }

        _ = await metrics!;
        Assert.True(provider.MetricsEntered.IsSet);
        Assert.False(provider.MetricsWhilePrepareIsInFlight);
        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
    }

    [Fact]
    public async Task ConcurrentFrameBoundaryEntryIsRejectedBeforeProviderReleaseCanOverlapPrepare()
    {
        Guid inactiveGuid = Guid.Parse("01000000-0000-0000-0000-000000000201");
        Guid activeGuid = Guid.Parse("f1000000-0000-0000-0000-000000000201");
        AddCookedAsset(inactiveGuid, "Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        AddCookedAsset(activeGuid, "Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var provider = new BlockingPreparedProvider("Mesh", gpuBytes: 64)
        {
            BlockOnPrepareCount = 2
        };
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease inactive = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(inactiveGuid, "Mesh", required: true)],
            pinned: false);
        residency.ProcessAtFrameBoundary();
        inactive.Dispose();
        using RuntimeAssetResidencyLease active = residency.AcquireSceneDependencies(
            CellOwner(2, generation: 1),
            [Dependency(activeGuid, "Mesh", required: true)],
            pinned: false);

        Task first = Task.Run(residency.ProcessAtFrameBoundary);
        try
        {
            Assert.True(provider.PrepareEntered.Wait(TimeSpan.FromSeconds(10)));

            InvalidOperationException rejection = Assert.Throws<InvalidOperationException>(
                residency.ProcessAtFrameBoundary);

            Assert.Contains("already active", rejection.Message);
            Assert.False(provider.ReleaseWhilePrepareIsInFlight);
            Assert.Equal(0, provider.ReleaseCount);
        }
        finally
        {
            provider.AllowPrepare.Set();
        }

        await first.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(provider.ReleaseWhilePrepareIsInFlight);
        Assert.Equal(1, provider.ReleaseCount);
        Assert.Equal(RuntimePreparedAssetState.Ready, active.State);
    }

    [Fact]
    public async Task StalePrepareCleanupReleasesPreparedResultExactlyOnce()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var provider = new BlockingPreparedProvider("Mesh", gpuBytes: 64);
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);

        Task process = Task.Run(residency.ProcessAtFrameBoundary);
        try
        {
            Assert.True(
                provider.PrepareEntered.Wait(TimeSpan.FromSeconds(10)),
                "The provider did not enter the in-flight setup call.");
            lease.Dispose();
        }
        finally
        {
            provider.AllowPrepare.Set();
        }

        await process.WaitAsync(TimeSpan.FromSeconds(10));
        residency.ProcessAtFrameBoundary();

        Assert.Equal(1, provider.ReleaseAttemptCount);
        Assert.Equal(1, provider.ReleaseCount);
        Assert.Equal(0, provider.PreparedResourceCount);
        Assert.Empty(residency.GetResources());
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public async Task FailedStalePrepareCleanupRetriesAtNextFrameBoundary()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var provider = new BlockingPreparedProvider("Mesh", gpuBytes: 64)
        {
            FailFirstRelease = true
        };
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);

        Task process = Task.Run(residency.ProcessAtFrameBoundary);
        try
        {
            Assert.True(
                provider.PrepareEntered.Wait(TimeSpan.FromSeconds(10)),
                "The provider did not enter the in-flight setup call.");
            lease.Dispose();
        }
        finally
        {
            provider.AllowPrepare.Set();
        }

        AggregateException failure = await Assert.ThrowsAsync<AggregateException>(() =>
            process.WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Single(failure.InnerExceptions);
        Assert.Equal(1, provider.ReleaseAttemptCount);
        Assert.Equal(0, provider.ReleaseCount);
        Assert.Equal(1, provider.PreparedResourceCount);
        Assert.Single(residency.GetResources());
        Assert.Single(m_Database.GetLoadedCookedAssetDiagnostics());

        residency.ProcessAtFrameBoundary();

        Assert.Equal(2, provider.ReleaseAttemptCount);
        Assert.Equal(1, provider.ReleaseCount);
        Assert.Equal(0, provider.PreparedResourceCount);
        Assert.Empty(residency.GetResources());
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public async Task StaleReleaseAndMetricsFailuresRetainThePreparedGpuEstimate()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var provider = new BlockingPreparedProvider("Mesh", gpuBytes: 64)
        {
            FailFirstRelease = true,
            FailMetrics = true
        };
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);

        Task process = Task.Run(residency.ProcessAtFrameBoundary);
        try
        {
            Assert.True(provider.PrepareEntered.Wait(TimeSpan.FromSeconds(10)));
            lease.Dispose();
        }
        finally
        {
            provider.AllowPrepare.Set();
        }

        AggregateException failure = await Assert.ThrowsAsync<AggregateException>(() =>
            process.WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Equal(2, failure.InnerExceptions.Count);
        RuntimeAssetResidencySnapshot retained = Assert.Single(residency.GetResources());
        Assert.Equal(RuntimePreparedAssetState.Waiting, retained.PreparedState);
        Assert.Equal(64, retained.EstimatedGpuBytes);
        Assert.Equal(provider.ProviderId, retained.ProviderId);
        Assert.Equal(1, provider.ReleaseAttemptCount);
        Assert.Equal(1, provider.PreparedResourceCount);

        provider.FailMetrics = false;
        Assert.Equal(64, residency.GetMetrics().PreparedGpuBytes);

        residency.ProcessAtFrameBoundary();

        Assert.Equal(2, provider.ReleaseAttemptCount);
        Assert.Equal(1, provider.ReleaseCount);
        Assert.Equal(0, residency.GetMetrics().PreparedGpuBytes);
        Assert.Empty(residency.GetResources());
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public async Task OwnerReleaseWaitsUntilPreparedPublicationCommitsAndIsReleased()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var residency = CreateResidency(maxInactiveResources: 0);
        using var provider = new BlockingPublicationPreparedProvider(residency);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);

        Task process = Task.Run(residency.ProcessAtFrameBoundary);
        Task? release = null;
        using var releaseStarted = new ManualResetEventSlim(false);
        try
        {
            Assert.True(provider.PublicationEntered.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(provider.SnapshotPublished);
            release = Task.Run(() =>
            {
                releaseStarted.Set();
                lease.Dispose();
            });
            Assert.True(releaseStarted.Wait(TimeSpan.FromSeconds(10)));
            Assert.False(release.IsCompleted);
            Assert.True(provider.SnapshotPublished);
            Assert.Equal(1, Assert.Single(residency.GetResources()).OwnerCount);
        }
        finally
        {
            provider.AllowPublicationReturn.Set();
            Task[] completions = release == null
                ? [process]
                : [process, release];
            await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(10));
        }

        residency.ProcessAtFrameBoundary();

        Assert.False(provider.SnapshotPublished);
        Assert.Equal(1, provider.ReleaseCount);
        Assert.Empty(residency.GetResources());
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public void OwnerReleaseRejectedFromPreparedPublicationCanBeRetried()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var residency = CreateResidency(maxInactiveResources: 0);
        using var provider = new BlockingPublicationPreparedProvider(residency);
        residency.RegisterPreparedProvider(provider);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);
        Exception? rejection = null;
        provider.PublicationAction = () => rejection = Record.Exception(lease.Dispose);
        provider.AllowPublicationReturn.Set();

        residency.ProcessAtFrameBoundary();

        InvalidOperationException failure = Assert.IsType<InvalidOperationException>(rejection);
        Assert.Equal(
            "Runtime asset residency ownership cannot mutate reentrantly from a prepared " +
            "publication callback.",
            failure.Message);
        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
        Assert.Equal(1, Assert.Single(residency.GetResources()).OwnerCount);

        lease.Dispose();
        residency.ProcessAtFrameBoundary();

        Assert.Equal(1, provider.ReleaseCount);
        Assert.Empty(residency.GetResources());
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public async Task OwnerAttachmentWaitsUntilPreparedPublicationCommits()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var residency = CreateResidency(maxInactiveResources: 1);
        using var provider = new BlockingPublicationPreparedProvider(residency);
        residency.RegisterPreparedProvider(provider);
        using RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);

        Task process = Task.Run(residency.ProcessAtFrameBoundary);
        Task<RuntimeAssetResidencyLease>? acquire = null;
        using var acquireStarted = new ManualResetEventSlim(false);
        try
        {
            Assert.True(provider.PublicationEntered.Wait(TimeSpan.FromSeconds(10)));
            acquire = Task.Run(() =>
            {
                acquireStarted.Set();
                return residency.AcquireSceneDependencies(
                    CellOwner(2, generation: 1),
                    [Dependency(meshGuid, "Mesh", required: true)],
                    pinned: false);
            });
            Assert.True(acquireStarted.Wait(TimeSpan.FromSeconds(10)));
            Assert.False(acquire.IsCompleted);
            Assert.True(provider.SnapshotPublished);
            Assert.Equal(1, Assert.Single(residency.GetResources()).OwnerCount);
        }
        finally
        {
            provider.AllowPublicationReturn.Set();
            Task[] completions = acquire == null
                ? [process]
                : [process, acquire];
            await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(10));
        }

        using RuntimeAssetResidencyLease second = await acquire!;
        Assert.True(provider.SnapshotPublished);
        Assert.Equal(RuntimePreparedAssetState.Ready, first.State);
        Assert.Equal(RuntimePreparedAssetState.Ready, second.State);
        Assert.Equal(2, Assert.Single(residency.GetResources()).OwnerCount);
        Assert.Equal(1, provider.PrepareCount);
    }

    [Fact]
    public async Task RequiredProviderInvalidationRollsBackPublicationAndRetries()
    {
        Guid dependencyGuid = Guid.Parse("01000000-0000-0000-0000-000000000301");
        Guid rootGuid = Guid.Parse("f1000000-0000-0000-0000-000000000301");
        AddCookedAsset(
            dependencyGuid,
            "Mesh",
            RuntimeAssetVariantPolicy.StaticMesh,
            64);
        AddCookedAsset(
            rootGuid,
            "Material",
            RuntimeAssetVariantPolicy.Material,
            64);
        m_Database.UseReadOnlyRuntime();
        using var residency = CreateResidency(maxInactiveResources: 0);
        var dependencyProvider = new FakePreparedProvider("Mesh", gpuBytes: 64);
        RuntimeAssetResidencyKey dependencyKey = MeshKey(dependencyGuid);
        using var rootProvider = new BlockingPublicationPreparedProvider(
            residency,
            assetType: "Material",
            requiredKey: dependencyKey);
        residency.RegisterPreparedProvider(dependencyProvider);
        residency.RegisterPreparedProvider(rootProvider);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [
                Dependency(dependencyGuid, "Mesh", required: true),
                Dependency(rootGuid, "Material", required: true)
            ],
            pinned: false);

        Task process = Task.Run(residency.ProcessAtFrameBoundary);
        Task<bool>? invalidation = null;
        try
        {
            Assert.True(
                rootProvider.PublicationEntered.Wait(TimeSpan.FromSeconds(10)),
                "The root provider did not enter its publication callback.");
            invalidation = Task.Run(() => residency.InvalidatePreparedProvider(
                dependencyProvider.ProviderId,
                "Required prepared resources were invalidated for the test."));
            Assert.True(SpinWait.SpinUntil(
                () => residency.GetResources()
                    .Single(resource => resource.Key == dependencyKey)
                    .Diagnostic.Contains("invalidated", StringComparison.OrdinalIgnoreCase),
                TimeSpan.FromSeconds(10)));
            Assert.False(invalidation.IsCompleted);
        }
        finally
        {
            rootProvider.AllowPublicationReturn.Set();
            Task[] completions = invalidation == null
                ? [process]
                : [process, invalidation];
            await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.True(await invalidation!);
        Assert.False(rootProvider.SnapshotPublished);
        Assert.Equal(1, rootProvider.RollbackCount);
        Assert.Equal(RuntimePreparedAssetState.Waiting, lease.State);
        Assert.All(
            residency.GetResources(),
            resource => Assert.Equal(RuntimePreparedAssetState.Waiting, resource.PreparedState));

        residency.ProcessAtFrameBoundary();

        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
        Assert.True(rootProvider.SnapshotPublished);
        Assert.Equal(2, rootProvider.PrepareCount);
        Assert.Equal(1, rootProvider.RollbackCount);
        Assert.Equal(2, dependencyProvider.PrepareCount);
        Assert.Equal(1, dependencyProvider.ReleaseCount);
    }

    [Fact]
    public void InvalidationCleanupAttemptsEveryReleaseAndKeepsAdmissionClosedOnFailure()
    {
        Guid firstGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        Guid secondGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var provider = new FailingReleasePreparedProvider("Mesh", gpuBytes: 64);
        provider.OnlyFailGuid = firstGuid;
        var residency = CreateResidency(maxInactiveResources: 0);
        RuntimeAssetResidencyLease? lease = null;
        try
        {
            residency.RegisterPreparedProvider(provider);
            lease = residency.AcquireSceneDependencies(
                CellOwner(1, generation: 1),
                [
                    Dependency(firstGuid, "Mesh", required: true),
                    Dependency(secondGuid, "Mesh", required: true)
                ],
                pinned: false);
            residency.ProcessAtFrameBoundary();

            AggregateException failure = Assert.Throws<AggregateException>(() =>
                residency.InvalidatePreparedProvider(provider.ProviderId, "test invalidation"));

            Assert.Single(failure.InnerExceptions);
            Assert.Equal(2, provider.ReleaseAttemptCount);
            Assert.Equal(1, provider.GetReleaseAttemptCount(firstGuid));
            Assert.Equal(1, provider.GetReleaseAttemptCount(secondGuid));
            Assert.All(
                residency.GetResources(),
                resource => Assert.Equal(RuntimePreparedAssetState.Waiting, resource.PreparedState));
            int prepareCount = provider.PrepareCount;
            residency.ProcessAtFrameBoundary();
            Assert.Equal(prepareCount, provider.PrepareCount);

            provider.FailReleases = false;
            Assert.True(residency.InvalidatePreparedProvider(
                provider.ProviderId,
                "retry invalidation cleanup"));
            Assert.Equal(3, provider.ReleaseAttemptCount);
            Assert.Equal(2, provider.GetReleaseAttemptCount(firstGuid));
            Assert.Equal(1, provider.GetReleaseAttemptCount(secondGuid));
            residency.ProcessAtFrameBoundary();
            Assert.True(provider.PrepareCount > prepareCount);
        }
        finally
        {
            provider.FailReleases = false;
            lease?.Dispose();
            residency.Dispose();
        }
    }

    [Fact]
    public void UnregisterCleanupAttemptsEveryReleaseBeforeReportingFailure()
    {
        Guid firstGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        Guid secondGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var provider = new FailingReleasePreparedProvider("Mesh", gpuBytes: 64);
        provider.OnlyFailGuid = firstGuid;
        var residency = CreateResidency(maxInactiveResources: 0);
        RuntimeAssetResidencyLease? lease = null;
        try
        {
            residency.RegisterPreparedProvider(provider);
            lease = residency.AcquireSceneDependencies(
                CellOwner(1, generation: 1),
                [
                    Dependency(firstGuid, "Mesh", required: true),
                    Dependency(secondGuid, "Mesh", required: true)
                ],
                pinned: false);
            residency.ProcessAtFrameBoundary();

            AggregateException failure = Assert.Throws<AggregateException>(() =>
                residency.UnregisterPreparedProvider(provider.ProviderId));

            Assert.Single(failure.InnerExceptions);
            Assert.True(residency.IsPreparedProviderRegistered(provider));
            Assert.Equal(2, provider.ReleaseAttemptCount);
            Assert.Equal(1, provider.GetReleaseAttemptCount(firstGuid));
            Assert.Equal(1, provider.GetReleaseAttemptCount(secondGuid));
            Assert.Throws<InvalidOperationException>(() =>
                residency.RegisterPreparedProvider(provider));

            provider.FailReleases = false;
            Assert.True(residency.UnregisterPreparedProvider(provider.ProviderId));
            Assert.False(residency.IsPreparedProviderRegistered(provider));
            Assert.Equal(3, provider.ReleaseAttemptCount);
            Assert.Equal(2, provider.GetReleaseAttemptCount(firstGuid));
            Assert.Equal(1, provider.GetReleaseAttemptCount(secondGuid));
            Assert.False(residency.UnregisterPreparedProvider(provider.ProviderId));
        }
        finally
        {
            provider.FailReleases = false;
            lease?.Dispose();
            residency.Dispose();
        }
    }

    [Fact]
    public void FailedInvalidationCleanupCanBePromotedToUnregister()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var provider = new FailingReleasePreparedProvider("Mesh", gpuBytes: 64);
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);
        residency.ProcessAtFrameBoundary();

        AggregateException failure = Assert.Throws<AggregateException>(() =>
            residency.InvalidatePreparedProvider(
                provider.ProviderId,
                "Injected invalidation cleanup failure."));

        Assert.Single(failure.InnerExceptions);
        Assert.Equal(1, provider.GetReleaseAttemptCount(meshGuid));
        provider.FailReleases = false;

        Assert.True(residency.UnregisterPreparedProvider(provider.ProviderId));
        Assert.Equal(2, provider.GetReleaseAttemptCount(meshGuid));
        Assert.False(residency.UnregisterPreparedProvider(provider.ProviderId));
    }

    [Fact]
    public void DisposeCleanupAttemptsEveryReleaseAndCookedHandle()
    {
        Guid firstGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        Guid secondGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var provider = new FailingReleasePreparedProvider("Mesh", gpuBytes: 64);
        var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [
                Dependency(firstGuid, "Mesh", required: true),
                Dependency(secondGuid, "Mesh", required: true)
            ],
            pinned: false);
        residency.ProcessAtFrameBoundary();

        AggregateException failure = Assert.Throws<AggregateException>(residency.Dispose);

        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.Equal(2, provider.ReleaseAttemptCount);
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
        provider.FailReleases = false;
        residency.Dispose();
        Assert.Equal(4, provider.ReleaseAttemptCount);
        residency.Dispose();
    }

    [Fact]
    public void EvictionCleanupAttemptsEveryReleaseAndCookedHandle()
    {
        Guid firstGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        Guid secondGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var provider = new FailingReleasePreparedProvider("Mesh", gpuBytes: 64);
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [
                Dependency(firstGuid, "Mesh", required: true),
                Dependency(secondGuid, "Mesh", required: true)
            ],
            pinned: false);
        residency.ProcessAtFrameBoundary();
        lease.Dispose();

        AggregateException failure = Assert.Throws<AggregateException>(
            residency.ProcessAtFrameBoundary);

        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.Equal(2, provider.ReleaseAttemptCount);
        Assert.Empty(residency.GetResources());
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
        provider.FailReleases = false;
        residency.ProcessAtFrameBoundary();
        Assert.Equal(4, provider.ReleaseAttemptCount);
    }

    [Fact]
    public void MixedCleanupRetryOnlyRepeatsFailedProviderAndCookedHandleKeys()
    {
        Guid providerFailureGuid = AddCookedAsset(
            "Mesh",
            RuntimeAssetVariantPolicy.StaticMesh,
            64);
        Guid handleFailureGuid = AddCookedAsset(
            "Mesh",
            RuntimeAssetVariantPolicy.StaticMesh,
            64);
        m_Database.UseReadOnlyRuntime();
        var database = new FailOnceReleaseAssetDatabase(m_Database, handleFailureGuid);
        using var provider = new FailingReleasePreparedProvider("Mesh", gpuBytes: 64)
        {
            OnlyFailGuid = providerFailureGuid
        };
        using var residency = CreateResidency(
            database,
            maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [
                Dependency(providerFailureGuid, "Mesh", required: true),
                Dependency(handleFailureGuid, "Mesh", required: true)
            ],
            pinned: false);
        residency.ProcessAtFrameBoundary();
        lease.Dispose();

        AggregateException failure = Assert.Throws<AggregateException>(
            residency.ProcessAtFrameBoundary);

        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.Equal(1, provider.GetReleaseAttemptCount(providerFailureGuid));
        Assert.Equal(1, provider.GetReleaseAttemptCount(handleFailureGuid));
        Assert.Equal(1, database.GetReleaseAttemptCount(providerFailureGuid));
        Assert.Equal(1, database.GetReleaseAttemptCount(handleFailureGuid));
        Assert.Equal(64, residency.GetMetrics().CpuCookedBytes);
        Assert.Equal(
            handleFailureGuid,
            Assert.Single(m_Database.GetLoadedCookedAssetDiagnostics()).Guid);

        provider.FailReleases = false;
        residency.ProcessAtFrameBoundary();

        Assert.Equal(2, provider.GetReleaseAttemptCount(providerFailureGuid));
        Assert.Equal(1, provider.GetReleaseAttemptCount(handleFailureGuid));
        Assert.Equal(1, database.GetReleaseAttemptCount(providerFailureGuid));
        Assert.Equal(2, database.GetReleaseAttemptCount(handleFailureGuid));
        Assert.Equal(0, residency.GetMetrics().CpuCookedBytes);
        Assert.Empty(residency.GetResources());
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public void PendingCookedHandleBytesRemainAccountedUntilReleaseSucceeds()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        var database = new FailOnceReleaseAssetDatabase(m_Database, meshGuid);
        using var residency = CreateResidency(
            database,
            maxInactiveResources: 0);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);
        Assert.Equal(64, residency.GetMetrics().CpuCookedBytes);
        lease.Dispose();

        AggregateException failure = Assert.Throws<AggregateException>(
            residency.ProcessAtFrameBoundary);

        Assert.Single(failure.InnerExceptions);
        Assert.Equal(1, database.GetReleaseAttemptCount(meshGuid));
        Assert.Equal(64, residency.GetMetrics().CpuCookedBytes);
        Assert.Empty(residency.GetResources());
        Assert.Single(m_Database.GetLoadedCookedAssetDiagnostics());

        residency.ProcessAtFrameBoundary();

        Assert.Equal(2, database.GetReleaseAttemptCount(meshGuid));
        Assert.Equal(0, residency.GetMetrics().CpuCookedBytes);
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public async Task SameHandleCleanupTransfersAccountingAndReuseBlockingToTheLastLoser()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var database = new SharedHandleRacingAssetDatabase(
            m_Database,
            expectedLoadCount: 3,
            failedReleaseCount: 2);
        using var residency = CreateResidency(
            database,
            maxInactiveResources: 0);
        using var start = new ManualResetEventSlim(false);
        Task<(RuntimeAssetResidencyLease? Lease, Exception? Failure)>[] acquisitions =
            Enumerable.Range(1, 3)
                .Select(cell => Task.Run(() =>
                {
                    start.Wait();
                    try
                    {
                        return (
                            Lease: (RuntimeAssetResidencyLease?)residency.AcquireSceneDependencies(
                                CellOwner(cell, generation: 1),
                                [Dependency(meshGuid, "Mesh", required: true)],
                                pinned: false),
                            Failure: (Exception?)null);
                    }
                    catch (Exception ex)
                    {
                        return (Lease: (RuntimeAssetResidencyLease?)null, Failure: ex);
                    }
                }))
                .ToArray();

        start.Set();
        (RuntimeAssetResidencyLease? Lease, Exception? Failure)[] outcomes =
            await Task.WhenAll(acquisitions).WaitAsync(TimeSpan.FromSeconds(10));
        using RuntimeAssetResidencyLease winner = Assert.Single(
            outcomes,
            outcome => outcome.Lease != null).Lease!;
        Exception[] failures = outcomes
            .Where(outcome => outcome.Failure != null)
            .Select(outcome => outcome.Failure!)
            .ToArray();
        Assert.Equal(2, failures.Length);
        Assert.All(failures, failure => Assert.IsType<InvalidOperationException>(failure));
        Assert.Equal(2, database.ReleaseAttemptCount);
        Assert.Equal(3, database.ReferenceCount);
        Assert.Equal(64, residency.GetMetrics().CpuCookedBytes);
        Assert.Equal(1, Assert.Single(residency.GetResources()).OwnerCount);

        using RuntimeAssetResidencyLease shared = residency.AcquireSceneDependencies(
            CellOwner(4, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);
        Assert.Equal(3, database.LoadCount);
        Assert.Equal(2, Assert.Single(residency.GetResources()).OwnerCount);

        database.FailNextReleases(1);
        shared.Dispose();
        winner.Dispose();
        AggregateException partialCleanup = Assert.Throws<AggregateException>(
            residency.ProcessAtFrameBoundary);

        Assert.Single(partialCleanup.InnerExceptions);
        Assert.Equal(5, database.ReleaseAttemptCount);
        Assert.Equal(1, database.ReferenceCount);
        Assert.Equal(64, residency.GetMetrics().CpuCookedBytes);
        Assert.Empty(residency.GetResources());
        Assert.Single(m_Database.GetLoadedCookedAssetDiagnostics());

        InvalidDataException blocked = Assert.Throws<InvalidDataException>(() =>
            residency.AcquireSceneDependencies(
                CellOwner(5, generation: 1),
                [Dependency(meshGuid, "Mesh", required: true)],
                pinned: false));
        Assert.Contains("still completing deterministic cleanup", blocked.Message);
        Assert.Equal(3, database.LoadCount);
        Assert.Equal(5, database.ReleaseAttemptCount);

        residency.ProcessAtFrameBoundary();

        Assert.Equal(6, database.ReleaseAttemptCount);
        Assert.Equal(0, database.ReferenceCount);
        Assert.Equal(0, residency.GetMetrics().CpuCookedBytes);
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());

        using RuntimeAssetResidencyLease reacquired = residency.AcquireSceneDependencies(
            CellOwner(6, generation: 1),
            [Dependency(meshGuid, "Mesh", required: true)],
            pinned: false);
        Assert.Equal(4, database.LoadCount);
        Assert.Equal(1, database.ReferenceCount);
        Assert.Equal(64, residency.GetMetrics().CpuCookedBytes);

        reacquired.Dispose();
        residency.ProcessAtFrameBoundary();

        Assert.Equal(7, database.ReleaseAttemptCount);
        Assert.Equal(0, database.ReferenceCount);
        Assert.Equal(0, residency.GetMetrics().CpuCookedBytes);
        Assert.Empty(residency.GetResources());
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public void BudgetRejectedTemporaryHandleFailureBlocksReuseUntilCleanupRetryCompletes()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        var database = new FailOnceReleaseAssetDatabase(m_Database, meshGuid);
        using var residency = CreateResidency(
            database,
            maxInactiveResources: 0,
            maxCpuCookedBytes: 32);

        InvalidOperationException cleanupFailure = Assert.Throws<InvalidOperationException>(() =>
            residency.AcquireSceneDependencies(
                CellOwner(1, generation: 1),
                [Dependency(meshGuid, "Mesh", required: true)],
                pinned: false));
        Assert.Contains("deterministic cleanup ownership", cleanupFailure.Message);
        Assert.Equal(1, database.GetReleaseAttemptCount(meshGuid));
        Assert.Equal(64, residency.GetMetrics().CpuCookedBytes);
        Assert.Empty(residency.GetResources());
        Assert.Single(m_Database.GetLoadedCookedAssetDiagnostics());

        InvalidDataException blocked = Assert.Throws<InvalidDataException>(() =>
            residency.AcquireSceneDependencies(
                CellOwner(2, generation: 1),
                [Dependency(meshGuid, "Mesh", required: true)],
                pinned: false));
        Assert.Contains("still completing deterministic cleanup", blocked.Message);
        Assert.Equal(1, database.GetReleaseAttemptCount(meshGuid));

        residency.ProcessAtFrameBoundary();

        Assert.Equal(2, database.GetReleaseAttemptCount(meshGuid));
        Assert.Equal(0, residency.GetMetrics().CpuCookedBytes);
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());

        InvalidDataException budget = Assert.Throws<InvalidDataException>(() =>
            residency.AcquireSceneDependencies(
                CellOwner(3, generation: 1),
                [Dependency(meshGuid, "Mesh", required: true)],
                pinned: false));
        Assert.Contains("would exceed the CPU cooked residency budget", budget.Message);
        Assert.Equal(3, database.GetReleaseAttemptCount(meshGuid));
        Assert.Equal(0, residency.GetMetrics().CpuCookedBytes);
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public async Task CompatibleOwnerAttachmentDuringPrepareMakesTheResultWaitingForRetry()
    {
        Guid assetGuid = Guid.Parse("01000000-0000-0000-0000-000000000101");
        Guid dependencyGuid = Guid.Parse("f1000000-0000-0000-0000-000000000101");
        AddCookedAsset(assetGuid, "Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        AddCookedAsset(dependencyGuid, "Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var provider = new BlockingPreparedProvider("Mesh", gpuBytes: 64);
        using var residency = CreateResidency(maxInactiveResources: 2);
        residency.RegisterPreparedProvider(provider);
        CookedSceneDependency[] plan =
        [
            Dependency(assetGuid, "Mesh", required: true),
            Dependency(dependencyGuid, "Mesh", required: true)
        ];
        using RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            plan,
            pinned: false);

        Task process = Task.Run(residency.ProcessAtFrameBoundary);
        try
        {
            Assert.True(provider.PrepareEntered.Wait(TimeSpan.FromSeconds(10)));
            using RuntimeAssetResidencyLease second = residency.AcquireSceneDependencies(
                CellOwner(2, generation: 1),
                plan,
                pinned: false);
            Assert.Equal(2, residency.GetResources().Single(
                resource => resource.Key.Guid == assetGuid).OwnerCount);
        }
        finally
        {
            provider.AllowPrepare.Set();
        }

        await process.WaitAsync(TimeSpan.FromSeconds(10));

        RuntimeAssetResidencySnapshot stale = residency.GetResources().Single(
            resource => resource.Key.Guid == assetGuid);
        Assert.Equal(RuntimePreparedAssetState.Waiting, stale.PreparedState);
        Assert.Equal(1, provider.ReleaseCount);
        Assert.Equal(RuntimePreparedAssetState.Waiting, first.State);

        residency.ProcessAtFrameBoundary();

        Assert.Equal(RuntimePreparedAssetState.Ready, first.State);
        Assert.Equal(3, provider.PrepareCount);
    }

    [Fact]
    public void WaitingSetupDoesNotStarveLaterWaitingResourcesInTheSameFrame()
    {
        Guid firstGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 128);
        Guid secondGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 128);
        m_Database.UseReadOnlyRuntime();
        using var provider = new WaitFirstPreparedProvider(firstGuid, gpuBytes: 512);
        using var residency = CreateResidency(maxSetupsPerFrame: 2, maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(firstGuid, "Mesh", true), Dependency(secondGuid, "Mesh", true)],
            pinned: false);

        residency.ProcessAtFrameBoundary();

        RuntimeAssetResidencyMetrics firstFrame = residency.GetMetrics();
        Assert.Equal(RuntimePreparedAssetState.Waiting, lease.State);
        Assert.Equal(1, firstFrame.WaitingAssetCount);
        Assert.Equal(1, firstFrame.ReadyAssetCount);
        Assert.Equal(2, provider.PrepareCount);

        residency.ProcessAtFrameBoundary();

        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
        Assert.Equal(3, provider.PrepareCount);
    }

    [Fact]
    public void BoundPreparationDependenciesRejectAnIncompatibleLaterOwner()
    {
        Guid assetGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        Guid dependencyGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var residency = CreateResidency(maxInactiveResources: 2);
        using RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [
                Dependency(assetGuid, "Mesh", required: true),
                Dependency(dependencyGuid, "Mesh", required: true)
            ],
            pinned: false);
        RuntimeAssetResidencyKey assetKey = MeshKey(assetGuid);
        RuntimeAssetResidencyKey dependencyKey = MeshKey(dependencyGuid);

        Assert.True(residency.TryGetPreparationClaim(assetKey, out RuntimeAssetPreparationClaim claim));
        Assert.True(
            residency.TryBindPreparationDependencies(claim, [dependencyKey], out string diagnostic),
            diagnostic);

        InvalidDataException failure = Assert.Throws<InvalidDataException>(() =>
            residency.AcquireSceneDependencies(
                CellOwner(2, generation: 1),
                [Dependency(assetGuid, "Mesh", required: true)],
                pinned: false));

        Assert.Contains("omits decoded dependency", failure.Message);
        Assert.Equal(1, residency.GetMetrics().ActiveOwnerCount);
        Assert.All(residency.GetResources(), resource => Assert.Equal(1, resource.OwnerCount));
    }

    [Fact]
    public void CompatibleOwnerPlanChangeInvalidatesThePreviousPreparationClaim()
    {
        Guid assetGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        Guid dependencyGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var residency = CreateResidency(maxInactiveResources: 2);
        CookedSceneDependency[] plan =
        [
            Dependency(assetGuid, "Mesh", required: true),
            Dependency(dependencyGuid, "Mesh", required: true)
        ];
        using RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            plan,
            pinned: false);
        RuntimeAssetResidencyKey assetKey = MeshKey(assetGuid);
        RuntimeAssetResidencyKey dependencyKey = MeshKey(dependencyGuid);
        Assert.True(residency.TryGetPreparationClaim(assetKey, out RuntimeAssetPreparationClaim firstClaim));
        Assert.True(residency.TryBindPreparationDependencies(firstClaim, [dependencyKey], out _));

        using RuntimeAssetResidencyLease second = residency.AcquireSceneDependencies(
            CellOwner(2, generation: 1),
            plan,
            pinned: false);

        Assert.False(residency.TryBindPreparationDependencies(firstClaim, [dependencyKey], out _));
        Assert.True(residency.TryGetPreparationClaim(assetKey, out RuntimeAssetPreparationClaim secondClaim));
        Assert.NotEqual(firstClaim.OwnerPlanGeneration, secondClaim.OwnerPlanGeneration);
        Assert.True(
            residency.TryBindPreparationDependencies(secondClaim, [dependencyKey], out string diagnostic),
            diagnostic);
    }

    [Fact]
    public async Task DependencyBindAndIncompatibleOwnerAddCannotBothSucceed()
    {
        Guid assetGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        Guid dependencyGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var residency = CreateResidency(maxInactiveResources: 2);
        using RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [
                Dependency(assetGuid, "Mesh", required: true),
                Dependency(dependencyGuid, "Mesh", required: true)
            ],
            pinned: false);
        RuntimeAssetResidencyKey assetKey = MeshKey(assetGuid);
        RuntimeAssetResidencyKey dependencyKey = MeshKey(dependencyGuid);
        Assert.True(residency.TryGetPreparationClaim(assetKey, out RuntimeAssetPreparationClaim claim));
        using var start = new ManualResetEventSlim(false);
        bool bindSucceeded = false;
        string bindDiagnostic = string.Empty;
        RuntimeAssetResidencyLease? second = null;
        Exception? acquireFailure = null;

        Task bind = Task.Run(() =>
        {
            start.Wait();
            bindSucceeded = residency.TryBindPreparationDependencies(
                claim,
                [dependencyKey],
                out bindDiagnostic);
        });
        Task acquire = Task.Run(() =>
        {
            start.Wait();
            try
            {
                second = residency.AcquireSceneDependencies(
                    CellOwner(2, generation: 1),
                    [Dependency(assetGuid, "Mesh", required: true)],
                    pinned: false);
            }
            catch (Exception ex)
            {
                acquireFailure = ex;
            }
        });

        start.Set();
        await Task.WhenAll(bind, acquire).WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            Assert.NotEqual(bindSucceeded, second != null);
            if (bindSucceeded)
            {
                Assert.IsType<InvalidDataException>(acquireFailure);
            }
            else
            {
                Assert.Null(acquireFailure);
                Assert.Contains("no longer current", bindDiagnostic);
                Assert.True(residency.TryGetPreparationClaim(
                    assetKey,
                    out RuntimeAssetPreparationClaim current));
                Assert.False(residency.TryBindPreparationDependencies(
                    current,
                    [dependencyKey],
                    out string incompatibleDiagnostic));
                Assert.Contains("does not require decoded dependency", incompatibleDiagnostic);
            }
        }
        finally
        {
            second?.Dispose();
        }
    }

    [Fact]
    public void BoundDependencyRejectionRollsBackEarlierResourceAttachments()
    {
        Guid fillerGuid = Guid.Parse("01000000-0000-0000-0000-000000000001");
        Guid assetGuid = Guid.Parse("f1000000-0000-0000-0000-000000000001");
        Guid dependencyGuid = Guid.Parse("f2000000-0000-0000-0000-000000000001");
        AddCookedAsset(fillerGuid, "Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        AddCookedAsset(assetGuid, "Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        AddCookedAsset(dependencyGuid, "Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        using var residency = CreateResidency(maxInactiveResources: 3);
        using RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [
                Dependency(assetGuid, "Mesh", required: true),
                Dependency(dependencyGuid, "Mesh", required: true)
            ],
            pinned: false);
        RuntimeAssetResidencyKey assetKey = MeshKey(assetGuid);
        Assert.True(residency.TryGetPreparationClaim(assetKey, out RuntimeAssetPreparationClaim claim));
        Assert.True(residency.TryBindPreparationDependencies(
            claim,
            [MeshKey(dependencyGuid)],
            out _));

        Assert.Throws<InvalidDataException>(() => residency.AcquireSceneDependencies(
            CellOwner(2, generation: 1),
            [
                Dependency(fillerGuid, "Mesh", required: true),
                Dependency(assetGuid, "Mesh", required: true)
            ],
            pinned: false));

        Assert.Equal(1, residency.GetMetrics().ActiveOwnerCount);
        RuntimeAssetResidencySnapshot filler = residency.GetResources().Single(
            resource => resource.Key.Guid == fillerGuid);
        Assert.Equal(0, filler.OwnerCount);
        Assert.Equal(1, residency.GetResources().Single(
            resource => resource.Key.Guid == assetGuid).OwnerCount);
    }

    [Fact]
    public async Task AcquireCannotRepublishResourcesAfterConcurrentDispose()
    {
        Guid assetGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        var residency = CreateResidency(maxInactiveResources: 1);
        using var loadEntered = new ManualResetEventSlim(false);
        using var allowLoad = new ManualResetEventSlim(false);
        m_Database.SuccessfulCookedAssetLoad += guid =>
        {
            if (guid == assetGuid)
            {
                loadEntered.Set();
                allowLoad.Wait();
            }
        };
        Exception? acquireFailure = null;
        Task acquire = Task.Run(() =>
        {
            try
            {
                residency.AcquireSceneDependencies(
                    CellOwner(1, generation: 1),
                    [Dependency(assetGuid, "Mesh", required: true)],
                    pinned: false);
            }
            catch (Exception ex)
            {
                acquireFailure = ex;
            }
        });

        try
        {
            Assert.True(loadEntered.Wait(TimeSpan.FromSeconds(10)));
            Task dispose = Task.Run(residency.Dispose);
            Assert.True(SpinWait.SpinUntil(
                () => residency.IsDisposed,
                TimeSpan.FromSeconds(10)));
            Assert.False(dispose.IsCompleted);
            allowLoad.Set();
            await acquire.WaitAsync(TimeSpan.FromSeconds(10));
            await dispose.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            allowLoad.Set();
            if (!acquire.IsCompleted)
            {
                await acquire.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }

        Assert.IsType<ObjectDisposedException>(acquireFailure);
        Assert.Empty(residency.GetResources());
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public void MissingRequiredCookedDependencyFailsBeforeOwnerIsPublished()
    {
        Guid meshGuid = Guid.Parse("84000000-0000-0000-0000-000000000101");
        string source = Path.Combine(m_Root, "Missing.obj");
        File.WriteAllText(source, "# missing cooked artifact");
        m_Database.AddAsset(meshGuid, "Mesh", source, PackageId);
        m_Database.UseReadOnlyRuntime();
        using var residency = CreateResidency();

        InvalidDataException failure = Assert.Throws<InvalidDataException>(() =>
            residency.AcquireSceneDependencies(
                CellOwner(1, generation: 1),
                [Dependency(meshGuid, "Mesh", required: true)],
                pinned: false));

        Assert.Contains("source fallback is disabled", failure.Message);
        Assert.Equal(0, residency.GetMetrics().ActiveOwnerCount);
        Assert.Empty(residency.GetResources());
    }

    [Fact]
    public void InactiveEvictionUsesDeterministicLeastRecentlyNeededOrder()
    {
        Guid firstGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        Guid secondGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        var provider = new FakePreparedProvider("Mesh", gpuBytes: 64);
        using var residency = CreateResidency(maxInactiveResources: 1);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, 1), [Dependency(firstGuid, "Mesh", true)], pinned: false);
        RuntimeAssetResidencyLease second = residency.AcquireSceneDependencies(
            CellOwner(2, 1), [Dependency(secondGuid, "Mesh", true)], pinned: false);
        residency.ProcessAtFrameBoundary();
        first.Dispose();
        second.Dispose();

        residency.ProcessAtFrameBoundary();

        RuntimeAssetResidencySnapshot remaining = Assert.Single(residency.GetResources());
        Assert.Equal(secondGuid, remaining.Key.Guid);
        Assert.Equal(1, provider.ReleaseCount);
        Assert.Equal(1, residency.GetMetrics().EvictionCount);
    }

    [Fact]
    public void ProjectedBudgetsEvictOnlyTheMinimumLruEntry()
    {
        Guid firstGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        Guid secondGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        Guid thirdGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        var provider = new FakePreparedProvider("Mesh", gpuBytes: 64);
        using var residency = CreateResidency(
            maxInactiveResources: 3,
            maxCpuCookedBytes: 3 * 64,
            maxPreparedGpuBytes: 2 * 64);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(firstGuid, "Mesh", required: true)],
            pinned: false);
        RuntimeAssetResidencyLease second = residency.AcquireSceneDependencies(
            CellOwner(2, generation: 1),
            [Dependency(secondGuid, "Mesh", required: true)],
            pinned: false);
        RuntimeAssetResidencyLease third = residency.AcquireSceneDependencies(
            CellOwner(3, generation: 1),
            [Dependency(thirdGuid, "Mesh", required: true)],
            pinned: false);
        residency.ProcessAtFrameBoundary();
        Assert.Equal(3 * 64, residency.GetMetrics().PreparedGpuBytes);
        first.Dispose();
        second.Dispose();
        third.Dispose();

        residency.ProcessAtFrameBoundary();

        RuntimeAssetResidencySnapshot[] remaining = residency.GetResources().ToArray();
        Assert.Equal(2, remaining.Length);
        Assert.DoesNotContain(remaining, resource => resource.Key.Guid == firstGuid);
        Assert.Contains(remaining, resource => resource.Key.Guid == secondGuid);
        Assert.Contains(remaining, resource => resource.Key.Guid == thirdGuid);
        Assert.Equal(1, provider.ReleaseCount);
        RuntimeAssetResidencyMetrics metrics = residency.GetMetrics();
        Assert.Equal(2, metrics.InactiveAssetCount);
        Assert.Equal(2 * 64, metrics.CpuCookedBytes);
        Assert.Equal(2 * 64, metrics.PreparedGpuBytes);
        Assert.Equal(1, metrics.EvictionCount);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(m_Root)) Directory.Delete(m_Root, recursive: true);
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private RuntimeAssetResidencyService CreateResidency(
        int maxSetupsPerFrame = 4,
        int maxInactiveResources = 0,
        long maxCpuCookedBytes = 1024 * 1024,
        long maxPreparedGpuBytes = 1024 * 1024) =>
        CreateResidency(
            m_Database,
            maxSetupsPerFrame,
            maxInactiveResources,
            maxCpuCookedBytes,
            maxPreparedGpuBytes);

    private static RuntimeAssetResidencyService CreateResidency(
        IAssetDatabase database,
        int maxSetupsPerFrame = 4,
        int maxInactiveResources = 0,
        long maxCpuCookedBytes = 1024 * 1024,
        long maxPreparedGpuBytes = 1024 * 1024)
    {
        return new RuntimeAssetResidencyService(
            database,
            new RuntimeAssetResidencyBudgets(
                MaxCpuCookedBytes: maxCpuCookedBytes,
                MaxPreparedGpuBytes: maxPreparedGpuBytes,
                MaxSetupsPerFrame: maxSetupsPerFrame,
                MaxSetupMilliseconds: 100,
                MaxInactiveResources: maxInactiveResources));
    }

    private Guid AddCookedAsset(string assetType, string variant, int byteCount)
    {
        return AddCookedAsset(Guid.NewGuid(), assetType, variant, byteCount);
    }

    private Guid AddCookedAsset(
        Guid guid,
        string assetType,
        string variant,
        int byteCount)
    {
        string source = Path.Combine(m_Root, guid.ToString("N") + ".source");
        string cooked = Path.Combine(m_Root, guid.ToString("N") + ".cooked");
        File.WriteAllText(source, "source");
        File.WriteAllBytes(cooked, Enumerable.Range(0, byteCount).Select(index => (byte)index).ToArray());
        m_Database.AddAsset(guid, assetType, source, PackageId);
        m_Database.RegisterCookedArtifact(new CookedAssetRecord(
            guid,
            assetType,
            variant,
            cooked,
            byteCount,
            File.GetLastWriteTimeUtc(cooked)));
        return guid;
    }

    private static CookedSceneDependency Dependency(Guid guid, string assetType, bool required) =>
        new(guid, PackageId, assetType, required);

    private static RuntimeAssetResidencyKey MeshKey(Guid guid) => new(
        guid,
        PackageId,
        "Mesh",
        RuntimeAssetVariantPolicy.StaticMesh);

    private static RuntimeAssetResidencyOwnerId CellOwner(int cell, long generation) =>
        RuntimeAssetResidencyOwnerId.Cell(
            s_WorldGuid,
            new WorldCellId(new Guid($"84100000-0000-0000-0000-{cell:D12}")),
            generation);

    private sealed class FailOnceReleaseAssetDatabase : IAssetDatabase
    {
        private readonly object m_Gate = new();
        private readonly IAssetDatabase m_Inner;
        private readonly HashSet<Guid> m_FailOnceGuids;
        private readonly Dictionary<Guid, int> m_ReleaseAttempts = new();

        public FailOnceReleaseAssetDatabase(IAssetDatabase inner, params Guid[] failOnceGuids)
        {
            m_Inner = inner;
            m_FailOnceGuids = failOnceGuids.ToHashSet();
        }

        public AssetDatabaseMode Mode => m_Inner.Mode;
        public bool IsReadOnlyRuntime => m_Inner.IsReadOnlyRuntime;
        public AssetSourceAccessMode SourceAccessMode => m_Inner.SourceAccessMode;
        public bool CanReadSourceAssets => m_Inner.CanReadSourceAssets;
        public string CookedRoot => m_Inner.CookedRoot;
        public IReadOnlyCollection<AssetRecord> Assets => m_Inner.Assets;

        public event Action<AssetChangeEvent>? AssetChanged
        {
            add => m_Inner.AssetChanged += value;
            remove => m_Inner.AssetChanged -= value;
        }

        public int GetReleaseAttemptCount(Guid guid)
        {
            lock (m_Gate)
            {
                return m_ReleaseAttempts.GetValueOrDefault(guid);
            }
        }

        public bool TryGetAsset(Guid guid, out AssetRecord asset) =>
            m_Inner.TryGetAsset(guid, out asset);

        public bool TryGetAssetDescriptor(Guid guid, out AssetDescriptor asset) =>
            m_Inner.TryGetAssetDescriptor(guid, out asset);

        public bool TryGetCookedArtifact(
            Guid guid,
            string variant,
            out CookedAssetRecord artifact) =>
            m_Inner.TryGetCookedArtifact(guid, variant, out artifact);

        public CookedArtifactWrite BeginCookedArtifactWrite(
            Guid guid,
            string variant,
            string extension) =>
            m_Inner.BeginCookedArtifactWrite(guid, variant, extension);

        public bool TryLoadCookedAsset(
            Guid guid,
            string variant,
            string expectedAssetType,
            out CookedAssetHandle handle) =>
            m_Inner.TryLoadCookedAsset(guid, variant, expectedAssetType, out handle);

        public bool TryGetCookedAssetBytes(
            CookedAssetHandle handle,
            out ReadOnlyMemory<byte> bytes) =>
            m_Inner.TryGetCookedAssetBytes(handle, out bytes);

        public ReadOnlyMemory<byte> GetCookedAssetBytes(CookedAssetHandle handle) =>
            m_Inner.GetCookedAssetBytes(handle);

        public void Release(CookedAssetHandle handle)
        {
            bool fail;
            lock (m_Gate)
            {
                m_ReleaseAttempts[handle.Guid] =
                    m_ReleaseAttempts.GetValueOrDefault(handle.Guid) + 1;
                fail = m_FailOnceGuids.Remove(handle.Guid);
            }

            if (fail)
            {
                throw new InvalidOperationException(
                    $"Injected cooked handle release failure for '{handle.Guid:D}'.");
            }

            m_Inner.Release(handle);
        }

        public void ReleaseAllLoadedCookedAssets() =>
            m_Inner.ReleaseAllLoadedCookedAssets();

        public int InvalidateCookedAssets(Guid guid, string? variant = null) =>
            m_Inner.InvalidateCookedAssets(guid, variant);

        public int RemoveCookedArtifacts(IReadOnlyCollection<CookedAssetIdentity> identities) =>
            m_Inner.RemoveCookedArtifacts(identities);

        public void NotifyAssetChanged(AssetChangeEvent change) =>
            m_Inner.NotifyAssetChanged(change);

        public IReadOnlyList<LoadedCookedAssetDiagnostic> GetLoadedCookedAssetDiagnostics() =>
            m_Inner.GetLoadedCookedAssetDiagnostics();
    }

    private sealed class SharedHandleRacingAssetDatabase : IAssetDatabase, IDisposable
    {
        private readonly object m_Gate = new();
        private readonly IAssetDatabase m_Inner;
        private readonly int m_ExpectedLoadCount;
        private readonly ManualResetEventSlim m_AllLoadsReady = new(false);
        private CookedAssetHandle m_SharedHandle = CookedAssetHandle.Invalid;
        private int m_LoadCount;
        private int m_ReferenceCount;
        private int m_ReleaseAttemptCount;
        private int m_FailedReleaseCount;

        public SharedHandleRacingAssetDatabase(
            IAssetDatabase inner,
            int expectedLoadCount,
            int failedReleaseCount)
        {
            m_Inner = inner;
            m_ExpectedLoadCount = expectedLoadCount;
            m_FailedReleaseCount = failedReleaseCount;
        }

        public AssetDatabaseMode Mode => m_Inner.Mode;
        public bool IsReadOnlyRuntime => m_Inner.IsReadOnlyRuntime;
        public AssetSourceAccessMode SourceAccessMode => m_Inner.SourceAccessMode;
        public bool CanReadSourceAssets => m_Inner.CanReadSourceAssets;
        public string CookedRoot => m_Inner.CookedRoot;
        public IReadOnlyCollection<AssetRecord> Assets => m_Inner.Assets;
        public int LoadCount => Volatile.Read(ref m_LoadCount);
        public int ReferenceCount => Volatile.Read(ref m_ReferenceCount);
        public int ReleaseAttemptCount => Volatile.Read(ref m_ReleaseAttemptCount);

        public void FailNextReleases(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            lock (m_Gate)
            {
                if (m_FailedReleaseCount != 0)
                {
                    throw new InvalidOperationException(
                        "Shared cooked-handle release failures are already pending.");
                }

                m_FailedReleaseCount = count;
            }
        }

        public event Action<AssetChangeEvent>? AssetChanged
        {
            add => m_Inner.AssetChanged += value;
            remove => m_Inner.AssetChanged -= value;
        }

        public bool TryGetAsset(Guid guid, out AssetRecord asset) =>
            m_Inner.TryGetAsset(guid, out asset);

        public bool TryGetAssetDescriptor(Guid guid, out AssetDescriptor asset) =>
            m_Inner.TryGetAssetDescriptor(guid, out asset);

        public bool TryGetCookedArtifact(
            Guid guid,
            string variant,
            out CookedAssetRecord artifact) =>
            m_Inner.TryGetCookedArtifact(guid, variant, out artifact);

        public CookedArtifactWrite BeginCookedArtifactWrite(
            Guid guid,
            string variant,
            string extension) =>
            m_Inner.BeginCookedArtifactWrite(guid, variant, extension);

        public bool TryLoadCookedAsset(
            Guid guid,
            string variant,
            string expectedAssetType,
            out CookedAssetHandle handle)
        {
            lock (m_Gate)
            {
                if (!m_SharedHandle.IsValid)
                {
                    if (!m_Inner.TryLoadCookedAsset(
                            guid,
                            variant,
                            expectedAssetType,
                            out CookedAssetHandle loaded))
                    {
                        handle = CookedAssetHandle.Invalid;
                        return false;
                    }

                    m_SharedHandle = loaded;
                }

                handle = m_SharedHandle;
                m_LoadCount++;
                m_ReferenceCount++;
                if (m_LoadCount == m_ExpectedLoadCount)
                {
                    m_AllLoadsReady.Set();
                }
            }

            if (!m_AllLoadsReady.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException(
                    "The expected concurrent cooked-handle loads did not reach the race barrier.");
            }

            return true;
        }

        public bool TryGetCookedAssetBytes(
            CookedAssetHandle handle,
            out ReadOnlyMemory<byte> bytes) =>
            m_Inner.TryGetCookedAssetBytes(handle, out bytes);

        public ReadOnlyMemory<byte> GetCookedAssetBytes(CookedAssetHandle handle) =>
            m_Inner.GetCookedAssetBytes(handle);

        public void Release(CookedAssetHandle handle)
        {
            bool releaseInner = false;
            lock (m_Gate)
            {
                m_ReleaseAttemptCount++;
                if (m_FailedReleaseCount > 0)
                {
                    m_FailedReleaseCount--;
                    throw new InvalidOperationException(
                        $"Injected shared cooked-handle release failure for '{handle}'.");
                }

                if (handle != m_SharedHandle || m_ReferenceCount <= 0)
                {
                    throw new InvalidOperationException(
                        $"Shared cooked-handle release ownership was lost for '{handle}'.");
                }

                m_ReferenceCount--;
                releaseInner = m_ReferenceCount == 0;
                if (releaseInner)
                {
                    m_SharedHandle = CookedAssetHandle.Invalid;
                }
            }

            if (releaseInner)
            {
                m_Inner.Release(handle);
            }
        }

        public void ReleaseAllLoadedCookedAssets()
        {
            lock (m_Gate)
            {
                m_SharedHandle = CookedAssetHandle.Invalid;
                m_ReferenceCount = 0;
            }

            m_Inner.ReleaseAllLoadedCookedAssets();
        }

        public int InvalidateCookedAssets(Guid guid, string? variant = null) =>
            m_Inner.InvalidateCookedAssets(guid, variant);

        public int RemoveCookedArtifacts(IReadOnlyCollection<CookedAssetIdentity> identities) =>
            m_Inner.RemoveCookedArtifacts(identities);

        public void NotifyAssetChanged(AssetChangeEvent change) =>
            m_Inner.NotifyAssetChanged(change);

        public IReadOnlyList<LoadedCookedAssetDiagnostic> GetLoadedCookedAssetDiagnostics() =>
            m_Inner.GetLoadedCookedAssetDiagnostics();

        public void Dispose()
        {
            m_AllLoadsReady.Dispose();
        }
    }

    private sealed class BlockingReleasePreparedProvider : IRuntimePreparedAssetProvider, IDisposable
    {
        private readonly string m_AssetType;
        private readonly long m_GpuBytes;
        private readonly HashSet<RuntimeAssetResidencyKey> m_Prepared = new();
        private int m_ReleaseCount;
        private int m_Releasing;
        private int m_Disposed;
        private int m_ReleaseAfterDispose;
        private int m_MetricsWhileReleasing;

        public BlockingReleasePreparedProvider(string assetType, long gpuBytes)
        {
            m_AssetType = assetType;
            m_GpuBytes = gpuBytes;
        }

        public string ProviderId => "test.blocking-release-prepared-assets";
        public ManualResetEventSlim ReleaseEntered { get; } = new(false);
        public ManualResetEventSlim AllowRelease { get; } = new(false);
        public int ReleaseCount => Volatile.Read(ref m_ReleaseCount);
        public bool ReleaseIsInFlight => Volatile.Read(ref m_Releasing) != 0;
        public bool ReleaseAfterDispose => Volatile.Read(ref m_ReleaseAfterDispose) != 0;
        public bool MetricsWhileReleaseIsInFlight =>
            Volatile.Read(ref m_MetricsWhileReleasing) != 0;

        public bool Supports(string assetType) => assetType == m_AssetType;

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            lock (m_Prepared) m_Prepared.Add(key);
            return RuntimePreparedAssetResult.Ready(m_GpuBytes);
        }

        public void Release(RuntimeAssetResidencyKey key)
        {
            Interlocked.Increment(ref m_Releasing);
            ReleaseEntered.Set();
            try
            {
                AllowRelease.Wait();
                if (Volatile.Read(ref m_Disposed) != 0)
                {
                    Interlocked.Exchange(ref m_ReleaseAfterDispose, 1);
                }

                lock (m_Prepared) m_Prepared.Remove(key);
                Interlocked.Increment(ref m_ReleaseCount);
            }
            finally
            {
                Interlocked.Decrement(ref m_Releasing);
            }
        }

        public RuntimePreparedAssetProviderMetrics GetMetrics()
        {
            if (Volatile.Read(ref m_Releasing) != 0)
            {
                Interlocked.Exchange(ref m_MetricsWhileReleasing, 1);
            }

            int preparedCount;
            lock (m_Prepared) preparedCount = m_Prepared.Count;
            return new(
                preparedCount,
                preparedCount * m_GpuBytes,
                PendingDisposalCount: 0);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref m_Disposed, 1);
            ReleaseEntered.Dispose();
            AllowRelease.Dispose();
        }
    }

    private sealed class FailingReleasePreparedProvider : IRuntimePreparedAssetProvider, IDisposable
    {
        private readonly string m_AssetType;
        private readonly long m_GpuBytes;
        private readonly HashSet<RuntimeAssetResidencyKey> m_Prepared = new();
        private readonly Dictionary<Guid, int> m_ReleaseAttempts = new();
        private int m_PrepareCount;
        private int m_ReleaseAttemptCount;

        public FailingReleasePreparedProvider(string assetType, long gpuBytes)
        {
            m_AssetType = assetType;
            m_GpuBytes = gpuBytes;
        }

        public string ProviderId => "test.failing-release-prepared-assets";
        public bool FailReleases { get; set; } = true;
        public Guid? OnlyFailGuid { get; set; }
        public int PrepareCount => Volatile.Read(ref m_PrepareCount);
        public int ReleaseAttemptCount => Volatile.Read(ref m_ReleaseAttemptCount);

        public int GetReleaseAttemptCount(Guid guid)
        {
            lock (m_Prepared)
            {
                return m_ReleaseAttempts.GetValueOrDefault(guid);
            }
        }

        public bool Supports(string assetType) => assetType == m_AssetType;

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            Interlocked.Increment(ref m_PrepareCount);
            lock (m_Prepared) m_Prepared.Add(key);
            return RuntimePreparedAssetResult.Ready(m_GpuBytes);
        }

        public void Release(RuntimeAssetResidencyKey key)
        {
            Interlocked.Increment(ref m_ReleaseAttemptCount);
            bool fail;
            lock (m_Prepared)
            {
                m_ReleaseAttempts[key.Guid] =
                    m_ReleaseAttempts.GetValueOrDefault(key.Guid) + 1;
                fail = FailReleases &&
                    (!OnlyFailGuid.HasValue || OnlyFailGuid.Value == key.Guid);
                if (!fail) m_Prepared.Remove(key);
            }

            if (fail)
            {
                throw new InvalidOperationException($"Injected release failure for '{key}'.");
            }
        }

        public RuntimePreparedAssetProviderMetrics GetMetrics()
        {
            int preparedCount;
            lock (m_Prepared) preparedCount = m_Prepared.Count;
            return new(
                preparedCount,
                preparedCount * m_GpuBytes,
                PendingDisposalCount: 0);
        }

        public void Dispose()
        {
            lock (m_Prepared) m_Prepared.Clear();
        }
    }

    private sealed class BlockingPreparedProvider : IRuntimePreparedAssetProvider, IDisposable
    {
        private readonly string m_AssetType;
        private readonly long m_GpuBytes;
        private readonly HashSet<RuntimeAssetResidencyKey> m_Prepared = new();
        private int m_PrepareCount;
        private int m_ReleaseCount;
        private int m_ReleaseAttemptCount;
        private int m_Preparing;
        private int m_ReleaseWhilePreparing;
        private int m_MetricsWhilePreparing;

        public BlockingPreparedProvider(string assetType, long gpuBytes)
        {
            m_AssetType = assetType;
            m_GpuBytes = gpuBytes;
        }

        public string ProviderId => "test.blocking-prepared-assets";
        public ManualResetEventSlim PrepareEntered { get; } = new(false);
        public ManualResetEventSlim AllowPrepare { get; } = new(false);
        public ManualResetEventSlim MetricsEntered { get; } = new(false);
        public int PrepareCount => Volatile.Read(ref m_PrepareCount);
        public int ReleaseCount => Volatile.Read(ref m_ReleaseCount);
        public int ReleaseAttemptCount => Volatile.Read(ref m_ReleaseAttemptCount);
        public int BlockOnPrepareCount { get; set; } = 1;
        public bool FailFirstRelease { get; set; }
        public bool FailMetrics { get; set; }
        public bool ReleaseWhilePrepareIsInFlight =>
            Volatile.Read(ref m_ReleaseWhilePreparing) != 0;
        public bool MetricsWhilePrepareIsInFlight =>
            Volatile.Read(ref m_MetricsWhilePreparing) != 0;
        public int PreparedResourceCount
        {
            get
            {
                lock (m_Prepared) return m_Prepared.Count;
            }
        }

        public bool Supports(string assetType) => assetType == m_AssetType;

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            int count = Interlocked.Increment(ref m_PrepareCount);
            lock (m_Prepared) m_Prepared.Add(key);
            Interlocked.Increment(ref m_Preparing);
            try
            {
                if (count == BlockOnPrepareCount)
                {
                    PrepareEntered.Set();
                    AllowPrepare.Wait();
                }

                return RuntimePreparedAssetResult.Ready(m_GpuBytes);
            }
            finally
            {
                Interlocked.Decrement(ref m_Preparing);
            }
        }

        public void Release(RuntimeAssetResidencyKey key)
        {
            int attempt = Interlocked.Increment(ref m_ReleaseAttemptCount);
            if (FailFirstRelease && attempt == 1)
            {
                throw new InvalidOperationException(
                    $"Injected first release failure for '{key}'.");
            }

            Interlocked.Increment(ref m_ReleaseCount);
            if (Volatile.Read(ref m_Preparing) != 0)
            {
                Interlocked.Exchange(ref m_ReleaseWhilePreparing, 1);
            }

            lock (m_Prepared)
            {
                m_Prepared.Remove(key);
            }
        }

        public RuntimePreparedAssetProviderMetrics GetMetrics()
        {
            if (Volatile.Read(ref m_Preparing) != 0)
            {
                Interlocked.Exchange(ref m_MetricsWhilePreparing, 1);
            }

            MetricsEntered.Set();
            if (FailMetrics)
            {
                throw new InvalidOperationException("Injected prepared-provider metrics failure.");
            }

            int preparedCount = PreparedResourceCount;
            return new(
                preparedCount,
                preparedCount * m_GpuBytes,
                PendingDisposalCount: 0);
        }

        public void Dispose()
        {
            PrepareEntered.Dispose();
            AllowPrepare.Dispose();
            MetricsEntered.Dispose();
        }
    }

    private sealed class BlockingPublicationPreparedProvider :
        IRuntimePreparedAssetProvider,
        IDisposable
    {
        private readonly RuntimeAssetResidencyService m_Residency;
        private readonly string m_AssetType;
        private readonly RuntimeAssetResidencyKey? m_RequiredKey;
        private int m_PrepareCount;
        private int m_ReleaseCount;
        private int m_RollbackCount;
        private int m_SnapshotPublished;

        public BlockingPublicationPreparedProvider(
            RuntimeAssetResidencyService residency,
            string assetType = "Mesh",
            RuntimeAssetResidencyKey? requiredKey = null)
        {
            m_Residency = residency;
            m_AssetType = assetType;
            m_RequiredKey = requiredKey;
        }

        public string ProviderId => "test.blocking-publication-prepared-assets";
        public ManualResetEventSlim PublicationEntered { get; } = new(false);
        public ManualResetEventSlim AllowPublicationReturn { get; } = new(false);
        public int PrepareCount => Volatile.Read(ref m_PrepareCount);
        public int ReleaseCount => Volatile.Read(ref m_ReleaseCount);
        public int RollbackCount => Volatile.Read(ref m_RollbackCount);
        public bool SnapshotPublished => Volatile.Read(ref m_SnapshotPublished) != 0;
        public Action? PublicationAction { get; set; }

        public bool Supports(string assetType) => assetType == m_AssetType;

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            Interlocked.Increment(ref m_PrepareCount);
            if (!m_Residency.TryGetPreparationClaim(
                    key,
                    out RuntimeAssetPreparationClaim claim))
            {
                return RuntimePreparedAssetResult.Waiting(
                    $"Runtime asset '{key}' has no current preparation claim.");
            }

            RuntimeAssetPreparationClaim[] requiredClaims =
                Array.Empty<RuntimeAssetPreparationClaim>();
            RuntimeAssetResidencyKey[] requiredKeys = Array.Empty<RuntimeAssetResidencyKey>();
            if (m_RequiredKey is RuntimeAssetResidencyKey requiredKey)
            {
                if (!m_Residency.TryGetPreparationClaim(
                        requiredKey,
                        out RuntimeAssetPreparationClaim requiredClaim))
                {
                    return RuntimePreparedAssetResult.Waiting(
                        $"Required runtime asset '{requiredKey}' has no current preparation claim.");
                }

                requiredClaims = [requiredClaim];
                requiredKeys = [requiredKey];
            }

            try
            {
                bool committed = m_Residency.TryCommitPreparedPublication(
                    claim,
                    requiredClaims,
                    requiredKeys,
                    estimatedGpuBytes: 0,
                    () =>
                    {
                        Interlocked.Exchange(ref m_SnapshotPublished, 1);
                        PublicationAction?.Invoke();
                        PublicationEntered.Set();
                        AllowPublicationReturn.Wait();
                    },
                    out string diagnostic);
                return committed
                    ? RuntimePreparedAssetResult.Ready(estimatedGpuBytes: 0)
                    : RuntimePreparedAssetResult.Waiting(diagnostic);
            }
            catch (RuntimePreparedPublicationInvalidatedException invalidated)
            {
                Interlocked.Exchange(ref m_SnapshotPublished, 0);
                Interlocked.Increment(ref m_RollbackCount);
                return RuntimePreparedAssetResult.Waiting(invalidated.Message);
            }
        }

        public void Release(RuntimeAssetResidencyKey key)
        {
            Interlocked.Exchange(ref m_SnapshotPublished, 0);
            Interlocked.Increment(ref m_ReleaseCount);
        }

        public RuntimePreparedAssetProviderMetrics GetMetrics() => new(
            PreparedResourceCount: SnapshotPublished ? 1 : 0,
            DescriptorCount: SnapshotPublished ? 1 : 0,
            EstimatedGpuBytes: 0,
            PendingDisposalCount: 0);

        public void Dispose()
        {
            PublicationEntered.Dispose();
            AllowPublicationReturn.Dispose();
        }
    }

    private sealed class WaitFirstPreparedProvider : IRuntimePreparedAssetProvider, IDisposable
    {
        private readonly Guid m_WaitingGuid;
        private readonly long m_GpuBytes;
        private readonly HashSet<RuntimeAssetResidencyKey> m_Prepared = new();
        private bool m_Waited;
        private int m_PrepareCount;

        public WaitFirstPreparedProvider(Guid waitingGuid, long gpuBytes)
        {
            m_WaitingGuid = waitingGuid;
            m_GpuBytes = gpuBytes;
        }

        public string ProviderId => "test.wait-first-prepared-assets";
        public int PrepareCount => Volatile.Read(ref m_PrepareCount);

        public bool Supports(string assetType) => assetType == "Mesh";

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            Interlocked.Increment(ref m_PrepareCount);
            bool shouldWait;
            lock (m_Prepared)
            {
                shouldWait = key.Guid == m_WaitingGuid && !m_Waited;
                if (shouldWait) m_Waited = true;
            }

            if (shouldWait)
            {
                return RuntimePreparedAssetResult.Waiting(
                    "The first mesh is waiting on asynchronous work.");
            }

            lock (m_Prepared) m_Prepared.Add(key);
            return RuntimePreparedAssetResult.Ready(m_GpuBytes);
        }

        public void Release(RuntimeAssetResidencyKey key)
        {
            lock (m_Prepared) m_Prepared.Remove(key);
        }

        public RuntimePreparedAssetProviderMetrics GetMetrics()
        {
            int preparedCount;
            lock (m_Prepared) preparedCount = m_Prepared.Count;
            return new(
                preparedCount,
                preparedCount * m_GpuBytes,
                PendingDisposalCount: 0);
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakePreparedProvider : IRuntimePreparedAssetProvider
    {
        private readonly string m_AssetType;
        private readonly long m_GpuBytes;
        private readonly HashSet<RuntimeAssetResidencyKey> m_Prepared = new();

        public FakePreparedProvider(string assetType, long gpuBytes)
        {
            m_AssetType = assetType;
            m_GpuBytes = gpuBytes;
        }

        public string ProviderId => "test.prepared-assets";
        public int PrepareCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public Action<RuntimeAssetResidencyKey>? PrepareCallback { get; set; }
        public Action<RuntimeAssetResidencyKey>? ReleaseCallback { get; set; }

        public bool Supports(string assetType) => assetType == m_AssetType;

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            PrepareCount++;
            m_Prepared.Add(key);
            PrepareCallback?.Invoke(key);
            return RuntimePreparedAssetResult.Ready(m_GpuBytes);
        }

        public void Release(RuntimeAssetResidencyKey key)
        {
            if (m_Prepared.Remove(key)) ReleaseCount++;
            ReleaseCallback?.Invoke(key);
        }

        public RuntimePreparedAssetProviderMetrics GetMetrics() =>
            new(m_Prepared.Count, m_Prepared.Count * m_GpuBytes, PendingDisposalCount: 0);
    }
}
