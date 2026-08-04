using System.Diagnostics;
using System.Reflection;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.ECS.Lifecycle;
using ArisenEngine.Resources;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Threading;
using ArisenKernel.Lifecycle;
using ArisenKernel.Services;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

[Collection(SceneComponentExtensionRegistryCollection.Name)]
public sealed class ResourcesPackageTests
{
    private const string PackageId = "com.arisen.test";
    private const string ResourcesPackageId = "com.arisen.resources";

    [Fact]
    public void FailedLoadRollsBackCookersAndAllowsRetry()
    {
        EngineKernel.Instance.Reset();
        using var taskGraph = new TaskGraph(workerCount: 1);
        var serviceRegistry = (ServiceRegistry)EngineKernel.Instance.Services;
        var services = new FailOnceServiceRegistry(
            serviceRegistry,
            typeof(IRuntimeSmokeScenarioRegistry));
        var cookerRegistry = new RuntimeAssetCookerRegistry();
        serviceRegistry.RegisterService<IRuntimeAssetCookerRegistry>(cookerRegistry);
        serviceRegistry.RegisterService<IBackgroundTaskScheduler>(taskGraph);
        EngineKernel.Instance.RegisterSubsystem(new SceneSubsystem());
        var package = new ResourcesPackage();
        bool loaded = false;
        try
        {
            InvalidOperationException failure;
            using (serviceRegistry.BeginPackageRegistration(ResourcesPackageId))
            {
                failure = Assert.Throws<InvalidOperationException>(
                    () => package.OnLoad(services));
            }

            Assert.Contains(
                nameof(IRuntimeSmokeScenarioRegistry),
                failure.Message,
                StringComparison.Ordinal);
            Assert.Empty(cookerRegistry.GetRegistrations());
            Assert.Null(GetField<IAssetDatabase>(package, "m_AssetDatabase"));
            Assert.Null(GetField<RuntimeSceneService>(package, "m_RuntimeSceneService"));
            Assert.Null(GetField<RuntimeWorldStreamingService>(
                package,
                "m_RuntimeWorldStreamingService"));
            Assert.Null(GetField<RuntimeAssetResidencyService>(
                package,
                "m_RuntimeAssetResidencyService"));
            Assert.Null(GetField<WorldOriginService>(package, "m_WorldOriginService"));
            Assert.Null(GetField<RuntimeSmokeScenarioRegistry>(
                package,
                "m_SmokeScenarioRegistry"));
            Assert.Null(GetField<WorldStreamingSmokeScenarioProvider>(
                package,
                "m_WorldStreamingSmokeProvider"));
            Assert.Null(GetField<IRuntimeAssetCookerRegistry>(
                package,
                "m_RuntimeAssetCookerRegistry"));
            Assert.False(package.HasPendingOwnership);
            serviceRegistry.UnregisterServicesProvidedByPackage(ResourcesPackageId);

            using (serviceRegistry.BeginPackageRegistration(ResourcesPackageId))
            {
                package.OnLoad(services);
            }
            loaded = true;

            Assert.Equal(2, cookerRegistry.GetRegistrations().Count);
            Assert.True(package.HasPendingOwnership);
            package.OnUnload(services);
            loaded = false;

            Assert.Empty(cookerRegistry.GetRegistrations());
            Assert.False(package.HasPendingOwnership);
        }
        finally
        {
            if (loaded)
            {
                try
                {
                    package.OnUnload(services);
                }
                catch
                {
                    // Best-effort cleanup after an assertion failure.
                }
            }

            EngineKernel.Instance.Reset();
        }
    }

    [Fact]
    public void CookerUnloadFailureRetainsOnlyFailedOwnershipForRetry()
    {
        var database = new TestAssetDatabase(
            AssetSourceAccessMode.Disabled,
            Path.Combine(Path.GetTempPath(), "ArisenResourcesPackageCookerTests"));
        var sceneCooker = new SceneRuntimeAssetCooker(database);
        var worldCooker = new WorldRuntimeAssetCooker(database);
        var cookerRegistry = new FailOnceUnregisterCookerRegistry(worldCooker);
        cookerRegistry.RegisterCooker(sceneCooker);
        cookerRegistry.RegisterCooker(worldCooker);
        var package = new ResourcesPackage();
        SetField(package, "m_RuntimeAssetCookerRegistry", cookerRegistry);
        SetField(package, "m_SceneRuntimeAssetCooker", sceneCooker);
        SetField(package, "m_WorldRuntimeAssetCooker", worldCooker);

        AggregateException failure = Assert.Throws<AggregateException>(
            () => package.OnUnload(null!));

        Assert.Single(failure.InnerExceptions);
        Assert.Contains(
            "world runtime asset cooker unregister",
            failure.InnerExceptions[0].Message,
            StringComparison.Ordinal);
        RuntimeAssetCookerRegistration remaining = Assert.Single(
            cookerRegistry.GetRegistrations());
        Assert.Equal(worldCooker.ProviderId, remaining.ProviderId);
        Assert.Null(GetField<SceneRuntimeAssetCooker>(
            package,
            "m_SceneRuntimeAssetCooker"));
        Assert.Same(
            worldCooker,
            GetField<WorldRuntimeAssetCooker>(package, "m_WorldRuntimeAssetCooker"));
        Assert.Same(
            cookerRegistry,
            GetField<IRuntimeAssetCookerRegistry>(package, "m_RuntimeAssetCookerRegistry"));
        Assert.Equal(1, cookerRegistry.GetUnregisterAttemptCount(sceneCooker));
        Assert.Equal(1, cookerRegistry.GetUnregisterAttemptCount(worldCooker));
        Assert.True(package.HasPendingOwnership);

        package.OnUnload(null!);

        Assert.Empty(cookerRegistry.GetRegistrations());
        Assert.Null(GetField<WorldRuntimeAssetCooker>(
            package,
            "m_WorldRuntimeAssetCooker"));
        Assert.Null(GetField<IRuntimeAssetCookerRegistry>(
            package,
            "m_RuntimeAssetCookerRegistry"));
        Assert.Equal(1, cookerRegistry.GetUnregisterAttemptCount(sceneCooker));
        Assert.Equal(2, cookerRegistry.GetUnregisterAttemptCount(worldCooker));
        Assert.False(package.HasPendingOwnership);

        package.OnUnload(null!);
        Assert.Equal(1, cookerRegistry.GetUnregisterAttemptCount(sceneCooker));
        Assert.Equal(2, cookerRegistry.GetUnregisterAttemptCount(worldCooker));
    }

    [Fact]
    public void OnUnloadRetainsDependenciesAndRetriesOnlyFailedStages()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "ArisenResourcesPackageTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        RuntimeAssetResidencyService? residency = null;
        TaskGraph? taskGraph = null;
        var provider = new FailOnceReleasePreparedProvider();
        try
        {
            var innerDatabase = new TestAssetDatabase(
                AssetSourceAccessMode.Diagnostic,
                Path.Combine(root, "Cooked"));
            Guid meshGuid = AddCookedMesh(innerDatabase, root);
            innerDatabase.UseReadOnlyRuntime();
            var database = new FailOnceReleaseAllAssetDatabase(innerDatabase);
            residency = new RuntimeAssetResidencyService(database);
            residency.RegisterPreparedProvider(provider);
            RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
                RuntimeAssetResidencyOwnerId.Cell(
                    Guid.Parse("8c100000-0000-0000-0000-000000000001"),
                    new WorldCellId(Guid.Parse("8c100000-0000-0000-0000-000000000002")),
                    generation: 1),
                [new CookedSceneDependency(meshGuid, PackageId, "Mesh", Required: true)],
                pinned: false);
            residency.ProcessAtFrameBoundary();
            Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);

            var sceneService = new RuntimeSceneService(database, new EntityManager());
            taskGraph = new TaskGraph(workerCount: 1);
            var originService = new WorldOriginService();
            var worldStreamingService = new RuntimeWorldStreamingService(
                database,
                sceneService,
                taskGraph,
                residencyService: residency,
                originService: originService);
            var smokeRegistry = new RuntimeSmokeScenarioRegistry();
            var smokeProvider = new WorldStreamingSmokeScenarioProvider(
                worldStreamingService,
                sceneService,
                residency,
                originService,
                database,
                taskGraph);
            var package = new ResourcesPackage();
            SetField(package, "m_AssetDatabase", database);
            SetField(package, "m_RuntimeSceneService", sceneService);
            SetField(package, "m_RuntimeWorldStreamingService", worldStreamingService);
            SetField(package, "m_RuntimeAssetResidencyService", residency);
            SetField(package, "m_WorldOriginService", originService);
            SetField(package, "m_SmokeScenarioRegistry", smokeRegistry);
            SetField(package, "m_WorldStreamingSmokeProvider", smokeProvider);

            AggregateException failure = Assert.Throws<AggregateException>(
                () => package.OnUnload(null!));

            InvalidOperationException residencyFailure = Assert.IsType<InvalidOperationException>(
                Assert.Single(failure.InnerExceptions));
            Assert.Contains(
                "runtime asset residency disposal",
                residencyFailure.Message,
                StringComparison.Ordinal);
            Assert.Null(GetField<RuntimeWorldStreamingService>(
                package,
                "m_RuntimeWorldStreamingService"));
            Assert.Null(GetField<RuntimeSceneService>(package, "m_RuntimeSceneService"));
            Assert.Null(GetField<WorldOriginService>(package, "m_WorldOriginService"));
            Assert.Null(GetField<RuntimeSmokeScenarioRegistry>(
                package,
                "m_SmokeScenarioRegistry"));
            Assert.Null(GetField<WorldStreamingSmokeScenarioProvider>(
                package,
                "m_WorldStreamingSmokeProvider"));
            Assert.Same(
                residency,
                GetField<RuntimeAssetResidencyService>(
                    package,
                    "m_RuntimeAssetResidencyService"));
            Assert.Same(database, GetField<IAssetDatabase>(package, "m_AssetDatabase"));
            Assert.Equal(1, provider.ReleaseAttemptCount);
            Assert.Equal(0, database.ReleaseAllAttemptCount);
            Assert.True(package.HasPendingOwnership);

            AggregateException databaseFailure = Assert.Throws<AggregateException>(
                () => package.OnUnload(null!));

            InvalidOperationException releaseAllFailure = Assert.IsType<InvalidOperationException>(
                Assert.Single(databaseFailure.InnerExceptions));
            Assert.Contains(
                "asset database cooked-handle release",
                releaseAllFailure.Message,
                StringComparison.Ordinal);
            Assert.Null(GetField<RuntimeAssetResidencyService>(
                package,
                "m_RuntimeAssetResidencyService"));
            Assert.Same(database, GetField<IAssetDatabase>(package, "m_AssetDatabase"));
            Assert.Equal(2, provider.ReleaseAttemptCount);
            Assert.Equal(1, database.ReleaseAllAttemptCount);
            Assert.True(package.HasPendingOwnership);

            package.OnUnload(null!);

            Assert.Equal(2, provider.ReleaseAttemptCount);
            Assert.Equal(2, database.ReleaseAllAttemptCount);
            Assert.Null(GetField<IAssetDatabase>(package, "m_AssetDatabase"));
            Assert.False(package.HasPendingOwnership);

            package.OnUnload(null!);
            Assert.Equal(2, provider.ReleaseAttemptCount);
            Assert.Equal(2, database.ReleaseAllAttemptCount);
            lease.Dispose();
        }
        finally
        {
            provider.AllowRelease = true;
            try
            {
                residency?.Dispose();
            }
            catch
            {
                // Best-effort cleanup after an assertion failure.
            }

            taskGraph?.Dispose();
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup after an assertion failure.
            }
        }
    }

    [Fact]
    public void WorldShutdownFailureRetainsItsPackageDependenciesForRetry()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "ArisenResourcesPackageWorldTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        RuntimeAssetResidencyService? residency = null;
        TaskGraph? taskGraph = null;
        ResourcesPackage? package = null;
        var provider = new FailOnceReleasePreparedProvider();
        try
        {
            var database = new TestAssetDatabase(
                AssetSourceAccessMode.Diagnostic,
                Path.Combine(root, "Cooked"));
            Guid meshGuid = AddCookedMesh(database, root);
            Guid materialGuid = AddCookedMaterial(database, root);
            AssetRef<WorldSourceAsset> worldAsset = AddStreamingWorld(
                database,
                root,
                meshGuid,
                materialGuid);
            residency = new RuntimeAssetResidencyService(database);
            residency.RegisterPreparedProvider(provider);
            var sceneService = new RuntimeSceneService(database, new EntityManager());
            taskGraph = new TaskGraph(workerCount: 1);
            var originService = new WorldOriginService();
            var worldStreamingService = new RuntimeWorldStreamingService(
                database,
                sceneService,
                taskGraph,
                residencyService: residency,
                originService: originService);

            RuntimeWorldLoadResult loaded = worldStreamingService.LoadWorld(worldAsset);
            Assert.True(loaded.Success, loaded.Diagnostic);
            PumpWorldUntil(
                worldStreamingService,
                () => worldStreamingService.ActiveWorldAsset == worldAsset);
            Assert.NotNull(sceneService.ActiveScene);
            Assert.All(
                residency.GetResources(),
                resource => Assert.Equal(RuntimePreparedAssetState.Ready, resource.PreparedState));

            package = new ResourcesPackage();
            SetField(package, "m_AssetDatabase", database);
            SetField(package, "m_RuntimeSceneService", sceneService);
            SetField(package, "m_RuntimeWorldStreamingService", worldStreamingService);
            SetField(package, "m_RuntimeAssetResidencyService", residency);
            SetField(package, "m_WorldOriginService", originService);

            AggregateException failure = Assert.Throws<AggregateException>(
                () => package.OnUnload(null!));

            InvalidOperationException worldFailure = Assert.IsType<InvalidOperationException>(
                Assert.Single(failure.InnerExceptions));
            Assert.Contains(
                "runtime world streaming shutdown",
                worldFailure.Message,
                StringComparison.Ordinal);
            Assert.Same(
                worldStreamingService,
                GetField<RuntimeWorldStreamingService>(
                    package,
                    "m_RuntimeWorldStreamingService"));
            Assert.Same(
                sceneService,
                GetField<RuntimeSceneService>(package, "m_RuntimeSceneService"));
            Assert.Same(
                residency,
                GetField<RuntimeAssetResidencyService>(
                    package,
                    "m_RuntimeAssetResidencyService"));
            Assert.Same(database, GetField<IAssetDatabase>(package, "m_AssetDatabase"));
            Assert.Same(
                originService,
                GetField<WorldOriginService>(package, "m_WorldOriginService"));
            Assert.NotNull(sceneService.ActiveScene);
            Assert.False(residency.IsDisposed);
            Assert.True(package.HasPendingOwnership);
            int releaseAttemptsAfterFailure = provider.ReleaseAttemptCount;

            package.OnUnload(null!);

            Assert.True(residency.IsDisposed);
            Assert.Null(sceneService.ActiveScene);
            Assert.True(provider.ReleaseAttemptCount > releaseAttemptsAfterFailure);
            Assert.Null(GetField<RuntimeWorldStreamingService>(
                package,
                "m_RuntimeWorldStreamingService"));
            Assert.Null(GetField<RuntimeSceneService>(package, "m_RuntimeSceneService"));
            Assert.Null(GetField<RuntimeAssetResidencyService>(
                package,
                "m_RuntimeAssetResidencyService"));
            Assert.Null(GetField<IAssetDatabase>(package, "m_AssetDatabase"));
            Assert.Null(GetField<WorldOriginService>(package, "m_WorldOriginService"));
            Assert.False(package.HasPendingOwnership);
        }
        finally
        {
            provider.AllowRelease = true;
            try
            {
                package?.OnUnload(null!);
            }
            catch
            {
                // Best-effort cleanup after an assertion failure.
            }

            try
            {
                residency?.Dispose();
            }
            catch
            {
                // Best-effort cleanup after an assertion failure.
            }

            taskGraph?.Dispose();
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup after an assertion failure.
            }
        }
    }

    private static Guid AddCookedMesh(TestAssetDatabase database, string root)
    {
        Guid guid = Guid.Parse("8c100000-0000-0000-0000-000000000003");
        AddCookedAsset(
            database,
            root,
            guid,
            "Mesh",
            RuntimeAssetVariantPolicy.StaticMesh);
        return guid;
    }

    private static Guid AddCookedMaterial(TestAssetDatabase database, string root)
    {
        Guid guid = Guid.Parse("8c100000-0000-0000-0000-000000000004");
        AddCookedAsset(
            database,
            root,
            guid,
            "Material",
            RuntimeAssetVariantPolicy.Material);
        return guid;
    }

    private static void AddCookedAsset(
        TestAssetDatabase database,
        string root,
        Guid guid,
        string assetType,
        string variant)
    {
        string source = Path.Combine(root, guid.ToString("N") + ".source");
        string cooked = Path.Combine(root, guid.ToString("N") + ".cooked");
        File.WriteAllText(source, "source");
        File.WriteAllBytes(cooked, [1, 2, 3, 4]);
        database.AddAsset(guid, assetType, source, PackageId);
        database.RegisterCookedArtifact(new CookedAssetRecord(
            guid,
            assetType,
            variant,
            cooked,
            4,
            File.GetLastWriteTimeUtc(cooked)));
    }

    private static AssetRef<WorldSourceAsset> AddStreamingWorld(
        TestAssetDatabase database,
        string root,
        Guid meshGuid,
        Guid materialGuid)
    {
        Guid worldGuid = Guid.Parse("8c100000-0000-0000-0000-000000000010");
        Guid persistentSceneGuid = Guid.Parse("8c100000-0000-0000-0000-000000000011");
        Guid cellSceneGuid = Guid.Parse("8c100000-0000-0000-0000-000000000012");
        Guid persistentEntityGuid = Guid.Parse("8c100000-0000-0000-0000-000000000013");
        Guid cellEntityGuid = Guid.Parse("8c100000-0000-0000-0000-000000000014");
        string persistentPath = Path.Combine(root, "Persistent.arisenscene");
        string cellPath = Path.Combine(root, "Cell.arisenscene");
        string worldPath = Path.Combine(root, "Streaming.arisenworld");
        File.WriteAllText(persistentPath, $$"""
            Version: 2
            Name: Persistent
            ComponentSchemas:
            - TypeId: 1
              Name: Transform
              Version: 1
              Required: true
            - TypeId: 3
              Name: MeshRenderer
              Version: 1
              Required: true
            Entities:
            - Guid: {{persistentEntityGuid:D}}
              Name: Persistent
              Transform:
                Position: { X: 0, Y: 0, Z: 0 }
                Rotation: { X: 0, Y: 0, Z: 0, W: 1 }
                Scale: { X: 1, Y: 1, Z: 1 }
              MeshRenderer:
                Mesh: { Guid: {{meshGuid:D}}, PackageId: {{PackageId}} }
                Material: { Guid: {{materialGuid:D}}, PackageId: {{PackageId}} }
            """);
        File.WriteAllText(cellPath, $$"""
            Version: 2
            Name: Cell
            ComponentSchemas:
            - TypeId: 1
              Name: Transform
              Version: 1
              Required: true
            Entities:
            - Guid: {{cellEntityGuid:D}}
              Name: Cell
              Transform:
                Position: { X: 0, Y: 0, Z: 0 }
                Rotation: { X: 0, Y: 0, Z: 0, W: 1 }
                Scale: { X: 1, Y: 1, Z: 1 }
            """);
        File.WriteAllText(worldPath, $$"""
            Version: 1
            WorldGuid: {{worldGuid:D}}
            Name: Resources Package World
            PersistentScene:
              Guid: {{persistentSceneGuid:D}}
              PackageId: {{PackageId}}
            Partition:
              Origin: { X: 0, Y: 0, Z: 0 }
              CellSize: { X: 100, Y: 100, Z: 100 }
              LoadRadius: 0
              UnloadHysteresis: 0
              MaxActiveCells: 1
            Policy:
              UnresolvedReferences: KeepUnresolved
              UnloadedTargets: ClearAndLateResolve
              DependencyCycles: Reject
            Layers:
            - Id: surface
              Priority: 0
            Cells:
            - Coordinate: { X: 0, Y: 0, Z: 0 }
              Layer: surface
              Scene:
                Guid: {{cellSceneGuid:D}}
                PackageId: {{PackageId}}
              Bounds:
                Min: { X: 0, Y: 0, Z: 0 }
                Max: { X: 100, Y: 100, Z: 100 }
              EstimatedCpuBytes: 4096
              EstimatedGpuBytes: 4096
            """);
        database.AddAsset(persistentSceneGuid, "Scene", persistentPath, PackageId);
        database.AddAsset(cellSceneGuid, "Scene", cellPath, PackageId);
        database.AddAsset(worldGuid, "World", worldPath, PackageId);
        return new AssetRef<WorldSourceAsset>(worldGuid, "World", PackageId);
    }

    private static void PumpWorldUntil(
        RuntimeWorldStreamingService streaming,
        Func<bool> condition)
    {
        var deadline = Stopwatch.StartNew();
        while (!condition())
        {
            streaming.ProcessAtFrameBoundary();
            if (deadline.Elapsed > TimeSpan.FromSeconds(5))
            {
                throw new TimeoutException(
                    "Resources package world did not reach the expected state.");
            }

            Thread.Yield();
        }
    }

    private static void SetField<T>(ResourcesPackage package, string name, T value)
    {
        FieldInfo field = typeof(ResourcesPackage).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing ResourcesPackage field '{name}'.");
        field.SetValue(package, value);
    }

    private static T? GetField<T>(ResourcesPackage package, string name)
        where T : class
    {
        FieldInfo field = typeof(ResourcesPackage).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing ResourcesPackage field '{name}'.");
        return (T?)field.GetValue(package);
    }

    private sealed class FailOnceReleasePreparedProvider : IRuntimePreparedAssetProvider
    {
        private readonly HashSet<RuntimeAssetResidencyKey> m_Prepared = new();

        public string ProviderId => "test.resources-package-fail-once";
        public bool AllowRelease { get; set; }
        public int ReleaseAttemptCount { get; private set; }

        public bool Supports(string assetType) =>
            assetType is "Mesh" or "Material";

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            m_Prepared.Add(key);
            return RuntimePreparedAssetResult.Ready(64);
        }

        public void Release(RuntimeAssetResidencyKey key)
        {
            ReleaseAttemptCount++;
            if (!AllowRelease)
            {
                AllowRelease = true;
                throw new InvalidOperationException(
                    $"Injected prepared-resource release failure for '{key}'.");
            }

            m_Prepared.Remove(key);
        }

        public RuntimePreparedAssetProviderMetrics GetMetrics() => new(
            m_Prepared.Count,
            m_Prepared.Count * 64,
            PendingDisposalCount: 0);
    }

    private sealed class FailOnceServiceRegistry : IServiceRegistry
    {
        private readonly IServiceRegistry m_Inner;
        private readonly Type m_FailOnceContract;
        private bool m_HasFailed;

        public FailOnceServiceRegistry(
            IServiceRegistry inner,
            Type failOnceContract)
        {
            m_Inner = inner;
            m_FailOnceContract = failOnceContract;
        }

        public void RegisterService<T>(T service)
        {
            ThrowOnceIfRequested(typeof(T));
            m_Inner.RegisterService(service);
        }

        public void RegisterService(Type contractType, object service)
        {
            ThrowOnceIfRequested(contractType);
            m_Inner.RegisterService(contractType, service);
        }

        public T GetService<T>() => m_Inner.GetService<T>();

        public bool TryGetService<T>(out T service) => m_Inner.TryGetService(out service);

        public bool IsServiceRegistered(string contractName) =>
            m_Inner.IsServiceRegistered(contractName);

        public IReadOnlyCollection<ServiceRegistrationInfo> GetRegisteredServices() =>
            m_Inner.GetRegisteredServices();

        private void ThrowOnceIfRequested(Type contractType)
        {
            if (m_HasFailed || contractType != m_FailOnceContract)
            {
                return;
            }

            m_HasFailed = true;
            throw new InvalidOperationException(
                $"Injected service registration failure for '{contractType.Name}'.");
        }
    }

    private sealed class FailOnceUnregisterCookerRegistry : IRuntimeAssetCookerRegistry
    {
        private readonly RuntimeAssetCookerRegistry m_Inner = new();
        private readonly IRuntimeAssetCooker m_FailOnceCooker;
        private readonly Dictionary<IRuntimeAssetCooker, int> m_UnregisterAttempts = new();
        private bool m_HasFailed;

        public FailOnceUnregisterCookerRegistry(IRuntimeAssetCooker failOnceCooker)
        {
            m_FailOnceCooker = failOnceCooker;
        }

        public void RegisterCooker(IRuntimeAssetCooker cooker) =>
            m_Inner.RegisterCooker(cooker);

        public bool UnregisterCooker(IRuntimeAssetCooker cooker)
        {
            m_UnregisterAttempts[cooker] = GetUnregisterAttemptCount(cooker) + 1;
            if (!m_HasFailed && ReferenceEquals(cooker, m_FailOnceCooker))
            {
                m_HasFailed = true;
                throw new InvalidOperationException(
                    $"Injected cooker unregister failure for '{cooker.ProviderId}'.");
            }

            return m_Inner.UnregisterCooker(cooker);
        }

        public bool TryGetCooker(string assetType, out IRuntimeAssetCooker cooker) =>
            m_Inner.TryGetCooker(assetType, out cooker);

        public IReadOnlyCollection<RuntimeAssetCookerRegistration> GetRegistrations() =>
            m_Inner.GetRegistrations();

        public int GetUnregisterAttemptCount(IRuntimeAssetCooker cooker) =>
            m_UnregisterAttempts.TryGetValue(cooker, out int count) ? count : 0;
    }

    private sealed class FailOnceReleaseAllAssetDatabase : IAssetDatabase
    {
        private readonly IAssetDatabase m_Inner;
        private bool m_FailNextReleaseAll = true;

        public FailOnceReleaseAllAssetDatabase(IAssetDatabase inner)
        {
            m_Inner = inner;
        }

        public AssetDatabaseMode Mode => m_Inner.Mode;
        public bool IsReadOnlyRuntime => m_Inner.IsReadOnlyRuntime;
        public AssetSourceAccessMode SourceAccessMode => m_Inner.SourceAccessMode;
        public bool CanReadSourceAssets => m_Inner.CanReadSourceAssets;
        public string CookedRoot => m_Inner.CookedRoot;
        public IReadOnlyCollection<AssetRecord> Assets => m_Inner.Assets;
        public int ReleaseAllAttemptCount { get; private set; }

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
            out CookedAssetHandle handle) =>
            m_Inner.TryLoadCookedAsset(guid, variant, expectedAssetType, out handle);

        public bool TryGetCookedAssetBytes(
            CookedAssetHandle handle,
            out ReadOnlyMemory<byte> bytes) =>
            m_Inner.TryGetCookedAssetBytes(handle, out bytes);

        public ReadOnlyMemory<byte> GetCookedAssetBytes(CookedAssetHandle handle) =>
            m_Inner.GetCookedAssetBytes(handle);

        public void Release(CookedAssetHandle handle) => m_Inner.Release(handle);

        public void ReleaseAllLoadedCookedAssets()
        {
            ReleaseAllAttemptCount++;
            if (m_FailNextReleaseAll)
            {
                m_FailNextReleaseAll = false;
                throw new InvalidOperationException(
                    "Injected package cooked-handle release failure.");
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
    }
}
