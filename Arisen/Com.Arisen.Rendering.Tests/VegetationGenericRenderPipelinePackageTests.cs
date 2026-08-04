using ArisenEngine.Core.Assets;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Threading;
using ArisenEngine.Vegetation;
using ArisenEngine.Vegetation.Assets;
using ArisenEngine.Vegetation.GenericRenderPipeline;
using ArisenKernel.Services;
using Xunit;

namespace ArisenEngine.Rendering
{
    public enum GenericRenderPipelineFeatureGraphStage
    {
        DirectionalShadow = 0,
        Opaque = 1
    }

    public readonly struct GenericRenderPipelineFeatureFrameContext
    {
    }

    public readonly struct GenericRenderPipelineFeatureGraphContext
    {
    }

    public readonly struct GenericRenderPipelineFeatureSubmissionContext
    {
    }

    public interface IGenericRenderPipelineFeature
    {
        string FeatureId { get; }
        int Order { get; }
        void ConsumeExtractedFrame(in GenericRenderPipelineFeatureFrameContext context);
        void PrepareResources(in GenericRenderPipelineFeatureFrameContext context);
        void AddRenderGraphPasses(
            GenericRenderPipelineFeatureGraphStage stage,
            in GenericRenderPipelineFeatureGraphContext context);
        void OnFrameSubmitted(in GenericRenderPipelineFeatureSubmissionContext context);
        void ReleaseDeviceResources();
    }

    public interface IGenericRenderPipelineFeatureRegistry
    {
        int Count { get; }
        bool IsPipelineActive { get; }
        void Register(IGenericRenderPipelineFeature feature);
        bool IsRegistered(IGenericRenderPipelineFeature feature);
        bool Unregister(IGenericRenderPipelineFeature feature);
    }
}

namespace Com.Arisen.Rendering.Tests
{
    public sealed class VegetationGenericRenderPipelinePackageTests : IDisposable
    {
        private readonly string m_Root = Path.Combine(
            Path.GetTempPath(),
            "ArisenVegetationGenericRenderPipelinePackageTests",
            Guid.NewGuid().ToString("N"));

        [Fact]
        public void FeatureRegistrationFailureRollsBackAndRetryUnloadsEveryRegistration()
        {
            Directory.CreateDirectory(m_Root);
            var database = new TestAssetDatabase(
                AssetSourceAccessMode.Disabled,
                Path.Combine(m_Root, "Cooked"));
            using var scheduler = new TaskGraph(workerCount: 1);
            using var residency = new RuntimeAssetResidencyService(
                database,
                new RuntimeAssetResidencyBudgets(
                    MaxCpuCookedBytes: 1024 * 1024,
                    MaxPreparedGpuBytes: 1024 * 1024,
                    MaxSetupsPerFrame: 4,
                    MaxSetupMilliseconds: 10,
                    MaxInactiveResources: 0));
            var runtimeData = new VegetationRuntimeDataStore();
            var diagnostics = new VegetationDiagnosticsService();
            var previews = new VegetationAuthoringPreviewService();
            var featureRegistry = new RecordingFeatureRegistry
            {
                ThrowOnRegister = true
            };
            var services = new ServiceRegistry();
            services.RegisterService<IAssetDatabase>(database);
            services.RegisterService<IBackgroundTaskScheduler>(scheduler);
            services.RegisterService<IRuntimeAssetResidencyService>(residency);
            services.RegisterService<ArisenEngine.Rendering.IGenericRenderPipelineFeatureRegistry>(
                featureRegistry);
            services.RegisterService<IVegetationClusterDataSource>(runtimeData);
            services.RegisterService<IVegetationRuntimeDataStore>(runtimeData);
            services.RegisterService<IVegetationDiagnosticsPublisher>(diagnostics);
            services.RegisterService<IVegetationAuthoringPreviewService>(previews);
            var package = new VegetationGenericRenderPipelinePackage();

            Assert.Throws<InvalidOperationException>(() => package.OnLoad(services));

            Assert.Equal(0, featureRegistry.Count);
            Assert.False(residency.UnregisterPreparedProvider(
                VegetationPreparedAssetProvider.Id));
            Assert.False(package.HasPendingOwnership);

            featureRegistry.ThrowOnRegister = false;
            package.OnLoad(services);

            Assert.Equal(1, featureRegistry.Count);
            package.OnUnload(services);

            Assert.Equal(0, featureRegistry.Count);
            Assert.False(residency.UnregisterPreparedProvider(
                VegetationPreparedAssetProvider.Id));
            Assert.Equal(default, runtimeData.GetMetrics());
        }

        [Fact]
        public void FeatureRegistrationCollisionPreservesExistingInstanceAndAllowsRetry()
        {
            Directory.CreateDirectory(m_Root);
            var database = new TestAssetDatabase(
                AssetSourceAccessMode.Disabled,
                Path.Combine(m_Root, "Cooked"));
            using var scheduler = new TaskGraph(workerCount: 1);
            using var residency = CreateResidency(database);
            var runtimeData = new VegetationRuntimeDataStore();
            var featureRegistry = new RecordingFeatureRegistry();
            var existing = new ForeignFeature(VegetationGenericRenderPipelineFeature.Id);
            featureRegistry.Register(existing);
            ServiceRegistry services = CreateServices(
                database,
                scheduler,
                residency,
                runtimeData,
                featureRegistry);
            var package = new VegetationGenericRenderPipelinePackage();

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => package.OnLoad(services));

            Assert.Contains("already registered", failure.Message, StringComparison.Ordinal);
            Assert.True(featureRegistry.IsRegistered(existing));
            Assert.Equal(1, featureRegistry.Count);
            Assert.Equal(0, existing.ReleaseCount);
            Assert.False(package.HasPendingOwnership);
            Assert.False(residency.UnregisterPreparedProvider(
                VegetationPreparedAssetProvider.Id));

            Assert.True(featureRegistry.Unregister(existing));
            package.OnLoad(services);
            package.OnUnload(services);

            Assert.Equal(0, featureRegistry.Count);
            Assert.False(package.HasPendingOwnership);
            Assert.Equal(default, runtimeData.GetMetrics());
        }

        [Fact]
        public void UnloadCompletesProviderCleanupAfterTransientUnregisterFailure()
        {
            Directory.CreateDirectory(m_Root);
            var database = new TestAssetDatabase(
                AssetSourceAccessMode.Disabled,
                Path.Combine(m_Root, "Cooked"));
            using var scheduler = new TaskGraph(workerCount: 1);
            using var innerResidency = CreateResidency(database);
            var residency = new FailOnceResidencyService(innerResidency);
            var runtimeData = new VegetationRuntimeDataStore();
            var featureRegistry = new RecordingFeatureRegistry();
            ServiceRegistry services = CreateServices(
                database,
                scheduler,
                residency,
                runtimeData,
                featureRegistry);
            var package = new VegetationGenericRenderPipelinePackage();
            package.OnLoad(services);

            residency.UnregisterFailuresRemaining = 1;
            AggregateException failure = Assert.Throws<AggregateException>(
                () => package.OnUnload(services));

            Assert.Contains(
                failure.InnerExceptions,
                exception => exception.Message.Contains(
                    "prepared-provider unregister",
                    StringComparison.Ordinal));
            Assert.Equal(0, featureRegistry.Count);
            Assert.False(innerResidency.UnregisterPreparedProvider(
                VegetationPreparedAssetProvider.Id));
            Assert.Equal(default, runtimeData.GetMetrics());
            Assert.False(package.HasPendingOwnership);
        }

        [Fact]
        public void ResidencyShutdownFinishesDisposedProviderAfterBothUnregisterAttemptsFail()
        {
            Directory.CreateDirectory(m_Root);
            var database = new TestAssetDatabase(
                AssetSourceAccessMode.Disabled,
                Path.Combine(m_Root, "Cooked"));
            using var scheduler = new TaskGraph(workerCount: 1);
            using var innerResidency = CreateResidency(database);
            var residency = new FailOnceResidencyService(innerResidency);
            var runtimeData = new VegetationRuntimeDataStore();
            var featureRegistry = new RecordingFeatureRegistry();
            ServiceRegistry services = CreateServices(
                database,
                scheduler,
                residency,
                runtimeData,
                featureRegistry);
            var package = new VegetationGenericRenderPipelinePackage();
            package.OnLoad(services);

            residency.UnregisterFailuresRemaining = 2;
            AggregateException failure = Assert.Throws<AggregateException>(
                () => package.OnUnload(services));

            Assert.Equal(
                2,
                failure.InnerExceptions.Count(exception => exception.Message.Contains(
                    "prepared-provider unregister",
                    StringComparison.Ordinal)));
            Assert.True(package.HasPendingOwnership);
            IRuntimePreparedAssetProvider provider = Assert.IsAssignableFrom<
                IRuntimePreparedAssetProvider>(residency.RegisteredProvider);
            provider.Release(new RuntimeAssetResidencyKey(
                Guid.Parse("8d200000-0000-0000-0000-000000000001"),
                "com.arisen.test",
                VegetationAssetTypes.Species,
                VegetationSpeciesAssetCooker.RuntimeVariant));
            Assert.Equal(default, provider.GetMetrics());

            innerResidency.Dispose();

            Assert.False(innerResidency.UnregisterPreparedProvider(
                VegetationPreparedAssetProvider.Id));
            Assert.Equal(default, runtimeData.GetMetrics());
        }

        [Fact]
        public void ProviderRegistrationFailureRollsBackFeatureAndAllowsRetry()
        {
            Directory.CreateDirectory(m_Root);
            var database = new TestAssetDatabase(
                AssetSourceAccessMode.Disabled,
                Path.Combine(m_Root, "Cooked"));
            using var scheduler = new TaskGraph(workerCount: 1);
            using var innerResidency = CreateResidency(database);
            var residency = new FailOnceResidencyService(innerResidency)
            {
                FailNextRegister = true
            };
            var runtimeData = new VegetationRuntimeDataStore();
            var featureRegistry = new RecordingFeatureRegistry();
            ServiceRegistry services = CreateServices(
                database,
                scheduler,
                residency,
                runtimeData,
                featureRegistry);
            var package = new VegetationGenericRenderPipelinePackage();

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => package.OnLoad(services));

            Assert.Contains("registration failure", failure.Message, StringComparison.Ordinal);
            Assert.Equal(0, featureRegistry.Count);
            Assert.False(innerResidency.UnregisterPreparedProvider(
                VegetationPreparedAssetProvider.Id));

            package.OnLoad(services);
            package.OnUnload(services);

            Assert.False(innerResidency.UnregisterPreparedProvider(
                VegetationPreparedAssetProvider.Id));
            Assert.Equal(default, runtimeData.GetMetrics());
            Assert.False(package.HasPendingOwnership);
        }

        [Fact]
        public void PreparedProviderCollisionPreservesExistingInstanceAndAllowsRetry()
        {
            Directory.CreateDirectory(m_Root);
            var database = new TestAssetDatabase(
                AssetSourceAccessMode.Disabled,
                Path.Combine(m_Root, "Cooked"));
            using var scheduler = new TaskGraph(workerCount: 1);
            using var residency = CreateResidency(database);
            var existing = new ForeignPreparedProvider();
            residency.RegisterPreparedProvider(existing);
            var runtimeData = new VegetationRuntimeDataStore();
            var featureRegistry = new RecordingFeatureRegistry();
            ServiceRegistry services = CreateServices(
                database,
                scheduler,
                residency,
                runtimeData,
                featureRegistry);
            var package = new VegetationGenericRenderPipelinePackage();

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => package.OnLoad(services));

            Assert.Contains("already registered", failure.Message, StringComparison.Ordinal);
            Assert.True(residency.IsPreparedProviderRegistered(existing));
            Assert.Equal(0, existing.ReleaseCount);
            Assert.Equal(0, featureRegistry.Count);
            Assert.False(package.HasPendingOwnership);

            Assert.True(residency.UnregisterPreparedProvider(existing.ProviderId));
            package.OnLoad(services);
            package.OnUnload(services);

            Assert.False(residency.IsPreparedProviderRegistered(existing));
            Assert.False(residency.UnregisterPreparedProvider(
                VegetationPreparedAssetProvider.Id));
            Assert.Equal(0, featureRegistry.Count);
            Assert.False(package.HasPendingOwnership);
            Assert.Equal(default, runtimeData.GetMetrics());
        }

        private static RuntimeAssetResidencyService CreateResidency(IAssetDatabase database) =>
            new(
                database,
                new RuntimeAssetResidencyBudgets(
                    MaxCpuCookedBytes: 1024 * 1024,
                    MaxPreparedGpuBytes: 1024 * 1024,
                    MaxSetupsPerFrame: 4,
                    MaxSetupMilliseconds: 10,
                    MaxInactiveResources: 0));

        private static ServiceRegistry CreateServices(
            IAssetDatabase database,
            IBackgroundTaskScheduler scheduler,
            IRuntimeAssetResidencyService residency,
            VegetationRuntimeDataStore runtimeData,
            RecordingFeatureRegistry featureRegistry)
        {
            var diagnostics = new VegetationDiagnosticsService();
            var previews = new VegetationAuthoringPreviewService();
            var services = new ServiceRegistry();
            services.RegisterService<IAssetDatabase>(database);
            services.RegisterService<IBackgroundTaskScheduler>(scheduler);
            services.RegisterService<IRuntimeAssetResidencyService>(residency);
            services.RegisterService<ArisenEngine.Rendering.IGenericRenderPipelineFeatureRegistry>(
                featureRegistry);
            services.RegisterService<IVegetationClusterDataSource>(runtimeData);
            services.RegisterService<IVegetationRuntimeDataStore>(runtimeData);
            services.RegisterService<IVegetationDiagnosticsPublisher>(diagnostics);
            services.RegisterService<IVegetationAuthoringPreviewService>(previews);
            return services;
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

        private sealed class RecordingFeatureRegistry :
            ArisenEngine.Rendering.IGenericRenderPipelineFeatureRegistry
        {
            private ArisenEngine.Rendering.IGenericRenderPipelineFeature? m_Feature;

            public bool ThrowOnRegister { get; set; }

            public int Count => m_Feature == null ? 0 : 1;

            public bool IsPipelineActive => false;

            public void Register(ArisenEngine.Rendering.IGenericRenderPipelineFeature feature)
            {
                ArgumentNullException.ThrowIfNull(feature);
                if (m_Feature != null)
                {
                    throw new InvalidOperationException("A feature is already registered.");
                }

                m_Feature = feature;
                if (ThrowOnRegister)
                {
                    throw new InvalidOperationException(
                        "Injected post-commit feature registration failure.");
                }
            }

            public bool IsRegistered(
                ArisenEngine.Rendering.IGenericRenderPipelineFeature feature) =>
                ReferenceEquals(m_Feature, feature);

            public bool Unregister(ArisenEngine.Rendering.IGenericRenderPipelineFeature feature)
            {
                if (m_Feature == null)
                {
                    return false;
                }

                if (!ReferenceEquals(m_Feature, feature))
                {
                    throw new InvalidOperationException(
                        $"Feature ID '{feature.FeatureId}' is registered to another instance.");
                }

                m_Feature = null;
                return true;
            }
        }

        private sealed class ForeignFeature :
            ArisenEngine.Rendering.IGenericRenderPipelineFeature
        {
            public ForeignFeature(string featureId)
            {
                FeatureId = featureId;
            }

            public string FeatureId { get; }
            public int Order => 0;
            public int ReleaseCount { get; private set; }

            public void ConsumeExtractedFrame(
                in ArisenEngine.Rendering.GenericRenderPipelineFeatureFrameContext context)
            {
            }

            public void PrepareResources(
                in ArisenEngine.Rendering.GenericRenderPipelineFeatureFrameContext context)
            {
            }

            public void AddRenderGraphPasses(
                ArisenEngine.Rendering.GenericRenderPipelineFeatureGraphStage stage,
                in ArisenEngine.Rendering.GenericRenderPipelineFeatureGraphContext context)
            {
            }

            public void OnFrameSubmitted(
                in ArisenEngine.Rendering.GenericRenderPipelineFeatureSubmissionContext context)
            {
            }

            public void ReleaseDeviceResources()
            {
                ReleaseCount++;
            }
        }

        private sealed class ForeignPreparedProvider : IRuntimePreparedAssetProvider
        {
            public string ProviderId => VegetationPreparedAssetProvider.Id;
            public int ReleaseCount { get; private set; }

            public bool Supports(string assetType) => false;

            public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key) =>
                throw new InvalidOperationException("Foreign collision provider cannot prepare assets.");

            public void Release(RuntimeAssetResidencyKey key)
            {
                ReleaseCount++;
            }

            public RuntimePreparedAssetProviderMetrics GetMetrics() => default;
        }

        private sealed class FailOnceResidencyService :
            IRuntimeAssetResidencyService
        {
            private readonly RuntimeAssetResidencyService m_Inner;

            public FailOnceResidencyService(RuntimeAssetResidencyService inner)
            {
                m_Inner = inner;
            }

            public bool FailNextRegister { get; set; }
            public int UnregisterFailuresRemaining { get; set; }
            public IRuntimePreparedAssetProvider? RegisteredProvider { get; private set; }

            public RuntimeAssetResidencyBudgets Budgets => m_Inner.Budgets;

            public RuntimeAssetResidencyLease AcquireSceneDependencies(
                RuntimeAssetResidencyOwnerId owner,
                IReadOnlyList<CookedSceneDependency> dependencies,
                bool pinned,
                CancellationToken cancellationToken = default) =>
                m_Inner.AcquireSceneDependencies(owner, dependencies, pinned, cancellationToken);

            public void RegisterPreparedProvider(IRuntimePreparedAssetProvider provider)
            {
                m_Inner.RegisterPreparedProvider(provider);
                RegisteredProvider = provider;
                if (FailNextRegister)
                {
                    FailNextRegister = false;
                    throw new InvalidOperationException(
                        "Injected post-commit prepared-provider registration failure.");
                }
            }

            public bool IsPreparedProviderRegistered(IRuntimePreparedAssetProvider provider) =>
                m_Inner.IsPreparedProviderRegistered(provider);

            public bool UnregisterPreparedProvider(string providerId)
            {
                if (UnregisterFailuresRemaining > 0)
                {
                    UnregisterFailuresRemaining--;
                    throw new InvalidOperationException(
                        "Injected prepared-provider unregister failure.");
                }

                bool removed = m_Inner.UnregisterPreparedProvider(providerId);
                if (RegisteredProvider != null &&
                    string.Equals(
                        RegisteredProvider.ProviderId,
                        providerId,
                        StringComparison.Ordinal))
                {
                    RegisteredProvider = null;
                }

                return removed;
            }

            public bool InvalidatePreparedProvider(string providerId, string diagnostic) =>
                m_Inner.InvalidatePreparedProvider(providerId, diagnostic);

            public bool TryGetPreparationClaim(
                RuntimeAssetResidencyKey key,
                out RuntimeAssetPreparationClaim claim) =>
                m_Inner.TryGetPreparationClaim(key, out claim);

            public bool TryBindPreparationDependencies(
                in RuntimeAssetPreparationClaim claim,
                IReadOnlyList<RuntimeAssetResidencyKey> canonicalRequiredKeys,
                out string diagnostic) =>
                m_Inner.TryBindPreparationDependencies(
                    claim,
                    canonicalRequiredKeys,
                    out diagnostic);

            public bool TryCommitPreparedPublication(
                in RuntimeAssetPreparationClaim claim,
                IReadOnlyList<RuntimeAssetPreparationClaim> canonicalRequiredClaims,
                IReadOnlyList<RuntimeAssetResidencyKey> canonicalRequiredKeys,
                long estimatedGpuBytes,
                Action publish,
                out string diagnostic) =>
                m_Inner.TryCommitPreparedPublication(
                    claim,
                    canonicalRequiredClaims,
                    canonicalRequiredKeys,
                    estimatedGpuBytes,
                    publish,
                    out diagnostic);

            public void ProcessAtFrameBoundary() => m_Inner.ProcessAtFrameBoundary();

            public IReadOnlyList<RuntimeAssetResidencySnapshot> GetResources() =>
                m_Inner.GetResources();

            public RuntimeAssetResidencyMetrics GetMetrics() => m_Inner.GetMetrics();
        }
    }
}
