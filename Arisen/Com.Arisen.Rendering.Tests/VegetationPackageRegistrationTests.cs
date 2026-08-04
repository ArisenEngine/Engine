using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation;
using ArisenKernel.Lifecycle;
using ArisenKernel.Services;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

[Collection(SceneComponentExtensionRegistryCollection.Name)]
public sealed class VegetationPackageRegistrationTests : IDisposable
{
    private readonly string m_Root;

    public VegetationPackageRegistrationTests()
    {
        EngineKernel.Instance.Reset();
        m_Root = Path.Combine(
            Path.GetTempPath(),
            "ArisenVegetationPackageRegistrationTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(m_Root);
    }

    [Fact]
    public void FailedLoadRollsBackExternalRegistrationsAndAllowsRetry()
    {
        SceneComponentSchemaInfo[] baseline =
            SceneComponentExtensionRegistry.Shared.GetRegistrations().ToArray();
        var package = new VegetationPackage();
        var firstCookerRegistry = new RuntimeAssetCookerRegistry();
        ServiceRegistry firstServices = ConfigureKernel(firstCookerRegistry);
        firstServices.RegisterService<IVegetationClusterDataSource>(
            new VegetationRuntimeDataStore());

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            package.OnLoad(firstServices));

        Assert.Contains(
            nameof(IVegetationClusterDataSource),
            failure.Message,
            StringComparison.Ordinal);
        Assert.Empty(firstCookerRegistry.GetRegistrations());
        Assert.Equal(
            baseline,
            SceneComponentExtensionRegistry.Shared.GetRegistrations());
        Assert.False(package.HasPendingOwnership);

        EngineKernel.Instance.Reset();
        var retryCookerRegistry = new RuntimeAssetCookerRegistry();
        ServiceRegistry retryServices = ConfigureKernel(retryCookerRegistry);

        package.OnLoad(retryServices);

        Assert.NotEmpty(retryCookerRegistry.GetRegistrations());
        Assert.Contains(
            SceneComponentExtensionRegistry.Shared.GetRegistrations(),
            registration => registration.TypeId == VegetationClusterSceneComponentCodec.TypeId);
        Assert.True(package.HasPendingOwnership);

        package.OnUnload(retryServices);

        Assert.Empty(retryCookerRegistry.GetRegistrations());
        Assert.Equal(
            baseline,
            SceneComponentExtensionRegistry.Shared.GetRegistrations());
        Assert.False(package.HasPendingOwnership);
    }

    [Fact]
    public void OnUnloadAggregatesRegistrationFailuresAndRetryReleasesPendingOwnership()
    {
        SceneComponentSchemaInfo[] baseline =
            SceneComponentExtensionRegistry.Shared.GetRegistrations().ToArray();
        var innerCookerRegistry = new RuntimeAssetCookerRegistry();
        var cookerRegistry = new FailOnceRuntimeAssetCookerRegistry(innerCookerRegistry);
        var sceneRegistry = new FailOnceSceneComponentRegistry(
            SceneComponentExtensionRegistry.Shared);
        ServiceRegistry services = ConfigureKernel(cookerRegistry, sceneRegistry);
        var package = new VegetationPackage();
        package.OnLoad(services);

        cookerRegistry.FailNextUnregister = true;
        sceneRegistry.FailNextUnregister = true;
        AggregateException failure = Assert.Throws<AggregateException>(
            () => package.OnUnload(services));

        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.Contains(
            failure.InnerExceptions,
            error => error.Message.Contains(
                "scene-component codec unregister",
                StringComparison.Ordinal));
        Assert.Contains(
            failure.InnerExceptions,
            error => error.Message.Contains(
                "runtime asset cooker unregister",
                StringComparison.Ordinal));
        Assert.True(package.HasPendingOwnership);
        Assert.NotEmpty(innerCookerRegistry.GetRegistrations());
        Assert.Contains(
            SceneComponentExtensionRegistry.Shared.GetRegistrations(),
            registration => registration.TypeId == VegetationClusterSceneComponentCodec.TypeId);

        package.OnUnload(services);

        Assert.False(package.HasPendingOwnership);
        Assert.Empty(innerCookerRegistry.GetRegistrations());
        Assert.Equal(
            baseline,
            SceneComponentExtensionRegistry.Shared.GetRegistrations());
        Assert.Equal(2, cookerRegistry.UnregisterAttemptCount);
        Assert.Equal(2, sceneRegistry.UnregisterAttemptCount);

        package.OnUnload(services);
        Assert.Equal(2, cookerRegistry.UnregisterAttemptCount);
        Assert.Equal(2, sceneRegistry.UnregisterAttemptCount);
    }

    [Fact]
    public void LoadRollbackRemovesRegistrationsThatThrowAfterCommit()
    {
        SceneComponentSchemaInfo[] baseline =
            SceneComponentExtensionRegistry.Shared.GetRegistrations().ToArray();
        var innerCookerRegistry = new RuntimeAssetCookerRegistry();
        var cookerRegistry = new FailOnceRuntimeAssetCookerRegistry(innerCookerRegistry)
        {
            FailNextRegister = true
        };
        var sceneRegistry = new FailOnceSceneComponentRegistry(
            SceneComponentExtensionRegistry.Shared);
        ServiceRegistry services = ConfigureKernel(cookerRegistry, sceneRegistry);
        var package = new VegetationPackage();

        InvalidOperationException cookerFailure = Assert.Throws<InvalidOperationException>(
            () => package.OnLoad(services));

        Assert.Contains("post-commit", cookerFailure.Message, StringComparison.Ordinal);
        Assert.False(package.HasPendingOwnership);
        Assert.Empty(innerCookerRegistry.GetRegistrations());
        Assert.Equal(
            baseline,
            SceneComponentExtensionRegistry.Shared.GetRegistrations());

        sceneRegistry.FailNextRegister = true;
        InvalidOperationException sceneFailure = Assert.Throws<InvalidOperationException>(
            () => package.OnLoad(services));

        Assert.Contains("post-commit", sceneFailure.Message, StringComparison.Ordinal);
        Assert.False(package.HasPendingOwnership);
        Assert.Empty(innerCookerRegistry.GetRegistrations());
        Assert.Equal(
            baseline,
            SceneComponentExtensionRegistry.Shared.GetRegistrations());

        package.OnLoad(services);
        package.OnUnload(services);

        Assert.False(package.HasPendingOwnership);
        Assert.Empty(innerCookerRegistry.GetRegistrations());
        Assert.Equal(
            baseline,
            SceneComponentExtensionRegistry.Shared.GetRegistrations());
    }

    public void Dispose()
    {
        EngineKernel.Instance.Reset();
        try
        {
            if (Directory.Exists(m_Root)) Directory.Delete(m_Root, recursive: true);
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private ServiceRegistry ConfigureKernel(
        IRuntimeAssetCookerRegistry cookerRegistry,
        ISceneComponentExtensionRegistry? sceneComponentRegistry = null)
    {
        var services = (ServiceRegistry)EngineKernel.Instance.Services;
        var assetDatabase = new TestAssetDatabase(
            AssetSourceAccessMode.Diagnostic,
            Path.Combine(m_Root, "Cooked"));
        services.RegisterService<IAssetDatabase>(assetDatabase);
        services.RegisterService<IRuntimeAssetCookerRegistry>(cookerRegistry);
        services.RegisterService<ISceneComponentExtensionRegistry>(
            sceneComponentRegistry ?? SceneComponentExtensionRegistry.Shared);
        services.RegisterService<IRuntimeSceneService>(
            new RuntimeSceneService(assetDatabase, new EntityManager()));
        return services;
    }

    private sealed class FailOnceRuntimeAssetCookerRegistry : IRuntimeAssetCookerRegistry
    {
        private readonly IRuntimeAssetCookerRegistry m_Inner;

        public FailOnceRuntimeAssetCookerRegistry(IRuntimeAssetCookerRegistry inner)
        {
            m_Inner = inner;
        }

        public bool FailNextUnregister { get; set; }
        public bool FailNextRegister { get; set; }
        public int UnregisterAttemptCount { get; private set; }

        public void RegisterCooker(IRuntimeAssetCooker cooker)
        {
            m_Inner.RegisterCooker(cooker);
            if (FailNextRegister)
            {
                FailNextRegister = false;
                throw new InvalidOperationException(
                    "Injected post-commit vegetation cooker registration failure.");
            }
        }

        public bool UnregisterCooker(IRuntimeAssetCooker cooker)
        {
            UnregisterAttemptCount++;
            if (FailNextUnregister)
            {
                FailNextUnregister = false;
                throw new InvalidOperationException(
                    "Injected vegetation cooker unregister failure.");
            }

            return m_Inner.UnregisterCooker(cooker);
        }

        public bool TryGetCooker(string assetType, out IRuntimeAssetCooker cooker) =>
            m_Inner.TryGetCooker(assetType, out cooker!);

        public IReadOnlyCollection<RuntimeAssetCookerRegistration> GetRegistrations() =>
            m_Inner.GetRegistrations();
    }

    private sealed class FailOnceSceneComponentRegistry :
        ISceneComponentExtensionRegistry
    {
        private readonly ISceneComponentExtensionRegistry m_Inner;

        public FailOnceSceneComponentRegistry(ISceneComponentExtensionRegistry inner)
        {
            m_Inner = inner;
        }

        public bool FailNextUnregister { get; set; }
        public bool FailNextRegister { get; set; }
        public int UnregisterAttemptCount { get; private set; }

        public void Register(ISceneComponentExtensionCodec codec)
        {
            m_Inner.Register(codec);
            if (FailNextRegister)
            {
                FailNextRegister = false;
                throw new InvalidOperationException(
                    "Injected post-commit vegetation scene-component registration failure.");
            }
        }

        public bool Unregister(ISceneComponentExtensionCodec codec)
        {
            UnregisterAttemptCount++;
            if (FailNextUnregister)
            {
                FailNextUnregister = false;
                throw new InvalidOperationException(
                    "Injected vegetation scene-component unregister failure.");
            }

            return m_Inner.Unregister(codec);
        }

        public IReadOnlyList<SceneComponentSchemaInfo> GetRegistrations() =>
            m_Inner.GetRegistrations();
    }
}
