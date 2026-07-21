using System.Diagnostics;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Threading;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RuntimeWorldStreamingTests
{
    [Fact]
    public void DelayedWorkerRead_DoesNotBlockFrameOrMutateEcsBeforeActivation()
    {
        using var context = new StreamingContext(loaderFactory: database =>
            new GatePayloadLoader(new WorldCellPayloadLoader(database), honorCancellation: true));
        var loader = Assert.IsType<GatePayloadLoader>(context.PayloadLoader);
        WorldCellId cellId = context.CellId(0);
        int frameThreadId = Environment.CurrentManagedThreadId;
        var transitions = new List<WorldCellStreamingState>();
        var callbackThreadIds = new List<int>();
        context.Streaming.CellStateChanged += snapshot =>
        {
            if (snapshot.CellId != cellId) return;
            transitions.Add(snapshot.State);
            callbackThreadIds.Add(Environment.CurrentManagedThreadId);
        };
        Assert.True(context.Streaming.PinCell(cellId));

        var elapsed = Stopwatch.StartNew();
        context.Streaming.ProcessAtFrameBoundary();
        elapsed.Stop();

        Assert.True(elapsed.Elapsed < TimeSpan.FromMilliseconds(100));
        Assert.True(loader.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, context.World.EntityCount);
        Assert.DoesNotContain(WorldCellStreamingState.Active, transitions);

        loader.Release.Set();
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Active);
        Assert.Equal(2, context.World.EntityCount);
        Assert.Contains(WorldCellStreamingState.Reading, transitions);
        Assert.Contains(WorldCellStreamingState.Decoding, transitions);
        Assert.Contains(WorldCellStreamingState.Validating, transitions);
        Assert.Contains(WorldCellStreamingState.ReadyToActivate, transitions);
        Assert.Contains(WorldCellStreamingState.Active, transitions);

        Assert.True(context.Streaming.UnpinCell(cellId));
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Unloaded);
        Assert.Equal(1, context.World.EntityCount);
        Assert.Contains(WorldCellStreamingState.QueuedToUnload, transitions);
        Assert.Contains(WorldCellStreamingState.Unloading, transitions);
        Assert.All(callbackThreadIds, threadId => Assert.Equal(frameThreadId, threadId));
    }

    [Fact]
    public void BudgetsBoundReadsAndActivationsWhileDependenciesActivateFirst()
    {
        var budgets = new WorldStreamingBudgets(
            MaxConcurrentReads: 1,
            MaxBytesInFlight: 1 * 1024 * 1024,
            MaxDecodedStagingBytes: 4 * 1024 * 1024,
            MaxActivationsPerFrame: 1,
            MaxActivationMilliseconds: 100,
            MaxUnloadsPerFrame: 1);
        using var context = new StreamingContext(budgets: budgets);
        WorldCellId first = context.CellId(0);
        WorldCellId dependent = context.CellId(1);
        WorldCellId independent = context.CellId(2);
        var activationOrder = new List<WorldCellId>();
        context.Streaming.CellStateChanged += snapshot =>
        {
            if (snapshot.State == WorldCellStreamingState.Active) activationOrder.Add(snapshot.CellId);
        };
        Assert.True(context.Streaming.PinCell(first));
        Assert.True(context.Streaming.PinCell(dependent));
        Assert.True(context.Streaming.PinCell(independent));

        context.PumpUntil(() => context.Streaming.GetMetrics().ActiveCells == 3, () =>
        {
            WorldStreamingMetrics metrics = context.Streaming.GetMetrics();
            Assert.InRange(metrics.InFlightReads, 0, 1);
            Assert.InRange(metrics.BytesInFlight, 0, budgets.MaxBytesInFlight);
            Assert.InRange(metrics.DecodedStagingBytes, 0, budgets.MaxDecodedStagingBytes);
        });

        Assert.Equal(3, activationOrder.Distinct().Count());
        Assert.True(activationOrder.IndexOf(first) < activationOrder.IndexOf(dependent));
        Assert.Equal(4, context.World.EntityCount);
        Assert.True(context.Streaming.GetMetrics().BudgetStallCount > 0);

        context.Streaming.UnpinCell(first);
        context.Streaming.UnpinCell(dependent);
        context.Streaming.UnpinCell(independent);
        context.PumpUntil(() => context.Streaming.GetMetrics().ActiveCells == 0);
        WorldStreamingMetrics drained = context.Streaming.GetMetrics();
        Assert.Equal(0, drained.BytesInFlight);
        Assert.Equal(0, drained.DecodedStagingBytes);
        Assert.Equal(1, context.World.EntityCount);
    }

    [Fact]
    public void OversizedCellFailsInsteadOfRemainingQueuedForever()
    {
        var budgets = new WorldStreamingBudgets(
            MaxConcurrentReads: 1,
            MaxBytesInFlight: 1024,
            MaxDecodedStagingBytes: 1024,
            MaxActivationsPerFrame: 1,
            MaxActivationMilliseconds: 100,
            MaxUnloadsPerFrame: 1);
        using var context = new StreamingContext(budgets: budgets);
        WorldCellId cellId = context.CellId(0);
        Assert.True(context.Streaming.PinCell(cellId));

        context.Streaming.ProcessAtFrameBoundary();

        WorldCellStreamingSnapshot failed = context.Cell(cellId);
        Assert.Equal(WorldCellStreamingState.Failed, failed.State);
        Assert.Contains("exceeding configured limits", failed.Diagnostic);
        Assert.Equal(0, context.TaskGraph.OutstandingTaskCount);
        Assert.Equal(1, context.World.EntityCount);
    }

    [Fact]
    public void CameraSelectionHonorsActiveCellLimitDeterministically()
    {
        using var context = new StreamingContext(loadRadius: 2, maxActiveCells: 2);
        context.Streaming.SetStreamingSource(new WorldPosition(10, 10, 10));

        context.PumpUntil(() => context.Streaming.GetMetrics().ActiveCells == 2);

        WorldCellId[] active = context.Streaming.GetCells()
            .Where(cell => cell.State == WorldCellStreamingState.Active)
            .Select(cell => cell.CellId)
            .Order()
            .ToArray();
        Assert.Equal(new[] { context.CellId(0), context.CellId(1) }.Order().ToArray(), active);
        Assert.Equal(WorldCellStreamingState.Unloaded, context.State(context.CellId(2)));
        Assert.Contains("active-cell limit", context.Cell(context.CellId(2)).Diagnostic);
        Assert.True(context.Streaming.GetMetrics().BudgetStallCount > 0);

        using var transitionContext = new StreamingContext(loadRadius: 0, maxActiveCells: 1);
        transitionContext.Streaming.SetStreamingSource(new WorldPosition(10, 10, 10));
        transitionContext.PumpUntil(() =>
            transitionContext.State(transitionContext.CellId(0)) == WorldCellStreamingState.Active);
        transitionContext.Streaming.SetStreamingSource(new WorldPosition(210, 10, 10));
        transitionContext.PumpUntil(
            () => transitionContext.State(transitionContext.CellId(2)) == WorldCellStreamingState.Active,
            () => Assert.InRange(transitionContext.Streaming.GetMetrics().ActiveCells, 0, 1));
        Assert.Equal(WorldCellStreamingState.Unloaded, transitionContext.State(transitionContext.CellId(0)));
    }

    [Fact]
    public void RebasedActivationPlacesCellLocalTransformsAgainstCurrentOrigin()
    {
        using var context = new StreamingContext(loadRadius: 0);
        WorldCellId firstCell = context.CellId(0);
        context.Streaming.SetStreamingSource(new WorldPosition(210, 10, 10));
        Assert.True(context.Streaming.PinCell(firstCell));

        context.PumpUntil(() =>
            context.State(firstCell) == WorldCellStreamingState.Active &&
            context.Origin.CurrentOrigin.X == 200);

        WorldCellStreamingSnapshot cell = context.Cell(firstCell);
        Assert.True(context.SceneService.TryResolveEntity(
            cell.SceneInstanceId,
            StreamingContext.EntityGuid(1),
            out Entity entity));
        ref TransformComponent transform = ref context.World.GetComponent<TransformComponent>(entity);
        Assert.Equal(-199.0f, transform.Position.X);
        Assert.Equal(1.0, context.Origin.ToWorld(transform.Position).X, 5);
    }

    [Fact]
    public void CancelledStaleCompletion_CannotResurrectUnwantedGeneration()
    {
        using var context = new StreamingContext(loaderFactory: database =>
            new GatePayloadLoader(new WorldCellPayloadLoader(database), honorCancellation: false));
        var loader = Assert.IsType<GatePayloadLoader>(context.PayloadLoader);
        WorldCellId cellId = context.CellId(0);
        int activeTransitions = 0;
        context.Streaming.CellStateChanged += snapshot =>
        {
            if (snapshot.CellId == cellId && snapshot.State == WorldCellStreamingState.Active)
            {
                activeTransitions++;
            }
        };
        context.Streaming.PinCell(cellId);
        context.Streaming.ProcessAtFrameBoundary();
        Assert.True(loader.Started.Wait(TimeSpan.FromSeconds(2)));

        context.Streaming.UnpinCell(cellId);
        context.Streaming.ProcessAtFrameBoundary();
        Assert.Equal(WorldCellStreamingState.Cancelled, context.State(cellId));
        context.Streaming.PinCell(cellId);
        context.Streaming.ProcessAtFrameBoundary();
        loader.Release.Set();

        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Active);
        WorldCellStreamingSnapshot snapshot = context.Cell(cellId);
        Assert.True(snapshot.RequestGeneration >= 3);
        Assert.Equal(1, activeTransitions);
        Assert.True(context.Streaming.GetMetrics().StaleCompletionCount >= 1);
        Assert.Equal(2, context.World.EntityCount);
    }

    [Fact]
    public void FailedReadIsRetainedUntilExplicitRetry()
    {
        using var context = new StreamingContext(loaderFactory: database =>
            new FailOncePayloadLoader(new WorldCellPayloadLoader(database)));
        WorldCellId cellId = context.CellId(0);
        context.Streaming.PinCell(cellId);

        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Failed);

        Assert.NotEmpty(context.Streaming.GetDiagnostics());
        Assert.True(context.Streaming.GetMetrics().FailureCount >= 1);
        Assert.Equal(1, context.World.EntityCount);
        Assert.True(context.Streaming.RetryCell(cellId));
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Active);
        Assert.Equal(2, context.World.EntityCount);
    }

    [Fact]
    public void EditorPreviewSource_ReloadsThroughNormalCellInstanceBoundaryWithoutWritingDisk()
    {
        using var context = new StreamingContext();
        WorldCellId cellId = context.CellId(0);
        context.Streaming.PinCell(cellId);
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Active);
        RuntimeSceneInstanceId firstInstance = context.Cell(cellId).SceneInstanceId;
        string path = context.CellScenePath(0);
        string diskSource = File.ReadAllText(path);
        string previewSource = diskSource.Replace(
            "Position: { X: 1, Y: 0, Z: 0 }",
            "Position: { X: 42, Y: 0, Z: 0 }",
            StringComparison.Ordinal);
        Assert.NotEqual(diskSource, previewSource);

        Assert.True(context.Streaming.SetCellPreviewSource(
            cellId,
            new SceneSourceSnapshot(
                context.CellSceneRef(0),
                path,
                previewSource,
                7)));
        Assert.True(context.Cell(cellId).ReloadRequested);
        context.PumpUntil(() =>
            context.State(cellId) == WorldCellStreamingState.Active &&
            !context.Cell(cellId).ReloadRequested &&
            context.Cell(cellId).SceneInstanceId != firstInstance);

        Assert.True(context.SceneService.TryResolveEntity(
            context.Cell(cellId).SceneInstanceId,
            StreamingContext.EntityGuid(1),
            out Entity previewEntity));
        Assert.Equal(42, context.World.GetComponent<TransformComponent>(previewEntity).Position.X);
        Assert.Equal(diskSource, File.ReadAllText(path));

        RuntimeSceneInstanceId previewInstance = context.Cell(cellId).SceneInstanceId;
        Assert.True(context.Streaming.SetCellPreviewSource(cellId, null));
        context.PumpUntil(() =>
            context.State(cellId) == WorldCellStreamingState.Active &&
            !context.Cell(cellId).ReloadRequested &&
            context.Cell(cellId).SceneInstanceId != previewInstance);
        Assert.True(context.SceneService.TryResolveEntity(
            context.Cell(cellId).SceneInstanceId,
            StreamingContext.EntityGuid(1),
            out Entity diskEntity));
        Assert.Equal(1, context.World.GetComponent<TransformComponent>(diskEntity).Position.X);
    }

    [Fact]
    public void RejectedUnloadRemainsActiveUntilExplicitRetryWithoutDuplicateLoad()
    {
        using var context = new StreamingContext();
        WorldCellId cellId = context.CellId(0);
        context.Streaming.PinCell(cellId);
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Active);

        WorldCellStreamingSnapshot active = context.Cell(cellId);
        Assert.True(context.SceneService.TryResolveEntity(
            context.SceneService.ActiveScene!.InstanceId,
            StreamingContext.EntityGuid(0),
            out Entity persistentEntity));
        Assert.True(context.SceneService.TryResolveEntity(
            active.SceneInstanceId,
            StreamingContext.EntityGuid(1),
            out Entity cellEntity));
        context.World.AddComponent(cellEntity, new ParentComponent { Parent = persistentEntity });

        Assert.True(context.Streaming.UnpinCell(cellId));
        context.Streaming.ProcessAtFrameBoundary();

        WorldCellStreamingSnapshot rejected = context.Cell(cellId);
        Assert.Equal(WorldCellStreamingState.Active, rejected.State);
        Assert.Equal(active.SceneInstanceId, rejected.SceneInstanceId);
        Assert.Contains("crosses the unload boundary", rejected.Diagnostic);
        Assert.Equal(2, context.SceneService.GetSceneInstances().Count);
        Assert.Equal(2, context.World.EntityCount);

        for (int frame = 0; frame < 4; frame++) context.Streaming.ProcessAtFrameBoundary();
        Assert.Equal(active.SceneInstanceId, context.Cell(cellId).SceneInstanceId);
        Assert.Equal(2, context.SceneService.GetSceneInstances().Count);

        context.World.RemoveComponent<ParentComponent>(cellEntity);
        Assert.True(context.Streaming.RetryCell(cellId));
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Unloaded);
        Assert.Single(context.SceneService.GetSceneInstances());
        Assert.Equal(1, context.World.EntityCount);
    }

    [Fact]
    public void CameraHysteresisAvoidsBoundaryThrashAndFarMoveUnloads()
    {
        using var context = new StreamingContext();
        WorldCellId first = context.CellId(0);
        context.Streaming.SetStreamingSource(new WorldPosition(10, 10, 10));
        context.PumpUntil(() => context.State(first) == WorldCellStreamingState.Active);

        context.Streaming.SetStreamingSource(new WorldPosition(110, 10, 10));
        for (int frame = 0; frame < 16; frame++) context.Streaming.ProcessAtFrameBoundary();
        Assert.Equal(WorldCellStreamingState.Active, context.State(first));

        context.Streaming.SetStreamingSource(new WorldPosition(310, 10, 10));
        context.PumpUntil(() => context.State(first) == WorldCellStreamingState.Unloaded);
    }

    [Fact]
    public void ShutdownCancelsWorkersUnloadsInstancesAndLeavesNoPendingState()
    {
        using var context = new StreamingContext(loaderFactory: database =>
            new GatePayloadLoader(new WorldCellPayloadLoader(database), honorCancellation: true));
        var loader = Assert.IsType<GatePayloadLoader>(context.PayloadLoader);
        context.Streaming.PinCell(context.CellId(0));
        context.Streaming.ProcessAtFrameBoundary();
        Assert.True(loader.Started.Wait(TimeSpan.FromSeconds(2)));

        context.Streaming.Shutdown(unloadActiveCells: true);

        Assert.Null(context.Streaming.ActiveWorld);
        Assert.Empty(context.Streaming.GetCells());
        Assert.Equal(0, context.TaskGraph.OutstandingTaskCount);
        Assert.Empty(context.SceneService.GetSceneInstances());
        Assert.Equal(0, context.World.EntityCount);
    }

    [Fact]
    public void RequiredPreparedResourcesGateActivationAndRemainSharedWithPersistentScene()
    {
        var provider = new GatePreparedProvider();
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider);
        WorldCellId cellId = context.CellId(0);
        context.Streaming.PinCell(cellId);

        context.PumpUntil(() =>
            context.State(cellId) == WorldCellStreamingState.WaitingForResources);

        Assert.Equal(1, context.World.EntityCount);
        Assert.Equal(2, context.Residency.GetMetrics().WaitingAssetCount);
        provider.Ready = true;
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Active);
        Assert.Equal(2, context.World.EntityCount);
        Assert.All(
            context.Residency.GetResources(),
            resource => Assert.Equal(2, resource.OwnerCount));

        context.Streaming.UnpinCell(cellId);
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Unloaded);
        Assert.All(
            context.Residency.GetResources(),
            resource => Assert.Equal(1, resource.OwnerCount));
        Assert.Equal(0, provider.ReleaseCount);
    }

    [Fact]
    public void BoundedSmokeScenario_ObservesTransitionsSoaksAndDrainsShutdown()
    {
        using var context = new StreamingContext(loadRadius: 2);
        string output = Path.Combine(context.Root, "world-streaming-smoke.json");
        var scenarioContext = new ArisenKernel.Lifecycle.RuntimeSmokeScenarioContext(
            "world-streaming",
            context.Root,
            "Development",
            output,
            VisualSummaryService: null);
        var scenario = new WorldStreamingSmokeScenario(
            scenarioContext,
            context.Streaming,
            context.SceneService,
            context.Residency,
            context.Origin,
            context.Database,
            context.TaskGraph);

        scenario.Start(0);
        for (uint frame = 0; frame < 1024 && !scenario.IsReadyForShutdown; frame++)
        {
            scenario.BeforeFrame(frame);
            context.Streaming.ProcessAtFrameBoundary();
            scenario.AfterFrame(frame);
            Thread.Yield();
        }

        Assert.True(scenario.IsReadyForShutdown, scenario.FailureMessage);
        context.Streaming.Shutdown(unloadActiveCells: true);
        context.SceneService.ClearForShutdown();
        context.Database.ReleaseAllLoadedCookedAssets();
        scenario.AfterShutdown();

        Assert.True(scenario.IsComplete);
        Assert.True(scenario.Succeeded, scenario.FailureMessage);
        Assert.True(File.Exists(output));
        string json = File.ReadAllText(output);
        Assert.Contains("\"passed\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"completedSoakCycles\": 4", json, StringComparison.Ordinal);
    }

    private sealed class StreamingContext : IDisposable
    {
        private const string PackageId = "com.arisen.test";
        private static readonly Guid s_WorldGuid = Guid.Parse("83000000-0000-0000-0000-000000000010");
        private static readonly Guid s_PersistentSceneGuid = Guid.Parse("83000000-0000-0000-0000-000000000001");
        private static readonly Guid[] s_CellSceneGuids =
        [
            Guid.Parse("83000000-0000-0000-0000-000000000002"),
            Guid.Parse("83000000-0000-0000-0000-000000000003"),
            Guid.Parse("83000000-0000-0000-0000-000000000004")
        ];
        private static readonly Guid s_SharedMeshGuid =
            Guid.Parse("83000000-0000-0000-0000-000000000101");
        private static readonly Guid s_SharedMaterialGuid =
            Guid.Parse("83000000-0000-0000-0000-000000000102");
        private readonly bool m_IncludeSharedRenderAssets;

        public StreamingContext(
            Func<IAssetDatabase, IWorldCellPayloadLoader>? loaderFactory = null,
            WorldStreamingBudgets? budgets = null,
            int loadRadius = 0,
            int maxActiveCells = 8,
            bool includeSharedRenderAssets = false,
            IRuntimePreparedAssetProvider? preparedProvider = null)
        {
            m_IncludeSharedRenderAssets = includeSharedRenderAssets;
            Root = Path.Combine(Path.GetTempPath(), "ArisenWorldStreamingTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(Root, "Cooked"));
            if (includeSharedRenderAssets)
            {
                AddCookedRenderAsset(s_SharedMeshGuid, "Mesh", RuntimeAssetVariantPolicy.StaticMesh);
                AddCookedRenderAsset(s_SharedMaterialGuid, "Material", RuntimeAssetVariantPolicy.Material);
            }
            AddScene(s_PersistentSceneGuid, "Persistent", 0);
            for (int index = 0; index < s_CellSceneGuids.Length; index++)
            {
                AddScene(s_CellSceneGuids[index], "Cell" + index, index + 1);
            }

            string worldPath = Path.Combine(Root, "Streaming.arisenworld");
            File.WriteAllText(worldPath, CreateWorldSource(loadRadius, maxActiveCells));
            Database.AddAsset(s_WorldGuid, "World", worldPath, PackageId);
            World = new EntityManager();
            SceneService = new RuntimeSceneService(Database, World);
            TaskGraph = new TaskGraph(workerCount: 2);
            Residency = new RuntimeAssetResidencyService(Database);
            Origin = new WorldOriginService();
            if (preparedProvider != null) Residency.RegisterPreparedProvider(preparedProvider);
            PayloadLoader = loaderFactory?.Invoke(Database) ?? new WorldCellPayloadLoader(Database);
            Streaming = new RuntimeWorldStreamingService(
                Database,
                SceneService,
                TaskGraph,
                PayloadLoader,
                budgets ?? new WorldStreamingBudgets(
                    2,
                    8 * 1024 * 1024,
                    16 * 1024 * 1024,
                    1,
                    100,
                    1),
                Residency,
                Origin);
            RuntimeWorldLoadResult loaded = Streaming.LoadWorld(
                new AssetRef<WorldSourceAsset>(s_WorldGuid, "World", PackageId));
            Assert.True(loaded.Success, loaded.Diagnostic);
            Assert.Equal(1, World.EntityCount);
        }

        public string Root { get; }
        public TestAssetDatabase Database { get; }
        public EntityManager World { get; }
        public RuntimeSceneService SceneService { get; }
        public TaskGraph TaskGraph { get; }
        public RuntimeAssetResidencyService Residency { get; }
        public WorldOriginService Origin { get; }
        public IWorldCellPayloadLoader PayloadLoader { get; }
        public RuntimeWorldStreamingService Streaming { get; }

        public WorldCellId CellId(int coordinateX) =>
            WorldCellIdentity.Create(s_WorldGuid, new WorldCellCoordinate(coordinateX, 0, 0), "surface");

        public string CellScenePath(int coordinateX) =>
            Path.Combine(Root, "Cell" + coordinateX + ".arisenscene");

        public AssetRef<SceneSourceAsset> CellSceneRef(int coordinateX) =>
            new(s_CellSceneGuids[coordinateX], "Scene", PackageId);

        public WorldCellStreamingSnapshot Cell(WorldCellId id) =>
            Streaming.GetCells().Single(cell => cell.CellId == id);

        public WorldCellStreamingState State(WorldCellId id) => Cell(id).State;

        public static Guid EntityGuid(int positionX) =>
            new($"83100000-0000-0000-0000-{positionX + 1:D12}");

        public void PumpUntil(Func<bool> condition, Action? observe = null)
        {
            var deadline = Stopwatch.StartNew();
            while (!condition())
            {
                Streaming.ProcessAtFrameBoundary();
                observe?.Invoke();
                if (deadline.Elapsed > TimeSpan.FromSeconds(5))
                {
                    throw new TimeoutException(
                        "World streaming condition was not reached. " +
                        string.Join(", ", Streaming.GetCells().Select(cell => $"{cell.CellId}:{cell.State}:{cell.Diagnostic}")));
                }

                Thread.Yield();
            }
        }

        public void Dispose()
        {
            Streaming.Shutdown(unloadActiveCells: true);
            SceneService.ClearForShutdown();
            Residency.Dispose();
            TaskGraph.Dispose();
            try
            {
                if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }

        private void AddScene(Guid sceneGuid, string name, int positionX)
        {
            Guid entityGuid = EntityGuid(positionX);
            string path = Path.Combine(Root, name + ".arisenscene");
            string schemaSuffix = m_IncludeSharedRenderAssets
                ? "\n- TypeId: 3\n  Name: MeshRenderer\n  Version: 1\n  Required: true"
                : string.Empty;
            string rendererSuffix = m_IncludeSharedRenderAssets
                ? $"\n  MeshRenderer:\n    Mesh: {{ Guid: {s_SharedMeshGuid:D}, PackageId: {PackageId} }}" +
                  $"\n    Material: {{ Guid: {s_SharedMaterialGuid:D}, PackageId: {PackageId} }}"
                : string.Empty;
            File.WriteAllText(path, $$"""
                Version: 2
                Name: {{name}}
                ComponentSchemas:
                - TypeId: 1
                  Name: Transform
                  Version: 1
                  Required: true{{schemaSuffix}}
                Entities:
                - Guid: {{entityGuid:D}}
                  Name: {{name}}
                  Transform:
                    Position: { X: {{positionX}}, Y: 0, Z: 0 }
                    Rotation: { X: 0, Y: 0, Z: 0, W: 1 }
                    Scale: { X: 1, Y: 1, Z: 1 }{{rendererSuffix}}
                """);
            Database.AddAsset(sceneGuid, "Scene", path, PackageId);
        }

        private void AddCookedRenderAsset(Guid guid, string assetType, string variant)
        {
            string sourcePath = Path.Combine(Root, guid.ToString("N") + ".source");
            string cookedPath = Path.Combine(Root, guid.ToString("N") + ".cooked");
            File.WriteAllText(sourcePath, "test source");
            File.WriteAllBytes(cookedPath, [1, 2, 3, 4]);
            Database.AddAsset(guid, assetType, sourcePath, PackageId);
            Database.RegisterCookedArtifact(new CookedAssetRecord(
                guid,
                assetType,
                variant,
                cookedPath,
                4,
                File.GetLastWriteTimeUtc(cookedPath)));
        }

        private static string CreateWorldSource(int loadRadius, int maxActiveCells)
        {
            return $$"""
                Version: 1
                WorldGuid: {{s_WorldGuid:D}}
                Name: Streaming Test World
                PersistentScene:
                  Guid: {{s_PersistentSceneGuid:D}}
                  PackageId: {{PackageId}}
                Partition:
                  Origin: { X: 0, Y: 0, Z: 0 }
                  CellSize: { X: 100, Y: 100, Z: 100 }
                  LoadRadius: {{loadRadius}}
                  UnloadHysteresis: 1
                  MaxActiveCells: {{maxActiveCells}}
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
                    Guid: {{s_CellSceneGuids[0]:D}}
                    PackageId: {{PackageId}}
                  Bounds:
                    Min: { X: 0, Y: 0, Z: 0 }
                    Max: { X: 100, Y: 100, Z: 100 }
                  EstimatedCpuBytes: 4096
                  EstimatedGpuBytes: 4096
                - Coordinate: { X: 1, Y: 0, Z: 0 }
                  Layer: surface
                  Scene:
                    Guid: {{s_CellSceneGuids[1]:D}}
                    PackageId: {{PackageId}}
                  Bounds:
                    Min: { X: 100, Y: 0, Z: 0 }
                    Max: { X: 200, Y: 100, Z: 100 }
                  Dependencies:
                  - Coordinate: { X: 0, Y: 0, Z: 0 }
                    Layer: surface
                  EstimatedCpuBytes: 4096
                  EstimatedGpuBytes: 4096
                - Coordinate: { X: 2, Y: 0, Z: 0 }
                  Layer: surface
                  Scene:
                    Guid: {{s_CellSceneGuids[2]:D}}
                    PackageId: {{PackageId}}
                  Bounds:
                    Min: { X: 200, Y: 0, Z: 0 }
                    Max: { X: 300, Y: 100, Z: 100 }
                  EstimatedCpuBytes: 8192
                  EstimatedGpuBytes: 4096
                """;
        }
    }

    private sealed class GatePreparedProvider : IRuntimePreparedAssetProvider
    {
        private readonly HashSet<RuntimeAssetResidencyKey> m_Prepared = new();

        public string ProviderId => "test.world-streaming-prepared";
        public bool Ready { get; set; }
        public int ReleaseCount { get; private set; }

        public bool Supports(string assetType) => assetType is "Mesh" or "Material";

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            if (!Ready) return RuntimePreparedAssetResult.Waiting("Test provider is gated.");
            m_Prepared.Add(key);
            return RuntimePreparedAssetResult.Ready(64);
        }

        public void Release(RuntimeAssetResidencyKey key)
        {
            if (m_Prepared.Remove(key)) ReleaseCount++;
        }

        public RuntimePreparedAssetProviderMetrics GetMetrics() =>
            new(m_Prepared.Count, m_Prepared.Count * 64, 0);
    }

    private sealed class GatePayloadLoader : IWorldCellPayloadLoader
    {
        private readonly IWorldCellPayloadLoader m_Inner;
        private readonly bool m_HonorCancellation;
        private int m_CallCount;

        public GatePayloadLoader(IWorldCellPayloadLoader inner, bool honorCancellation)
        {
            m_Inner = inner;
            m_HonorCancellation = honorCancellation;
        }

        public ManualResetEventSlim Started { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);

        public CellPayloadLoadResult Load(
            WorldCellDescriptor cell,
            long generation,
            SceneSourceSnapshot? previewSource,
            Action<WorldCellStreamingState> reportState,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref m_CallCount) == 1)
            {
                reportState(WorldCellStreamingState.Reading);
                Started.Set();
                if (m_HonorCancellation)
                {
                    Release.Wait(cancellationToken);
                }
                else
                {
                    Release.Wait();
                }
            }

            return m_Inner.Load(
                cell,
                generation,
                previewSource,
                reportState,
                m_HonorCancellation ? cancellationToken : CancellationToken.None);
        }
    }

    private sealed class FailOncePayloadLoader : IWorldCellPayloadLoader
    {
        private readonly IWorldCellPayloadLoader m_Inner;
        private int m_Failed;

        public FailOncePayloadLoader(IWorldCellPayloadLoader inner)
        {
            m_Inner = inner;
        }

        public CellPayloadLoadResult Load(
            WorldCellDescriptor cell,
            long generation,
            SceneSourceSnapshot? previewSource,
            Action<WorldCellStreamingState> reportState,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref m_Failed, 1) == 0)
            {
                reportState(WorldCellStreamingState.Reading);
                throw new IOException("Injected world-cell read failure.");
            }

            return m_Inner.Load(cell, generation, previewSource, reportState, cancellationToken);
        }
    }
}
