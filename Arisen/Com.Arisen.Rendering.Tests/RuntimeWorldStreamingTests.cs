using System.Diagnostics;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Threading;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

[Collection(SceneComponentExtensionRegistryCollection.Name)]
public sealed class RuntimeWorldStreamingTests
{
    [Fact]
    public void CellNotifications_AggregateFailuresAndRunEverySubscriberInOrder()
    {
        using var context = new StreamingContext();
        var subscribers = new FailingStreamingSubscribers();
        context.Streaming.CellStateChanged += subscribers.CellFailureOne;
        context.Streaming.CellStateChanged += subscribers.CellSuccessOne;
        context.Streaming.CellStateChanged += subscribers.CellFailureTwo;
        context.Streaming.CellStateChanged += subscribers.CellSuccessTwo;

        Assert.True(context.Streaming.PinCell(context.CellId(0)));
        context.Streaming.ProcessAtFrameBoundary();

        context.Streaming.CellStateChanged -= subscribers.CellFailureOne;
        context.Streaming.CellStateChanged -= subscribers.CellSuccessOne;
        context.Streaming.CellStateChanged -= subscribers.CellFailureTwo;
        context.Streaming.CellStateChanged -= subscribers.CellSuccessTwo;

        Assert.NotEmpty(subscribers.Invocations);
        Assert.Equal(0, subscribers.Invocations.Count % 4);
        for (int index = 0; index < subscribers.Invocations.Count; index += 4)
        {
            Assert.Equal(
                ["cell-failure-one", "cell-success-one", "cell-failure-two", "cell-success-two"],
                subscribers.Invocations.Skip(index).Take(4));
        }

        WorldStreamingDiagnostic aggregate = Assert.Single(
            context.Streaming.GetDiagnostics(),
            diagnostic => diagnostic.Kind == WorldStreamingDiagnosticKind.SubscriberAggregate);
        Assert.Equal("ProcessAtFrameBoundary", aggregate.Boundary);
        Assert.Equal(subscribers.Invocations.Count / 2, aggregate.SubscriberFailures.Count);
        Assert.Equal(
            (long)aggregate.SubscriberFailures.Count,
            context.Streaming.GetMetrics().SubscriberFailureCount);
        Assert.All(
            aggregate.SubscriberFailures,
            failure =>
            {
                Assert.Equal("CellStateChanged", failure.Notification);
                Assert.Contains(nameof(FailingStreamingSubscribers), failure.Subscriber, StringComparison.Ordinal);
                Assert.Contains("Cell=", failure.Payload, StringComparison.Ordinal);
            });
        Assert.Contains(
            aggregate.SubscriberFailures,
            failure => failure.Message == "cell subscriber one failed");
        Assert.Contains(
            aggregate.SubscriberFailures,
            failure => failure.Message == "cell subscriber two failed");
    }

    [Fact]
    public void ActiveWorldNotifications_AggregateCloseAndOpenFailuresWithinLoadBoundary()
    {
        using var context = new StreamingContext();
        var subscribers = new FailingStreamingSubscribers();
        context.Streaming.ActiveWorldChanged += subscribers.WorldFailureOne;
        context.Streaming.ActiveWorldChanged += subscribers.WorldSuccessOne;
        context.Streaming.ActiveWorldChanged += subscribers.WorldFailureTwo;
        context.Streaming.ActiveWorldChanged += subscribers.WorldSuccessTwo;
        AssetRef<WorldSourceAsset> world = context.Streaming.ActiveWorldAsset!.Value;

        RuntimeWorldLoadResult reloaded = context.Streaming.LoadWorld(world);

        context.Streaming.ActiveWorldChanged -= subscribers.WorldFailureOne;
        context.Streaming.ActiveWorldChanged -= subscribers.WorldSuccessOne;
        context.Streaming.ActiveWorldChanged -= subscribers.WorldFailureTwo;
        context.Streaming.ActiveWorldChanged -= subscribers.WorldSuccessTwo;

        Assert.True(reloaded.Success, reloaded.Diagnostic);
        Assert.Equal(
            [
                "world-failure-one", "world-success-one", "world-failure-two", "world-success-two",
                "world-failure-one", "world-success-one", "world-failure-two", "world-success-two"
            ],
            subscribers.Invocations);
        WorldStreamingDiagnostic aggregate = Assert.Single(
            context.Streaming.GetDiagnostics(),
            diagnostic => diagnostic.Kind == WorldStreamingDiagnosticKind.SubscriberAggregate);
        Assert.Equal("LoadWorld", aggregate.Boundary);
        Assert.Equal(4, aggregate.SubscriberFailures.Count);
        Assert.Equal(4, context.Streaming.GetMetrics().SubscriberFailureCount);
        Assert.All(
            aggregate.SubscriberFailures,
            failure =>
            {
                Assert.Equal("ActiveWorldChanged", failure.Notification);
                Assert.Contains(nameof(FailingStreamingSubscribers), failure.Subscriber, StringComparison.Ordinal);
            });
        Assert.Equal(2, aggregate.SubscriberFailures.Count(failure => failure.Payload == "World=<closed>"));
        Assert.Equal(2, aggregate.SubscriberFailures.Count(failure => failure.Payload.Contains(
            $"World={world.Guid:D}",
            StringComparison.Ordinal)));
    }

    [Fact]
    public void PresentationNotifications_AggregateFailuresAndRunEverySubscriberInOrder()
    {
        using var context = new StreamingContext();
        var subscribers = new FailingStreamingSubscribers();
        context.Streaming.WorldPresentationChanged += subscribers.PresentationFailureOne;
        context.Streaming.WorldPresentationChanged += subscribers.PresentationSuccessOne;
        context.Streaming.WorldPresentationChanged += subscribers.PresentationFailureTwo;
        context.Streaming.WorldPresentationChanged += subscribers.PresentationSuccessTwo;

        RuntimeWorldLoadResult reloaded = context.Streaming.LoadWorld(context.WorldAsset);

        context.Streaming.WorldPresentationChanged -= subscribers.PresentationFailureOne;
        context.Streaming.WorldPresentationChanged -= subscribers.PresentationSuccessOne;
        context.Streaming.WorldPresentationChanged -= subscribers.PresentationFailureTwo;
        context.Streaming.WorldPresentationChanged -= subscribers.PresentationSuccessTwo;

        Assert.True(reloaded.Success, reloaded.Diagnostic);
        Assert.Equal(0, subscribers.Invocations.Count % 4);
        Assert.Equal(3, subscribers.Invocations.Count / 4);
        for (int index = 0; index < subscribers.Invocations.Count; index += 4)
        {
            Assert.Equal(
                [
                    "presentation-failure-one",
                    "presentation-success-one",
                    "presentation-failure-two",
                    "presentation-success-two"
                ],
                subscribers.Invocations.Skip(index).Take(4));
        }

        WorldStreamingDiagnostic aggregate = Assert.Single(
            context.Streaming.GetDiagnostics(),
            diagnostic => diagnostic.Kind == WorldStreamingDiagnosticKind.SubscriberAggregate);
        Assert.Equal("LoadWorld", aggregate.Boundary);
        Assert.Equal(6, aggregate.SubscriberFailures.Count);
        Assert.Equal(6, context.Streaming.GetMetrics().SubscriberFailureCount);
        Assert.All(
            aggregate.SubscriberFailures,
            failure =>
            {
                Assert.Equal("WorldPresentationChanged", failure.Notification);
                Assert.Contains("Revision=", failure.Payload, StringComparison.Ordinal);
                Assert.Contains("Active=", failure.Payload, StringComparison.Ordinal);
                Assert.Contains("Pending=", failure.Payload, StringComparison.Ordinal);
                Assert.Contains("ActiveWorldGuid=", failure.Payload, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void PresentationSnapshot_RevisionsAdvanceAcrossSupersessionActivationAndShutdown()
    {
        var provider = new GatePreparedProvider();
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider,
            activateInitialWorld: false);
        RuntimeWorldPresentationSnapshot initial = context.Streaming.PresentationSnapshot;
        Assert.Equal(1, initial.Revision);
        Assert.Null(initial.ActiveWorldAsset);
        Assert.Equal(context.WorldAsset, initial.PendingWorldAsset);
        Assert.Equal(Guid.Empty, initial.ActiveWorldGuid);

        var observed = new List<RuntimeWorldPresentationSnapshot>();
        context.Streaming.WorldPresentationChanged += snapshot =>
        {
            Assert.Equal(snapshot, context.Streaming.PresentationSnapshot);
            observed.Add(snapshot);
        };

        RuntimeWorldLoadResult superseded = context.Streaming.LoadWorld(context.WorldAsset);
        Assert.True(superseded.Success, superseded.Diagnostic);
        Assert.True(superseded.Deferred);
        provider.Ready = true;
        context.Streaming.ProcessAtFrameBoundary();
        context.Streaming.Shutdown(unloadActiveCells: true);

        Assert.Equal(3, observed.Count);
        Assert.Equal(initial.Revision + 1, observed[0].Revision);
        Assert.Null(observed[0].ActiveWorldAsset);
        Assert.Equal(context.WorldAsset, observed[0].PendingWorldAsset);
        Assert.Equal(initial.Revision + 2, observed[1].Revision);
        Assert.Equal(context.WorldAsset, observed[1].ActiveWorldAsset);
        Assert.Null(observed[1].PendingWorldAsset);
        Assert.Equal(context.WorldAsset.Guid, observed[1].ActiveWorldGuid);
        Assert.Equal(initial.Revision + 3, observed[2].Revision);
        Assert.Null(observed[2].ActiveWorldAsset);
        Assert.Null(observed[2].PendingWorldAsset);
        Assert.Equal(Guid.Empty, observed[2].ActiveWorldGuid);
    }

    [Fact]
    public void FailedSupersessionCleanupPublishesTerminalStateAndAllowsRetry()
    {
        var provider = new GatePreparedProvider { Ready = true };
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider);
        RuntimeWorldPresentationSnapshot initial = context.Streaming.PresentationSnapshot;
        var presentations = new List<RuntimeWorldPresentationSnapshot>();
        var activeWorlds = new List<AssetRef<WorldSourceAsset>?>();
        context.Streaming.WorldPresentationChanged += presentations.Add;
        context.Streaming.ActiveWorldChanged += activeWorlds.Add;
        provider.FailNextRelease = true;

        RuntimeWorldLoadResult failed = context.Streaming.LoadWorld(context.WorldAsset);

        Assert.False(failed.Success);
        Assert.Contains("cleanup failed", failed.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.False(context.Streaming.IsShuttingDown);
        Assert.Null(context.Streaming.ActiveWorld);
        Assert.Null(context.Streaming.ActiveWorldAsset);
        Assert.Equal(3, presentations.Count);
        Assert.Equal(initial.Revision + 1, presentations[0].Revision);
        Assert.Equal(context.WorldAsset, presentations[0].ActiveWorldAsset);
        Assert.Equal(context.WorldAsset, presentations[0].PendingWorldAsset);
        Assert.Equal(initial.Revision + 2, presentations[1].Revision);
        Assert.Null(presentations[1].ActiveWorldAsset);
        Assert.Equal(context.WorldAsset, presentations[1].PendingWorldAsset);
        Assert.Equal(initial.Revision + 3, presentations[2].Revision);
        Assert.Null(presentations[2].ActiveWorldAsset);
        Assert.Null(presentations[2].PendingWorldAsset);
        Assert.Equal(Guid.Empty, presentations[2].ActiveWorldGuid);
        Assert.Single(activeWorlds);
        Assert.Null(activeWorlds[0]);

        context.Streaming.ProcessAtFrameBoundary();
        Assert.Empty(context.Residency.GetResources());
        Assert.True(provider.ReleaseAttemptCount > provider.ReleaseCount);

        RuntimeWorldLoadResult retry = context.Streaming.LoadWorld(context.WorldAsset);

        Assert.True(retry.Success, retry.Diagnostic);
        context.PumpUntil(() => context.Streaming.ActiveWorldAsset == context.WorldAsset);
        Assert.Equal(context.WorldAsset, context.Streaming.ActiveWorldAsset);
    }

    [Fact]
    public void FailedSupersedingWorldPublishesPendingWinnerThenTerminalEmptySnapshot()
    {
        var provider = new GatePreparedProvider();
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider,
            activateInitialWorld: false);
        RuntimeWorldPresentationSnapshot initial = context.Streaming.PresentationSnapshot;
        var replacement = new AssetRef<WorldSourceAsset>(
            Guid.Parse("83000000-0000-0000-0000-000000000099"),
            "World",
            "com.arisen.test");
        var observed = new List<RuntimeWorldPresentationSnapshot>();
        context.Streaming.WorldPresentationChanged += observed.Add;

        RuntimeWorldLoadResult failed = context.Streaming.LoadWorld(replacement);

        Assert.False(failed.Success);
        Assert.Equal(2, observed.Count);
        Assert.Equal(initial.Revision + 1, observed[0].Revision);
        Assert.Equal(replacement, observed[0].PendingWorldAsset);
        Assert.Null(observed[0].ActiveWorldAsset);
        Assert.Equal(initial.Revision + 2, observed[1].Revision);
        Assert.Null(observed[1].ActiveWorldAsset);
        Assert.Null(observed[1].PendingWorldAsset);
        Assert.Equal(observed[1], context.Streaming.PresentationSnapshot);
    }

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
    public void DeferredPersistentResidency_ActivatesOnlyAfterProviderBecomesReady()
    {
        var provider = new GatePreparedProvider();
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider,
            activateInitialWorld: false);

        Assert.Null(context.Streaming.ActiveWorld);
        Assert.Equal(
            context.WorldAsset,
            context.Streaming.PresentationSnapshot.PendingWorldAsset);
        Assert.DoesNotContain(
            context.SceneService.GetSceneInstances(),
            instance => instance.Kind == RuntimeSceneInstanceKind.Persistent &&
                        instance.State == RuntimeSceneInstanceState.Active);
        Assert.Equal(0, context.World.EntityCount);
        Assert.Throws<InvalidOperationException>(() =>
            context.SceneService.LoadScene(context.CellSceneRef(0)));
        Assert.Throws<InvalidOperationException>(() =>
            context.SceneService.RequestSceneLoad(context.CellSceneRef(0)));
        Assert.Throws<InvalidOperationException>(() =>
            context.SceneService.RequestAdditiveSceneLoad(context.CellSceneRef(0)));
        Assert.Empty(context.SceneService.GetSceneInstances());

        provider.Ready = true;
        context.Streaming.ProcessAtFrameBoundary();

        Assert.Equal(context.WorldAsset.Guid, context.Streaming.ActiveWorldAsset!.Value.Guid);
        Assert.Null(context.Streaming.PresentationSnapshot.PendingWorldAsset);
        Assert.Contains(
            context.SceneService.GetSceneInstances(),
            instance => instance.Kind == RuntimeSceneInstanceKind.Persistent &&
                        instance.State == RuntimeSceneInstanceState.Active);
        Assert.Equal(1, context.World.EntityCount);
    }

    [Fact]
    public void DeferredPersistentResidency_UsesExactlyOneSetupPassPerBoundary()
    {
        var provider = new GatePreparedProvider
        {
            Ready = true,
            WaitForFirstPrepare = true
        };
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider,
            residencyBudgets: new RuntimeAssetResidencyBudgets(
                MaxCpuCookedBytes: 512L * 1024 * 1024,
                MaxPreparedGpuBytes: 1024L * 1024 * 1024,
                MaxSetupsPerFrame: 4,
                MaxSetupMilliseconds: 100,
                MaxInactiveResources: 0),
            activateInitialWorld: false);

        int dependencyCount = context.Residency.GetResources().Count;
        Assert.True(dependencyCount > 0);

        context.Streaming.ProcessAtFrameBoundary();

        // The first pass prepares each key once. The intentionally waiting key must
        // not be retried by a second setup pass in the same engine frame.
        Assert.Null(context.Streaming.ActiveWorld);
        Assert.Equal(dependencyCount, provider.PrepareCount);
        Assert.Equal(dependencyCount, context.Residency.GetMetrics().SetupCount);
        Assert.Contains(
            context.Residency.GetResources(),
            resource => resource.PreparedState == RuntimePreparedAssetState.Waiting);

        context.Streaming.ProcessAtFrameBoundary();

        Assert.NotNull(context.Streaming.ActiveWorld);
        Assert.Equal(dependencyCount + 1, provider.PrepareCount);
        Assert.Equal(dependencyCount + 1, context.Residency.GetMetrics().SetupCount);
    }

    [Fact]
    public void DeferredPersistentWorldReplacement_ReleasesOnlyTheSupersededPendingOwner()
    {
        var provider = new GatePreparedProvider();
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider,
            activateInitialWorld: false);

        RuntimeAssetResidencyOwnerId firstOwner = Assert.Single(
            context.Residency.GetResources()
                .SelectMany(resource => resource.Owners)
                .Distinct());

        RuntimeWorldLoadResult replacement = context.Streaming.LoadWorld(context.WorldAsset);

        Assert.True(replacement.Success, replacement.Diagnostic);
        Assert.True(replacement.Deferred);
        RuntimeAssetResidencyOwnerId[] owners = context.Residency.GetResources()
            .SelectMany(resource => resource.Owners)
            .Distinct()
            .ToArray();
        Assert.Single(owners);
        Assert.NotEqual(firstOwner, owners[0]);
        Assert.DoesNotContain(firstOwner, owners);
        Assert.Equal(1, context.Residency.GetMetrics().ActiveOwnerCount);
        Assert.Throws<InvalidOperationException>(() =>
            context.SceneService.LoadScene(context.CellSceneRef(0)));
    }

    [Fact]
    public void PersistentSceneReload_PreservesActiveCellInstanceAndSubsequentUnload()
    {
        using var context = new StreamingContext();
        WorldCellId cellId = context.CellId(0);
        Assert.True(context.Streaming.PinCell(cellId));
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Active);

        WorldCellStreamingSnapshot active = context.Cell(cellId);
        RuntimeSceneInstanceId cellInstanceId = active.SceneInstanceId;
        Assert.True(context.SceneService.TryResolveEntity(
            cellInstanceId,
            StreamingContext.EntityGuid(1),
            out Entity cellEntity));

        context.SceneService.RequestSceneLoad(context.SceneService.ActiveScene!.Scene);
        context.Streaming.ProcessAtFrameBoundary();
        Assert.Equal(WorldCellStreamingState.Active, context.State(cellId));
        Assert.Equal(cellInstanceId, context.Cell(cellId).SceneInstanceId);
        Assert.True(context.World.IsAlive(cellEntity));

        Assert.True(context.Streaming.UnpinCell(cellId));
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Unloaded);
        Assert.DoesNotContain(
            "not active and cannot be unloaded",
            context.Cell(cellId).Diagnostic,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicSceneUnloadRejectsWorldOwnedCellUntilStreamingUnpinsIt()
    {
        var provider = new GatePreparedProvider { Ready = true };
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider);
        WorldCellId cellId = context.CellId(0);
        Assert.True(context.Streaming.PinCell(cellId));
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Active);

        WorldCellStreamingSnapshot activeCell = context.Cell(cellId);
        RuntimeSceneInstanceId cellInstanceId = activeCell.SceneInstanceId;
        Assert.True(context.SceneService.TryGetSceneInstance(
            cellInstanceId,
            out RuntimeSceneInstanceSnapshot activeInstance));
        Assert.Equal(RuntimeSceneInstanceState.Active, activeInstance.State);
        Assert.Equal(cellId, activeInstance.WorldCellId);
        Assert.True(context.SceneService.TryResolveEntity(
            cellInstanceId,
            StreamingContext.EntityGuid(1),
            out Entity cellEntity));
        RuntimeAssetResidencyOwnerId cellOwner = Assert.Single(
            context.Residency.GetResources()
                .SelectMany(resource => resource.Owners)
                .Distinct(),
            owner => owner.Kind == RuntimeAssetResidencyOwnerKind.WorldCell &&
                     owner.CellId == cellId);

        Assert.False(context.SceneService.RequestSceneUnload(cellInstanceId));
        context.Streaming.ProcessAtFrameBoundary();

        Assert.Equal(WorldCellStreamingState.Active, context.State(cellId));
        Assert.Equal(cellInstanceId, context.Cell(cellId).SceneInstanceId);
        Assert.True(context.World.IsAlive(cellEntity));
        Assert.True(context.SceneService.TryGetSceneInstance(
            cellInstanceId,
            out RuntimeSceneInstanceSnapshot preservedInstance));
        Assert.Equal(RuntimeSceneInstanceState.Active, preservedInstance.State);
        Assert.Contains(
            cellOwner,
            context.Residency.GetResources().SelectMany(resource => resource.Owners));

        Assert.True(context.Streaming.UnpinCell(cellId));
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Unloaded);

        Assert.False(context.World.IsAlive(cellEntity));
        Assert.False(context.SceneService.TryGetEntityOwner(cellEntity, out _));
        Assert.True(context.SceneService.TryGetSceneInstance(
            cellInstanceId,
            out RuntimeSceneInstanceSnapshot unloadedInstance));
        Assert.Equal(RuntimeSceneInstanceState.Unloaded, unloadedInstance.State);
        Assert.DoesNotContain(
            context.Residency.GetResources().SelectMany(resource => resource.Owners),
            owner => owner.Kind == RuntimeAssetResidencyOwnerKind.WorldCell &&
                     owner.CellId == cellId);
        Assert.Equal(1, context.Residency.GetMetrics().ActiveOwnerCount);
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
    public void PinningDependentCell_SeparatesEditAndRuntimeDemandProvenance()
    {
        using var context = new StreamingContext(loadRadius: 0);
        WorldCellId prerequisite = context.CellId(0);
        WorldCellId dependent = context.CellId(1);

        Assert.True(context.Streaming.PinCell(dependent));
        context.Streaming.ProcessAtFrameBoundary();

        Assert.Equal(
            WorldCellDesiredSource.EditDependency,
            context.Cell(prerequisite).DesiredSources);
        Assert.Equal(WorldCellDesiredSource.EditPin, context.Cell(dependent).DesiredSources);
        Assert.True(context.Cell(prerequisite).Desired);
        Assert.True(context.Cell(dependent).Desired);
        context.PumpUntil(() =>
            context.State(prerequisite) == WorldCellStreamingState.Active &&
            context.State(dependent) == WorldCellStreamingState.Active);

        var provenanceChanges = new List<WorldCellStreamingSnapshot>();
        context.Streaming.CellStateChanged += snapshot => provenanceChanges.Add(snapshot);

        context.Streaming.SetStreamingSource(new WorldPosition(110, 10, 10));
        context.Streaming.ProcessAtFrameBoundary();

        Assert.Equal(
            WorldCellDesiredSource.EditDependency | WorldCellDesiredSource.Runtime,
            context.Cell(prerequisite).DesiredSources);
        Assert.Equal(
            WorldCellDesiredSource.EditPin | WorldCellDesiredSource.Runtime,
            context.Cell(dependent).DesiredSources);
        Assert.Contains(provenanceChanges, snapshot =>
            snapshot.CellId == prerequisite &&
            snapshot.DesiredSources ==
            (WorldCellDesiredSource.EditDependency | WorldCellDesiredSource.Runtime));
        Assert.Contains(provenanceChanges, snapshot =>
            snapshot.CellId == dependent &&
            snapshot.DesiredSources ==
            (WorldCellDesiredSource.EditPin | WorldCellDesiredSource.Runtime));

        provenanceChanges.Clear();
        Assert.True(context.Streaming.UnpinCell(dependent));
        context.Streaming.ProcessAtFrameBoundary();

        Assert.Equal(WorldCellDesiredSource.Runtime, context.Cell(prerequisite).DesiredSources);
        Assert.Equal(WorldCellDesiredSource.Runtime, context.Cell(dependent).DesiredSources);
        Assert.Contains(provenanceChanges, snapshot =>
            snapshot.CellId == prerequisite &&
            snapshot.DesiredSources == WorldCellDesiredSource.Runtime);
        Assert.Contains(provenanceChanges, snapshot =>
            snapshot.CellId == dependent &&
            snapshot.DesiredSources == WorldCellDesiredSource.Runtime);

        provenanceChanges.Clear();
        context.Streaming.ClearStreamingSource();
        context.Streaming.ProcessAtFrameBoundary();

        Assert.Equal(WorldCellDesiredSource.None, context.Cell(prerequisite).DesiredSources);
        Assert.Equal(WorldCellDesiredSource.None, context.Cell(dependent).DesiredSources);
        Assert.False(context.Cell(prerequisite).Desired);
        Assert.False(context.Cell(dependent).Desired);
        Assert.Contains(provenanceChanges, snapshot =>
            snapshot.CellId == prerequisite &&
            snapshot.DesiredSources == WorldCellDesiredSource.None);
        Assert.Contains(provenanceChanges, snapshot =>
            snapshot.CellId == dependent &&
            snapshot.DesiredSources == WorldCellDesiredSource.None);
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
    public void ActivationValidatorRejectsBeforeEcsMutationAndReceivesWorldCellContext()
    {
        var codec = new ActivationProbeCodec();
        using var context = new StreamingContext(loaderFactory: database =>
            new ActivationProbePayloadLoader(new WorldCellPayloadLoader(database), codec));
        WorldCellId cellId = context.CellId(0);
        Assert.True(context.Streaming.PinCell(cellId));

        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Failed);

        Assert.Equal(1, codec.ValidationCount);
        Assert.True(codec.LastContext.HasValue);
        SceneComponentActivationContext activationContext = codec.LastContext.Value;
        Assert.Equal(cellId, activationContext.WorldCellId);
        Assert.Equal(new WorldPosition(0.0, 0.0, 0.0), activationContext.CellOrigin);
        Assert.Equal(
            new WorldBounds(
                new WorldPosition(0.0, 0.0, 0.0),
                new WorldPosition(100.0, 100.0, 100.0)),
            activationContext.CellBounds);
        Assert.Contains("Injected activation rejection", context.Cell(cellId).Diagnostic);
        Assert.Equal(1, context.World.EntityCount);
        Assert.Equal(0, codec.AddToEntityCount);
        Assert.DoesNotContain(
            context.SceneService.GetSceneInstances(),
            instance => instance.Kind == RuntimeSceneInstanceKind.Additive);

        codec.AllowActivation = true;
        Assert.True(context.Streaming.RetryCell(cellId));
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Active);

        Assert.Equal(2, codec.ValidationCount);
        Assert.Equal(1, codec.AddToEntityCount);
        Assert.Equal(2, context.World.EntityCount);
        Assert.True(context.SceneService.TryResolveEntity(
            context.Cell(cellId).SceneInstanceId,
            StreamingContext.EntityGuid(1),
            out Entity activatedEntity));
        Assert.Equal(7, context.World.GetComponent<ActivationProbeComponent>(activatedEntity).Value);
        Assert.Contains(
            context.SceneService.GetSceneInstances(),
            instance =>
                instance.Kind == RuntimeSceneInstanceKind.Additive &&
                instance.State == RuntimeSceneInstanceState.Active);
    }

    [Fact]
    public async Task PreviewReloadDuringClaimedActivationUnloadsSupersededSceneBeforeRequeue()
    {
        var codec = new ActivationProbeCodec
        {
            AllowActivation = true,
            BlockActivation = true
        };
        var provider = new GatePreparedProvider { Ready = true };
        using var context = new StreamingContext(
            loaderFactory: database =>
                new ActivationProbePayloadLoader(new WorldCellPayloadLoader(database), codec),
            includeSharedRenderAssets: true,
            preparedProvider: provider);
        WorldCellId cellId = context.CellId(0);
        Assert.True(context.Streaming.PinCell(cellId));

        string path = context.CellScenePath(0);
        string diskSource = File.ReadAllText(path);
        string previewSource = diskSource.Replace(
            "Position: { X: 1, Y: 0, Z: 0 }",
            "Position: { X: 42, Y: 0, Z: 0 }",
            StringComparison.Ordinal);
        Assert.NotEqual(diskSource, previewSource);

        Task activationFrame = Task.Run(() =>
        {
            while (!codec.ValidationStarted.IsSet)
            {
                context.Streaming.ProcessAtFrameBoundary();
                Thread.Yield();
            }
        });
        try
        {
            Assert.True(codec.ValidationStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(WorldCellStreamingState.ReadyToActivate, context.State(cellId));
            Assert.True(context.Streaming.SetCellPreviewSource(
                cellId,
                new SceneSourceSnapshot(
                    context.CellSceneRef(0),
                    path,
                    previewSource,
                    11)));
            Assert.True(context.Cell(cellId).ReloadRequested);
        }
        finally
        {
            codec.ContinueValidation.Set();
        }

        await activationFrame.WaitAsync(TimeSpan.FromSeconds(5));
        WorldCellStreamingSnapshot requeued = context.Cell(cellId);
        Assert.Equal(WorldCellStreamingState.Queued, requeued.State);
        Assert.False(requeued.ReloadRequested);
        Assert.False(requeued.SceneInstanceId.IsValid);
        Assert.Equal(1, context.World.EntityCount);
        Assert.DoesNotContain(
            context.SceneService.GetSceneInstances(),
            instance => instance.Kind == RuntimeSceneInstanceKind.Additive);
        Assert.NotEmpty(context.Residency.GetResources());
        Assert.All(
            context.Residency.GetResources(),
            resource =>
            {
                Assert.Equal(1, resource.OwnerCount);
                Assert.DoesNotContain(
                    resource.Owners,
                    owner => owner.Kind == RuntimeAssetResidencyOwnerKind.WorldCell);
            });

        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Active);
        Assert.Equal(2, context.World.EntityCount);
        Assert.True(context.SceneService.TryResolveEntity(
            context.Cell(cellId).SceneInstanceId,
            StreamingContext.EntityGuid(1),
            out Entity previewEntity));
        Assert.Equal(42, context.World.GetComponent<TransformComponent>(previewEntity).Position.X);
        Assert.NotEmpty(context.Residency.GetResources());
        Assert.All(
            context.Residency.GetResources(),
            resource => Assert.Equal(2, resource.OwnerCount));
    }

    [Fact]
    public async Task WorldReloadDrainsClaimedActivationBeforeReplacingOwnership()
    {
        var codec = new ActivationProbeCodec
        {
            AllowActivation = true,
            BlockActivation = true
        };
        var provider = new GatePreparedProvider { Ready = true };
        using var context = new StreamingContext(
            loaderFactory: database =>
                new ActivationProbePayloadLoader(new WorldCellPayloadLoader(database), codec),
            includeSharedRenderAssets: true,
            preparedProvider: provider);
        WorldCellId cellId = context.CellId(0);
        Assert.True(context.Streaming.PinCell(cellId));

        Task activationFrame = Task.Run(() =>
        {
            while (!codec.ValidationStarted.IsSet)
            {
                context.Streaming.ProcessAtFrameBoundary();
                Thread.Yield();
            }
        });
        Task<RuntimeWorldLoadResult>? worldReload = null;
        try
        {
            Assert.True(codec.ValidationStarted.Wait(TimeSpan.FromSeconds(5)));
            worldReload = Task.Run(() => context.Streaming.LoadWorld(context.WorldAsset));
            Assert.True(SpinWait.SpinUntil(
                () => context.Streaming.PendingLifecycleOperationCount == 1,
                TimeSpan.FromSeconds(5)));
            Assert.False(context.Streaming.IsShuttingDown);
            Assert.False(worldReload.IsCompleted);
        }
        finally
        {
            codec.ContinueValidation.Set();
        }

        await activationFrame.WaitAsync(TimeSpan.FromSeconds(5));
        RuntimeWorldLoadResult reloaded = await worldReload!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(reloaded.Success, reloaded.Diagnostic);
        if (reloaded.Deferred)
        {
            context.PumpUntil(() => context.Streaming.ActiveWorld != null);
        }
        Assert.False(context.Streaming.IsShuttingDown);
        Assert.All(
            context.Streaming.GetCells(),
            cell => Assert.Equal(WorldCellStreamingState.Unloaded, cell.State));
        Assert.Equal(1, context.World.EntityCount);
        Assert.DoesNotContain(
            context.SceneService.GetSceneInstances(),
            instance => instance.Kind == RuntimeSceneInstanceKind.Additive);
        Assert.NotEmpty(context.Residency.GetResources());
        Assert.All(
            context.Residency.GetResources(),
            resource =>
            {
                Assert.Equal(1, resource.OwnerCount);
                Assert.DoesNotContain(
                    resource.Owners,
                    owner => owner.Kind == RuntimeAssetResidencyOwnerKind.WorldCell);
            });

        Assert.True(context.Streaming.PinCell(cellId));
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Active);
        Assert.Equal(2, context.World.EntityCount);
        Assert.Single(
            context.SceneService.GetSceneInstances(),
            instance => instance.Kind == RuntimeSceneInstanceKind.Additive);
    }

    [Fact]
    public void ReentrantWorldLoadFromActivationValidatorFailsWithoutDeadlock()
    {
        var codec = new ActivationProbeCodec { AllowActivation = true };
        using var context = new StreamingContext(loaderFactory: database =>
            new ActivationProbePayloadLoader(new WorldCellPayloadLoader(database), codec));
        codec.ValidationAction = () => context.Streaming.LoadWorld(context.WorldAsset);
        WorldCellId cellId = context.CellId(0);
        Assert.True(context.Streaming.PinCell(cellId));

        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Failed);

        Assert.Contains("cannot run reentrantly", context.Cell(cellId).Diagnostic);
        Assert.Equal(1, context.World.EntityCount);
        Assert.DoesNotContain(
            context.SceneService.GetSceneInstances(),
            instance => instance.Kind == RuntimeSceneInstanceKind.Additive);
        Assert.False(context.Streaming.IsShuttingDown);
        Assert.Equal(0, context.Streaming.PendingLifecycleOperationCount);
    }

    [Fact]
    public void ReentrantWorldLoadFromPreparedPublicationFailsBeforeMutation()
    {
        var provider = new PreparedPublicationProbeProvider();
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider,
            activateInitialWorld: false);
        RuntimeWorldPresentationSnapshot before = context.Streaming.PresentationSnapshot;
        provider.Residency = context.Residency;
        provider.PublicationAction = () => context.Streaming.LoadWorld(context.WorldAsset);

        context.Residency.ProcessAtFrameBoundary();

        InvalidOperationException rejection = Assert.IsType<InvalidOperationException>(
            provider.PublicationFailure);
        Assert.Equal(
            "Runtime world lifecycle mutation cannot run reentrantly from an asset " +
            "residency callback.",
            rejection.Message);
        Assert.Equal(1, provider.PublicationCount);
        Assert.Equal(before, context.Streaming.PresentationSnapshot);
        Assert.Null(context.Streaming.ActiveWorld);
        Assert.Null(context.Streaming.ActiveWorldAsset);
        Assert.Equal(context.WorldAsset, context.Streaming.PresentationSnapshot.PendingWorldAsset);
        Assert.False(context.Streaming.IsShuttingDown);
        Assert.Equal(0, context.Streaming.PendingLifecycleOperationCount);

        context.Residency.ProcessAtFrameBoundary();

        Assert.Equal(before, context.Streaming.PresentationSnapshot);
        Assert.All(
            context.Residency.GetResources(),
            resource => Assert.Equal(RuntimePreparedAssetState.Ready, resource.PreparedState));

        context.Streaming.ProcessAtFrameBoundary();

        RuntimeWorldPresentationSnapshot activated = context.Streaming.PresentationSnapshot;
        Assert.Equal(before.Revision + 1, activated.Revision);
        Assert.Equal(context.WorldAsset, activated.ActiveWorldAsset);
        Assert.Null(activated.PendingWorldAsset);
        Assert.Equal(context.WorldAsset.Guid, activated.ActiveWorldGuid);
        Assert.Equal(context.WorldAsset, context.Streaming.ActiveWorldAsset);
        Assert.False(context.Streaming.IsShuttingDown);
        Assert.Equal(0, context.Streaming.PendingLifecycleOperationCount);
    }

    [Fact]
    public async Task ConcurrentReloadRejectsPreparedPublicationReentryBeforeLifecycleWait()
    {
        var provider = new PreparedPublicationProbeProvider
        {
            BlockPublication = true
        };
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider,
            activateInitialWorld: false);
        provider.Residency = context.Residency;
        provider.PublicationAction = () => context.Streaming.LoadWorld(context.WorldAsset);

        Task publication = Task.Run(context.Residency.ProcessAtFrameBoundary);
        Assert.True(provider.PublicationStarted.Wait(TimeSpan.FromSeconds(5)));
        Task<RuntimeWorldLoadResult> reload =
            Task.Run(() => context.Streaming.LoadWorld(context.WorldAsset));
        try
        {
            Assert.True(SpinWait.SpinUntil(
                () => context.Streaming.IsShuttingDown,
                TimeSpan.FromSeconds(5)));
        }
        finally
        {
            provider.ContinuePublication.Set();
        }

        await publication.WaitAsync(TimeSpan.FromSeconds(5));
        await reload.WaitAsync(TimeSpan.FromSeconds(5));

        InvalidOperationException rejection = Assert.IsType<InvalidOperationException>(
            provider.PublicationFailure);
        Assert.Equal(
            "Runtime world lifecycle mutation cannot run reentrantly from an asset " +
            "residency callback.",
            rejection.Message);
        Assert.Equal(1, provider.PublicationCount);
        Assert.Equal(0, context.Streaming.PendingLifecycleOperationCount);
    }

    [Fact]
    public void ReentrantWorldLoadFromPrepareCallbackFailsBeforeMutation()
    {
        var provider = new GatePreparedProvider { Ready = true };
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider,
            activateInitialWorld: false);
        RuntimeWorldPresentationSnapshot before = context.Streaming.PresentationSnapshot;
        provider.PrepareAction = () => context.Streaming.LoadWorld(context.WorldAsset);

        context.Residency.ProcessAtFrameBoundary();

        InvalidOperationException rejection = Assert.IsType<InvalidOperationException>(
            provider.PrepareFailure);
        Assert.Equal(
            "Runtime world lifecycle mutation cannot run reentrantly from an asset " +
            "residency callback.",
            rejection.Message);
        Assert.Equal(1, provider.PrepareCallbackCount);
        Assert.Equal(before, context.Streaming.PresentationSnapshot);
        Assert.Null(context.Streaming.ActiveWorld);
        Assert.Null(context.Streaming.ActiveWorldAsset);
        Assert.Equal(context.WorldAsset, context.Streaming.PresentationSnapshot.PendingWorldAsset);
        Assert.False(context.Streaming.IsShuttingDown);
        Assert.Equal(0, context.Streaming.PendingLifecycleOperationCount);

        context.Residency.ProcessAtFrameBoundary();

        Assert.Equal(before, context.Streaming.PresentationSnapshot);
        Assert.All(
            context.Residency.GetResources(),
            resource => Assert.Equal(RuntimePreparedAssetState.Ready, resource.PreparedState));

        context.Streaming.ProcessAtFrameBoundary();

        RuntimeWorldPresentationSnapshot activated = context.Streaming.PresentationSnapshot;
        Assert.Equal(before.Revision + 1, activated.Revision);
        Assert.Equal(context.WorldAsset, activated.ActiveWorldAsset);
        Assert.Null(activated.PendingWorldAsset);
        Assert.Equal(context.WorldAsset.Guid, activated.ActiveWorldGuid);
        Assert.Equal(context.WorldAsset, context.Streaming.ActiveWorldAsset);
        Assert.False(context.Streaming.IsShuttingDown);
        Assert.Equal(0, context.Streaming.PendingLifecycleOperationCount);
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
    public void WorldReloadRetainsScenesAndResidencyWhenBatchUnloadIsRejected()
    {
        var provider = new GatePreparedProvider { Ready = true };
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider);
        WorldCellId cellId = context.CellId(0);
        Assert.True(context.Streaming.PinCell(cellId));
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Active);
        RuntimeSceneInstanceId cellInstance = context.Cell(cellId).SceneInstanceId;
        Assert.True(context.SceneService.TryResolveEntity(
            cellInstance,
            StreamingContext.EntityGuid(1),
            out Entity cellEntity));
        Entity externalEntity = context.World.CreateEntity();
        context.World.AddComponent(
            externalEntity,
            new ParentComponent { Parent = cellEntity });

        RuntimeWorldLoadResult rejected = context.Streaming.LoadWorld(context.WorldAsset);

        Assert.False(rejected.Success);
        Assert.Contains("crosses the unload boundary", rejected.Diagnostic);
        Assert.False(context.Streaming.IsShuttingDown);
        Assert.Equal(WorldCellStreamingState.Active, context.State(cellId));
        Assert.Equal(cellInstance, context.Cell(cellId).SceneInstanceId);
        Assert.Equal(2, context.SceneService.GetSceneInstances().Count);
        Assert.Equal(3, context.World.EntityCount);
        Assert.NotEmpty(context.Residency.GetResources());
        Assert.All(
            context.Residency.GetResources(),
            resource => Assert.Equal(2, resource.OwnerCount));

        context.World.RemoveComponent<ParentComponent>(externalEntity);
        Assert.True(context.World.TryDestroyEntity(externalEntity));
        RuntimeWorldLoadResult reloaded = context.Streaming.LoadWorld(context.WorldAsset);
        Assert.True(reloaded.Success, reloaded.Diagnostic);
        if (reloaded.Deferred)
        {
            context.PumpUntil(() => context.Streaming.ActiveWorld != null);
        }
        Assert.All(
            context.Streaming.GetCells(),
            cell => Assert.Equal(WorldCellStreamingState.Unloaded, cell.State));
        Assert.Single(context.SceneService.GetSceneInstances());
        Assert.Equal(1, context.World.EntityCount);
        Assert.NotEmpty(context.Residency.GetResources());
        Assert.All(
            context.Residency.GetResources(),
            resource => Assert.Equal(1, resource.OwnerCount));
    }

    [Fact]
    public void PersistentResidencyFailureRejectsBeforeSceneActivation()
    {
        var provider = new GatePreparedProvider { Ready = true };
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider,
            residencyBudgets: new RuntimeAssetResidencyBudgets(
                MaxCpuCookedBytes: 8,
                MaxPreparedGpuBytes: 1024,
                MaxSetupsPerFrame: 4,
                MaxSetupMilliseconds: 100,
                MaxInactiveResources: 0));
        context.ReplacePersistentMeshGuid(StreamingContext.OversizedPersistentMeshGuid);

        RuntimeWorldLoadResult failed = context.Streaming.LoadWorld(context.WorldAsset);

        Assert.False(failed.Success);
        Assert.Contains("residency acquisition failed", failed.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.False(context.Streaming.IsShuttingDown);
        Assert.False(context.Streaming.PersistentSceneUnloadBlocked);
        Assert.Empty(context.Streaming.PersistentSceneDiagnostic);
        Assert.Null(context.Streaming.ActiveWorld);
        Assert.Null(context.Streaming.ActiveWorldAsset);
        Assert.Empty(context.Streaming.GetCells());
        Assert.Empty(context.SceneService.GetSceneInstances());
        Assert.Equal(0, context.World.EntityCount);
        Assert.Equal(0, context.Residency.GetMetrics().ActiveOwnerCount);
        Assert.All(context.Residency.GetResources(), resource => Assert.Empty(resource.Owners));
        context.Residency.ProcessAtFrameBoundary();
        Assert.Empty(context.Residency.GetResources());
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
        var provider = new GatePreparedProvider { Ready = true };
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider);
        WorldCellId cellId = context.CellId(0);
        context.Streaming.PinCell(cellId);
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Active);
        Assert.Equal(2, context.World.EntityCount);
        Assert.NotEmpty(context.Residency.GetResources());
        Assert.All(
            context.Residency.GetResources(),
            resource => Assert.Equal(2, resource.OwnerCount));

        context.Streaming.UnpinCell(cellId);
        context.PumpUntil(() => context.State(cellId) == WorldCellStreamingState.Unloaded);
        Assert.NotEmpty(context.Residency.GetResources());
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
        var deadline = Stopwatch.StartNew();
        for (uint frame = 0;
             deadline.Elapsed < TimeSpan.FromSeconds(5) && !scenario.IsReadyForShutdown;
             frame++)
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

    [Fact]
    public void BoundedSmokeScenario_DefersStartupUntilPersistentWorldActivates()
    {
        var provider = new GatePreparedProvider();
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider,
            activateInitialWorld: false);
        var visual = new RecordingVisualSummaryService();
        var scenario = new WorldStreamingSmokeScenario(
            new ArisenKernel.Lifecycle.RuntimeSmokeScenarioContext(
                "world-streaming",
                context.Root,
                "Development",
                Path.Combine(context.Root, "world-streaming-smoke.json"),
                visual),
            context.Streaming,
            context.SceneService,
            context.Residency,
            context.Origin,
            context.Database,
            context.TaskGraph);

        scenario.Start(7);
        Assert.Empty(visual.ScheduledCaptures);

        provider.Ready = true;
        context.Streaming.ProcessAtFrameBoundary();
        scenario.AfterFrame(7);

        (string Name, uint FrameIndex) capture = Assert.Single(visual.ScheduledCaptures);
        Assert.Equal("before", capture.Name);
        Assert.Equal(8u, capture.FrameIndex);
    }

    [Fact]
    public void DeferredPersistentActivationFailure_EvictsReleasedResourcesOnNextBoundary()
    {
        var provider = new GatePreparedProvider();
        var codec = new ActivationProbeCodec { AllowActivation = false };
        SceneComponentExtensionRegistry.Shared.Register(codec);
        try
        {
            using var context = new StreamingContext(
                includeSharedRenderAssets: true,
                preparedProvider: provider,
                activateInitialWorld: false,
                persistentExtensionCodec: codec);
            RuntimeWorldPresentationSnapshot pending = context.Streaming.PresentationSnapshot;
            var observed = new List<RuntimeWorldPresentationSnapshot>();
            context.Streaming.WorldPresentationChanged += observed.Add;
            provider.Ready = true;

            Assert.Throws<InvalidOperationException>(
                () => context.Streaming.ProcessAtFrameBoundary());
            RuntimeWorldPresentationSnapshot cleared = Assert.Single(observed);
            Assert.Equal(pending.Revision + 1, cleared.Revision);
            Assert.Null(cleared.ActiveWorldAsset);
            Assert.Null(cleared.PendingWorldAsset);
            Assert.Equal(cleared, context.Streaming.PresentationSnapshot);
            Assert.Equal(0, context.Residency.GetMetrics().ActiveOwnerCount);
            Assert.NotEmpty(context.Residency.GetResources());
            Assert.True(provider.GetMetrics().PreparedResourceCount > 0);

            context.Streaming.ProcessAtFrameBoundary();

            Assert.Empty(context.Residency.GetResources());
            Assert.Equal(0, provider.GetMetrics().PreparedResourceCount);

            SceneLoadResult reopened = context.SceneService.LoadScene(context.CellSceneRef(0));
            Assert.True(reopened.Success, reopened.Diagnostic);
        }
        finally
        {
            Assert.True(SceneComponentExtensionRegistry.Shared.Unregister(codec));
        }
    }

    [Fact]
    public void ShutdownOfDeferredPersistentWorld_ReleasesLeaseAndReopensSceneAdmission()
    {
        var provider = new GatePreparedProvider();
        using var context = new StreamingContext(
            includeSharedRenderAssets: true,
            preparedProvider: provider,
            activateInitialWorld: false);

        Assert.NotEmpty(
            context.Residency.GetResources()
                .SelectMany(resource => resource.Owners));
        Assert.Throws<InvalidOperationException>(() =>
            context.SceneService.LoadScene(context.CellSceneRef(0)));
        RuntimeWorldPresentationSnapshot pending = context.Streaming.PresentationSnapshot;
        var observed = new List<RuntimeWorldPresentationSnapshot>();
        context.Streaming.WorldPresentationChanged += observed.Add;

        context.Streaming.Shutdown(unloadActiveCells: true);

        RuntimeWorldPresentationSnapshot cleared = Assert.Single(observed);
        Assert.Equal(pending.Revision + 1, cleared.Revision);
        Assert.Null(cleared.ActiveWorldAsset);
        Assert.Null(cleared.PendingWorldAsset);
        Assert.Equal(cleared, context.Streaming.PresentationSnapshot);
        Assert.Null(context.Streaming.ActiveWorld);
        Assert.Empty(context.SceneService.GetSceneInstances());
        Assert.Equal(0, context.Residency.GetMetrics().ActiveOwnerCount);
        Assert.Empty(context.Residency.GetResources());

        SceneLoadResult reopened = context.SceneService.LoadScene(context.CellSceneRef(0));
        Assert.True(reopened.Success, reopened.Diagnostic);
    }

    [Fact]
    public void BoundedSmokeScenario_WarmsOneFrameBeforeInitialVisualCapture()
    {
        using var context = new StreamingContext(loadRadius: 2);
        var visual = new RecordingVisualSummaryService();
        var scenarioContext = new ArisenKernel.Lifecycle.RuntimeSmokeScenarioContext(
            "world-streaming",
            context.Root,
            "Development",
            Path.Combine(context.Root, "world-streaming-smoke.json"),
            visual);
        var scenario = new WorldStreamingSmokeScenario(
            scenarioContext,
            context.Streaming,
            context.SceneService,
            context.Residency,
            context.Origin,
            context.Database,
            context.TaskGraph);

        scenario.Start(41);

        (string Name, uint FrameIndex) capture = Assert.Single(visual.ScheduledCaptures);
        Assert.Equal("before", capture.Name);
        Assert.Equal(42u, capture.FrameIndex);
    }

    [Fact]
    public void BoundedSmokeScenario_CapturesStableNearMidFarCameraPath()
    {
        using var context = new StreamingContext(loadRadius: 2);
        var visual = new RecordingVisualSummaryService();
        var scenarioContext = new ArisenKernel.Lifecycle.RuntimeSmokeScenarioContext(
            "world-streaming",
            context.Root,
            "Development",
            Path.Combine(context.Root, "world-streaming-smoke.json"),
            visual);
        var scenario = new WorldStreamingSmokeScenario(
            scenarioContext,
            context.Streaming,
            context.SceneService,
            context.Residency,
            context.Origin,
            context.Database,
            context.TaskGraph);
        var cameraPool = context.World.GetPool<CameraComponent>();
        Entity cameraEntity = Assert.Single(
            cameraPool.GetRawEntityArray().AsSpan(0, cameraPool.Count).ToArray());
        WorldPosition originalCamera = context.Origin.ToWorld(
            context.World.GetComponent<TransformComponent>(cameraEntity).Position);

        scenario.Start(0);
        var deadline = Stopwatch.StartNew();
        for (uint frame = 0;
             deadline.Elapsed < TimeSpan.FromSeconds(5) && !scenario.IsReadyForShutdown;
             frame++)
        {
            scenario.BeforeFrame(frame);
            context.Streaming.ProcessAtFrameBoundary();
            visual.CompleteFrame(frame);
            scenario.AfterFrame(frame);
            Thread.Yield();
        }

        Assert.True(scenario.IsReadyForShutdown, scenario.FailureMessage);
        Assert.Equal(
            [
                "before",
                "during",
                "shadow-near",
                "shadow-mid",
                "shadow-far",
                "shadow-far-stable",
                "after"
            ],
            visual.ScheduledCaptures.Select(capture => capture.Name).ToArray());
        Assert.True(visual.ScheduledCaptures
            .Select(capture => capture.FrameIndex)
            .SequenceEqual(visual.ScheduledCaptures
                .Select(capture => capture.FrameIndex)
                .Order()));
        Assert.Equal(
            visual.ScheduledCaptures.Count,
            visual.ScheduledCaptures.Select(capture => capture.FrameIndex).Distinct().Count());
        uint farFrame = visual.ScheduledCaptures.Single(capture =>
            capture.Name == "shadow-far").FrameIndex;
        uint stableFrame = visual.ScheduledCaptures.Single(capture =>
            capture.Name == "shadow-far-stable").FrameIndex;
        Assert.Equal(farFrame + 1, stableFrame);
        WorldPosition restoredCamera = context.Origin.ToWorld(
            context.World.GetComponent<TransformComponent>(cameraEntity).Position);
        Assert.InRange(Math.Abs(restoredCamera.X - originalCamera.X), 0.0, 0.001);
        Assert.InRange(Math.Abs(restoredCamera.Y - originalCamera.Y), 0.0, 0.001);
        Assert.InRange(Math.Abs(restoredCamera.Z - originalCamera.Z), 0.0, 0.001);

        context.Streaming.Shutdown(unloadActiveCells: true);
        context.SceneService.ClearForShutdown();
        context.Database.ReleaseAllLoadedCookedAssets();
        scenario.AfterShutdown();
        Assert.True(scenario.Succeeded, scenario.FailureMessage);
    }

    private sealed class RecordingVisualSummaryService : ArisenKernel.Lifecycle.IRuntimeVisualSummaryService
    {
        public List<(string Name, uint FrameIndex)> ScheduledCaptures { get; } = [];
        private readonly List<ArisenKernel.Lifecycle.RuntimeVisualSummaryCaptureResult> m_Results = [];
        private bool m_Sealed;
        public bool IsEnabled => true;
        public uint CaptureFrameIndex => m_Results
            .Where(result => result.State == ArisenKernel.Lifecycle.RuntimeVisualSummaryCaptureState.Scheduled)
            .Select(result => result.Capture.FrameIndex)
            .DefaultIfEmpty(uint.MaxValue)
            .Min();
        public string ProfileName => "Development";
        public string OutputPath => "visual-summary.json";
        public bool IsComplete => m_Sealed && m_Results.Count > 0 && m_Results.All(result =>
            result.State is ArisenKernel.Lifecycle.RuntimeVisualSummaryCaptureState.Succeeded or
                ArisenKernel.Lifecycle.RuntimeVisualSummaryCaptureState.Failed);
        public bool Succeeded => IsComplete && m_Results.All(result =>
            result.State == ArisenKernel.Lifecycle.RuntimeVisualSummaryCaptureState.Succeeded);
        public string? FailureMessage { get; private set; }

        public bool TryScheduleCapture(string name, uint frameIndex, out string outputPath)
        {
            ScheduledCaptures.Add((name, frameIndex));
            outputPath = $"visual-summary.{name}.json";
            var capture = new ArisenKernel.Lifecycle.RuntimeVisualSummaryCapture(
                ScheduledCaptures.Count,
                name,
                frameIndex,
                outputPath);
            m_Results.Add(new ArisenKernel.Lifecycle.RuntimeVisualSummaryCaptureResult(
                capture,
                ArisenKernel.Lifecycle.RuntimeVisualSummaryCaptureState.Scheduled,
                null));
            return true;
        }

        public void CompleteFrame(uint frameIndex)
        {
            for (int index = 0; index < m_Results.Count; index++)
            {
                ArisenKernel.Lifecycle.RuntimeVisualSummaryCaptureResult result = m_Results[index];
                if (result.State == ArisenKernel.Lifecycle.RuntimeVisualSummaryCaptureState.Scheduled &&
                    result.Capture.FrameIndex == frameIndex)
                {
                    m_Results[index] = result with
                    {
                        State = ArisenKernel.Lifecycle.RuntimeVisualSummaryCaptureState.Succeeded
                    };
                }
            }
        }

        public bool TryBeginCapture(
            uint frameIndex,
            out ArisenKernel.Lifecycle.RuntimeVisualSummaryCapture capture)
        {
            capture = default;
            return false;
        }

        public void ReportSuccess(ArisenKernel.Lifecycle.RuntimeVisualSummaryCapture capture) =>
            throw new NotSupportedException();

        public void ReportFailure(
            ArisenKernel.Lifecycle.RuntimeVisualSummaryCapture capture,
            string message) => FailureMessage = message;

        public void ReportFailure(string message) => FailureMessage = message;

        public bool TryGetCaptureResult(
            string name,
            out ArisenKernel.Lifecycle.RuntimeVisualSummaryCaptureResult result)
        {
            ArisenKernel.Lifecycle.RuntimeVisualSummaryCaptureResult? match =
                m_Results.SingleOrDefault(candidate =>
                    string.Equals(candidate.Capture.Name, name, StringComparison.Ordinal));
            if (match == null)
            {
                result = null!;
                return false;
            }

            result = match;
            return true;
        }

        public IReadOnlyList<ArisenKernel.Lifecycle.RuntimeVisualSummaryCaptureResult>
            GetCaptureResults() => m_Results.ToArray();

        public void Seal()
        {
            m_Sealed = true;
        }
    }

    private sealed class FailingStreamingSubscribers
    {
        public List<string> Invocations { get; } = new();

        public void CellFailureOne(WorldCellStreamingSnapshot _)
        {
            Invocations.Add("cell-failure-one");
            throw new InvalidOperationException("cell subscriber one failed");
        }

        public void CellSuccessOne(WorldCellStreamingSnapshot _) => Invocations.Add("cell-success-one");

        public void CellFailureTwo(WorldCellStreamingSnapshot _)
        {
            Invocations.Add("cell-failure-two");
            throw new ArgumentException("cell subscriber two failed");
        }

        public void CellSuccessTwo(WorldCellStreamingSnapshot _) => Invocations.Add("cell-success-two");

        public void WorldFailureOne(AssetRef<WorldSourceAsset>? _)
        {
            Invocations.Add("world-failure-one");
            throw new InvalidOperationException("world subscriber one failed");
        }

        public void WorldSuccessOne(AssetRef<WorldSourceAsset>? _) => Invocations.Add("world-success-one");

        public void WorldFailureTwo(AssetRef<WorldSourceAsset>? _)
        {
            Invocations.Add("world-failure-two");
            throw new ArgumentException("world subscriber two failed");
        }

        public void WorldSuccessTwo(AssetRef<WorldSourceAsset>? _) => Invocations.Add("world-success-two");

        public void PresentationFailureOne(RuntimeWorldPresentationSnapshot _)
        {
            Invocations.Add("presentation-failure-one");
            throw new InvalidOperationException("presentation subscriber one failed");
        }

        public void PresentationSuccessOne(RuntimeWorldPresentationSnapshot _) =>
            Invocations.Add("presentation-success-one");

        public void PresentationFailureTwo(RuntimeWorldPresentationSnapshot _)
        {
            Invocations.Add("presentation-failure-two");
            throw new ArgumentException("presentation subscriber two failed");
        }

        public void PresentationSuccessTwo(RuntimeWorldPresentationSnapshot _) =>
            Invocations.Add("presentation-success-two");
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
        internal static readonly Guid OversizedPersistentMeshGuid =
            Guid.Parse("83000000-0000-0000-0000-000000000103");
        private readonly bool m_IncludeSharedRenderAssets;
        private readonly ISceneComponentExtensionCodec? m_PersistentExtensionCodec;

        public StreamingContext(
            Func<IAssetDatabase, IWorldCellPayloadLoader>? loaderFactory = null,
            WorldStreamingBudgets? budgets = null,
            int loadRadius = 0,
            int maxActiveCells = 8,
            bool includeSharedRenderAssets = false,
            IRuntimePreparedAssetProvider? preparedProvider = null,
            RuntimeAssetResidencyBudgets? residencyBudgets = null,
            bool activateInitialWorld = true,
            ISceneComponentExtensionCodec? persistentExtensionCodec = null)
        {
            m_IncludeSharedRenderAssets = includeSharedRenderAssets;
            m_PersistentExtensionCodec = persistentExtensionCodec;
            Root = Path.Combine(Path.GetTempPath(), "ArisenWorldStreamingTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(Root, "Cooked"));
            if (includeSharedRenderAssets)
            {
                AddCookedRenderAsset(s_SharedMeshGuid, "Mesh", RuntimeAssetVariantPolicy.StaticMesh);
                AddCookedRenderAsset(s_SharedMaterialGuid, "Material", RuntimeAssetVariantPolicy.Material);
                AddCookedRenderAsset(
                    OversizedPersistentMeshGuid,
                    "Mesh",
                    RuntimeAssetVariantPolicy.StaticMesh,
                    byteCount: 16);
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
            Residency = new RuntimeAssetResidencyService(Database, residencyBudgets);
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
            if (loaded.Deferred && activateInitialWorld)
            {
                PumpUntil(() => Streaming.ActiveWorld != null);
            }

            if (activateInitialWorld)
            {
                Assert.Equal(1, World.EntityCount);
            }
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
        public AssetRef<WorldSourceAsset> WorldAsset =>
            new(s_WorldGuid, "World", PackageId);

        public string PersistentScenePath => Path.Combine(Root, "Persistent.arisenscene");

        public void ReplacePersistentMeshGuid(Guid meshGuid)
        {
            if (!m_IncludeSharedRenderAssets)
            {
                throw new InvalidOperationException(
                    "Persistent mesh replacement requires shared render assets in the fixture.");
            }

            string source = File.ReadAllText(PersistentScenePath);
            string replaced = source.Replace(
                s_SharedMeshGuid.ToString("D"),
                meshGuid.ToString("D"),
                StringComparison.Ordinal);
            if (string.Equals(source, replaced, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The persistent fixture did not contain its shared mesh identity.");
            }

            File.WriteAllText(PersistentScenePath, replaced);
        }

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
            bool includeCamera = string.Equals(name, "Persistent", StringComparison.Ordinal);
            string cameraSchemaSuffix = includeCamera
                ? "\n- TypeId: 2\n  Name: Camera\n  Version: 2\n  Required: true"
                : string.Empty;
            string rendererSchemaSuffix = m_IncludeSharedRenderAssets
                ? "\n- TypeId: 3\n  Name: MeshRenderer\n  Version: 1\n  Required: true"
                : string.Empty;
            bool includePersistentExtension =
                includeCamera && m_PersistentExtensionCodec != null;
            string extensionSchemaSuffix = includePersistentExtension
                ? $"\n- TypeId: {m_PersistentExtensionCodec!.Schema.TypeId}\n  Name: " +
                  $"{m_PersistentExtensionCodec.Schema.Name}\n  Version: " +
                  $"{m_PersistentExtensionCodec.Schema.Version}\n  Required: " +
                  m_PersistentExtensionCodec.Schema.Required.ToString().ToLowerInvariant()
                : string.Empty;
            string schemaSuffix = cameraSchemaSuffix + rendererSchemaSuffix + extensionSchemaSuffix;
            string cameraSuffix = includeCamera
                ? "\n  Camera:\n    VerticalFov: 45\n    NearPlane: 0.1\n    FarPlane: 200\n    IsPerspective: true"
                : string.Empty;
            string rendererSuffix = m_IncludeSharedRenderAssets
                ? $"\n  MeshRenderer:\n    Mesh: {{ Guid: {s_SharedMeshGuid:D}, PackageId: {PackageId} }}" +
                  $"\n    Material: {{ Guid: {s_SharedMaterialGuid:D}, PackageId: {PackageId} }}"
                : string.Empty;
            string extensionSuffix = includePersistentExtension
                ? $"\n  {m_PersistentExtensionCodec!.Schema.Name}: {{}}"
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
                    Scale: { X: 1, Y: 1, Z: 1 }{{cameraSuffix}}{{rendererSuffix}}{{extensionSuffix}}
                """);
            Database.AddAsset(sceneGuid, "Scene", path, PackageId);
        }

        private void AddCookedRenderAsset(
            Guid guid,
            string assetType,
            string variant,
            int byteCount = 4)
        {
            string sourcePath = Path.Combine(Root, guid.ToString("N") + ".source");
            string cookedPath = Path.Combine(Root, guid.ToString("N") + ".cooked");
            File.WriteAllText(sourcePath, "test source");
            File.WriteAllBytes(cookedPath, new byte[byteCount]);
            Database.AddAsset(guid, assetType, sourcePath, PackageId);
            Database.RegisterCookedArtifact(new CookedAssetRecord(
                guid,
                assetType,
                variant,
                cookedPath,
                byteCount,
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
        private bool m_FirstPrepareWaited;

        public string ProviderId => "test.world-streaming-prepared";
        public bool Ready { get; set; }
        public bool WaitForFirstPrepare { get; set; }
        public bool FailNextRelease { get; set; }
        public int PrepareCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public int ReleaseAttemptCount { get; private set; }
        public Action? PrepareAction { get; set; }
        public Exception? PrepareFailure { get; private set; }
        public int PrepareCallbackCount { get; private set; }

        public bool Supports(string assetType) => assetType is "Mesh" or "Material";

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            PrepareCount++;
            Action? prepareAction = PrepareAction;
            if (prepareAction != null)
            {
                PrepareAction = null;
                PrepareCallbackCount++;
                try
                {
                    prepareAction();
                }
                catch (Exception error)
                {
                    PrepareFailure = error;
                }
            }

            if (WaitForFirstPrepare && !m_FirstPrepareWaited)
            {
                m_FirstPrepareWaited = true;
                return RuntimePreparedAssetResult.Waiting("Test provider intentionally waits once.");
            }

            if (!Ready) return RuntimePreparedAssetResult.Waiting("Test provider is gated.");
            m_Prepared.Add(key);
            return RuntimePreparedAssetResult.Ready(64);
        }

        public void Release(RuntimeAssetResidencyKey key)
        {
            ReleaseAttemptCount++;
            if (FailNextRelease)
            {
                FailNextRelease = false;
                throw new InvalidOperationException(
                    "Test provider intentionally fails one release.");
            }

            if (m_Prepared.Remove(key)) ReleaseCount++;
        }

        public RuntimePreparedAssetProviderMetrics GetMetrics() =>
            new(m_Prepared.Count, m_Prepared.Count * 64, 0);
    }

    private sealed class PreparedPublicationProbeProvider : IRuntimePreparedAssetProvider
    {
        private readonly HashSet<RuntimeAssetResidencyKey> m_Prepared = new();

        public string ProviderId => "test.world-streaming-publication-probe";
        public RuntimeAssetResidencyService? Residency { get; set; }
        public Action? PublicationAction { get; set; }
        public Exception? PublicationFailure { get; private set; }
        public int PublicationCount { get; private set; }
        public bool BlockPublication { get; set; }
        public ManualResetEventSlim PublicationStarted { get; } = new(false);
        public ManualResetEventSlim ContinuePublication { get; } = new(false);

        public bool Supports(string assetType) => assetType is "Mesh" or "Material";

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            if (!string.Equals(key.AssetType, "Mesh", StringComparison.Ordinal))
            {
                m_Prepared.Add(key);
                return RuntimePreparedAssetResult.Ready(64);
            }

            if (Residency == null || PublicationAction == null)
            {
                return RuntimePreparedAssetResult.Waiting(
                    "Prepared-publication probe is not configured.");
            }

            if (!Residency.TryGetPreparationClaim(
                    key,
                    out RuntimeAssetPreparationClaim claim))
            {
                return RuntimePreparedAssetResult.Waiting(
                    $"Runtime asset '{key}' has no current preparation claim.");
            }

            bool committed = Residency.TryCommitPreparedPublication(
                claim,
                [],
                [],
                estimatedGpuBytes: 64,
                () =>
                {
                    m_Prepared.Add(key);
                    PublicationCount++;
                    PublicationStarted.Set();
                    if (BlockPublication) ContinuePublication.Wait();
                    try
                    {
                        PublicationAction();
                    }
                    catch (Exception error)
                    {
                        PublicationFailure = error;
                    }
                },
                out string diagnostic);
            return committed
                ? RuntimePreparedAssetResult.Ready(64)
                : RuntimePreparedAssetResult.Waiting(diagnostic);
        }

        public void Release(RuntimeAssetResidencyKey key) => m_Prepared.Remove(key);

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

    private sealed class ActivationProbePayloadLoader : IWorldCellPayloadLoader
    {
        private readonly IWorldCellPayloadLoader m_Inner;
        private readonly ActivationProbeCodec m_Codec;

        public ActivationProbePayloadLoader(
            IWorldCellPayloadLoader inner,
            ActivationProbeCodec codec)
        {
            m_Inner = inner;
            m_Codec = codec;
        }

        public CellPayloadLoadResult Load(
            WorldCellDescriptor cell,
            long generation,
            SceneSourceSnapshot? previewSource,
            Action<WorldCellStreamingState> reportState,
            CancellationToken cancellationToken)
        {
            using CellPayloadLoadResult loaded = m_Inner.Load(
                cell,
                generation,
                previewSource,
                reportState,
                cancellationToken);
            SceneStagingEntity[] entities = loaded.Staging.Entities.ToArray();
            if (entities.Length == 0)
            {
                throw new InvalidDataException("Activation probe requires one staged entity.");
            }

            SceneStagedExtensionComponent[] extensions = entities[0].ExtensionComponents
                ?? Array.Empty<SceneStagedExtensionComponent>();
            entities[0] = entities[0] with
            {
                ExtensionComponents =
                [
                    .. extensions,
                    new SceneStagedExtensionComponent(m_Codec, new ActivationProbeComponent { Value = 7 })
                ]
            };
            SceneStagingData staging = loaded.Staging with
            {
                ComponentSchemas = [.. loaded.Staging.ComponentSchemas, m_Codec.Schema],
                Entities = entities
            };
            return new CellPayloadLoadResult(
                staging,
                loaded.SourceKind,
                loaded.PayloadBytes,
                checked(loaded.StagingBytes + sizeof(int)));
        }
    }

    private sealed class ActivationProbeCodec :
        ISceneComponentExtensionCodec,
        ISceneComponentExtensionActivationValidator
    {
        private static readonly Guid s_OwnershipId =
            Guid.Parse("83000000-0000-0000-0000-000000000201");

        public SceneComponentSchemaInfo Schema { get; } =
            new(0x50524F42, "ActivationProbe", 1, Required: true);
        public bool AllowActivation { get; set; }
        public bool BlockActivation { get; set; }
        public Action? ValidationAction { get; set; }
        public ManualResetEventSlim ValidationStarted { get; } = new(false);
        public ManualResetEventSlim ContinueValidation { get; } = new(false);
        public int ValidationCount { get; private set; }
        public int AddToEntityCount { get; private set; }
        public SceneComponentActivationContext? LastContext { get; private set; }

        public bool TryValidateActivation(
            in SceneComponentActivationContext context,
            object component,
            out string diagnostic)
        {
            RequireComponent(component);
            ValidationCount++;
            LastContext = context;
            ValidationStarted.Set();
            if (BlockActivation) ContinueValidation.Wait();
            ValidationAction?.Invoke();
            diagnostic = AllowActivation ? string.Empty : "Injected activation rejection";
            return AllowActivation;
        }

        public bool TryReadSource(
            in SceneComponentReadContext context,
            YamlDotNet.RepresentationModel.YamlMappingNode source,
            out object component,
            out string diagnostic)
        {
            component = new ActivationProbeComponent { Value = 7 };
            diagnostic = string.Empty;
            return true;
        }

        public byte[] WriteCooked(object component) => throw new NotSupportedException();

        public bool TryReadCooked(
            in SceneComponentReadContext context,
            ReadOnlySpan<byte> payload,
            out object component,
            out string diagnostic) => throw new NotSupportedException();

        public IReadOnlyList<CookedSceneDependency> GetDependencies(object component)
        {
            RequireComponent(component);
            return Array.Empty<CookedSceneDependency>();
        }

        public Guid GetExclusiveOwnershipId(object component)
        {
            RequireComponent(component);
            return s_OwnershipId;
        }

        public void AddToEntity(EntityManager entityManager, Entity entity, object component)
        {
            ActivationProbeComponent value = RequireComponent(component);
            AddToEntityCount++;
            entityManager.AddComponent(entity, value);
        }

        private static ActivationProbeComponent RequireComponent(object component) =>
            component is ActivationProbeComponent value
                ? value
                : throw new InvalidOperationException("Activation probe component type is invalid.");
    }

    private struct ActivationProbeComponent : IComponent
    {
        public int Value;
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
