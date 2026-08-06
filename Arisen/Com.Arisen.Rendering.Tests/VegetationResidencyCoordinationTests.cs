using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Threading;
using ArisenEngine.Vegetation;
using ArisenEngine.Vegetation.Assets;
using ArisenEngine.Vegetation.GenericRenderPipeline;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

[Collection(SceneComponentExtensionRegistryCollection.Name)]
public sealed class VegetationResidencyCoordinationTests
    : IDisposable
{
    private static readonly Guid s_WorldGuid =
        Guid.Parse("b9100000-0000-0000-0000-000000000001");
    private readonly VegetationClusterSceneComponentCodec m_SceneCodec = new();

    public VegetationResidencyCoordinationTests()
    {
        SceneComponentExtensionRegistry.Shared.Register(m_SceneCodec);
    }

    public void Dispose()
    {
        SceneComponentExtensionRegistry.Shared.Unregister(m_SceneCodec);
    }

    [Fact]
    public void CookedVegetationCellStreamsThroughWaitingActivationQueryAndUnload()
    {
        using var fixture = new VegetationResidencyFixture(includeWorldStreamingFixture: true);
        var world = new EntityManager();
        var sceneService = new RuntimeSceneService(fixture.Database, world);
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        var runtimeData = new VegetationRuntimeDataStore();
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            runtimeData,
            residency);
        var waitingProvider = new WaitOncePreparedProvider(vegetationProvider);
        residency.RegisterPreparedProvider(waitingProvider);
        residency.RegisterPreparedProvider(new ImmediateRenderingPreparedProvider());
        var origin = new WorldOriginService();
        var streaming = new RuntimeWorldStreamingService(
            fixture.Database,
            sceneService,
            fixture.Scheduler,
            WorldStreamingBudgets.Default with
            {
                MaxActivationsPerFrame = 1,
                MaxActivationMilliseconds = 100
            },
            residency,
            origin);
        var query = new VegetationQueryService(runtimeData, () => world);
        var transitions = new List<WorldCellStreamingState>();
        streaming.CellStateChanged += snapshot =>
        {
            if (snapshot.CellId == fixture.CellId)
            {
                transitions.Add(snapshot.State);
            }
        };

        try
        {
            RuntimeWorldLoadResult loaded = streaming.LoadWorld(fixture.WorldAsset);
            Assert.True(loaded.Success, loaded.Diagnostic);
            Assert.Equal(1, world.EntityCount);
            Assert.True(streaming.PinCell(fixture.CellId));

            PumpUntil(
                streaming,
                () => streaming.GetCells().Single().State == WorldCellStreamingState.WaitingForResources);
            Assert.True(waitingProvider.Waited);
            Assert.Contains(WorldCellStreamingState.WaitingForResources, transitions);
            Assert.Equal(1, world.EntityCount);
            VegetationInstanceQueryResult[] waitingResults =
                new VegetationInstanceQueryResult[2];
            VegetationQueryStatus waitingQueryStatus = query.QueryNearby(
                new VegetationQueryRequest(new WorldPosition(0.0, 0.0, 0.0), 16.0, 2),
                waitingResults,
                out int waitingResultCount);
            Assert.Equal(VegetationQueryStatus.Unavailable, waitingQueryStatus);
            Assert.Equal(0, waitingResultCount);

            vegetationProvider.WaitForWorkerIdle();
            PumpUntil(
                streaming,
                () => streaming.GetCells().Single().State == WorldCellStreamingState.Active,
                vegetationProvider);

            Assert.Contains(WorldCellStreamingState.ReadyToActivate, transitions);
            Assert.Contains(WorldCellStreamingState.Active, transitions);
            Assert.Equal(2, world.EntityCount);
            Assert.True(world.HasPool<VegetationClusterComponent>());
            Assert.Equal(1, world.GetPool<VegetationClusterComponent>().Count);

            VegetationClusterDataSnapshot snapshot = runtimeData.GetSnapshot();
            Assert.Equal(1, snapshot.ClusterCount);
            Assert.Equal(1, snapshot.PageCount);
            VegetationResidentClusterData cluster = snapshot.Clusters[0];
            Assert.Equal(fixture.ClusterGuid, cluster.Guid);
            Assert.Equal(fixture.PageGuid, cluster.Pages[0].Guid);

            VegetationInstanceQueryResult[] results = new VegetationInstanceQueryResult[2];
            VegetationQueryStatus queryStatus = query.QueryNearby(
                new VegetationQueryRequest(new WorldPosition(0.0, 0.0, 0.0), 16.0, 2),
                results,
                out int resultCount);
            Assert.Equal(VegetationQueryStatus.Available, queryStatus);
            Assert.Equal(2, resultCount);
            Assert.All(
                results,
                result =>
                {
                    Assert.Equal(fixture.ClusterGuid, result.ClusterGuid);
                    Assert.Equal(cluster.Generation, result.ClusterGeneration);
                });

            IReadOnlyList<RuntimeAssetResidencySnapshot> resident = residency.GetResources();
            Assert.Equal(9, resident.Count);
            Assert.Contains(resident, resource =>
                resource.Key.Guid == fixture.AlternateSpeciesGuid &&
                resource.Key.AssetType == VegetationAssetTypes.Species);
            Assert.Contains(resident, resource =>
                resource.Key.Guid == fixture.AlternateMeshGuid &&
                resource.Key.AssetType == "Mesh");
            Assert.Contains(resident, resource =>
                resource.Key.Guid == fixture.AlternateMaterialGuid &&
                resource.Key.AssetType == "Material");
            Assert.All(resident, resource =>
            {
                Assert.Equal(1, resource.OwnerCount);
                Assert.Contains(
                    resource.Owners,
                    owner => owner.Kind == RuntimeAssetResidencyOwnerKind.WorldCell &&
                             owner.CellId == fixture.CellId);
            });

            Assert.True(streaming.UnpinCell(fixture.CellId));
            PumpUntil(
                streaming,
                () => streaming.GetCells().Single().State == WorldCellStreamingState.Unloaded);

            Assert.Equal(1, world.EntityCount);
            Assert.Equal(0, world.GetPool<VegetationClusterComponent>().Count);
            residency.ProcessAtFrameBoundary();
            Assert.True(runtimeData.GetSnapshot().IsEmpty);
            Assert.Equal(VegetationQueryStatus.Unavailable, query.QueryNearby(
                new VegetationQueryRequest(new WorldPosition(0.0, 0.0, 0.0), 16.0, 2),
                results,
                out int unloadedResults));
            Assert.Equal(0, unloadedResults);
            Assert.Empty(residency.GetResources());
            Assert.Empty(fixture.Database.GetLoadedCookedAssetDiagnostics());
        }
        finally
        {
            vegetationProvider.WaitForWorkerIdle();
            streaming.Shutdown(unloadActiveCells: true);
            sceneService.ClearForShutdown();
        }
    }

    private static void PumpUntil(
        RuntimeWorldStreamingService streaming,
        Func<bool> condition,
        VegetationPreparedAssetProvider? provider = null)
    {
        for (int attempt = 0; attempt < 5_000; attempt++)
        {
            if (condition())
            {
                return;
            }

            streaming.ProcessAtFrameBoundary();
            provider?.WaitForWorkerIdle();
            Thread.Yield();
        }

        throw new Xunit.Sdk.XunitException(
            "Vegetation world-streaming condition was not reached. " +
            string.Join(
                ", ",
                streaming.GetCells().Select(cell =>
                    $"{cell.CellId}:{cell.State}:{cell.Diagnostic}")));
    }

    [Fact]
    public void ResidentPageAcceptsCanonicalCrossPackageSpeciesOrder()
    {
        var store = new VegetationRuntimeDataStore();
        Guid pageGuid = Guid.Parse("b9100000-0000-0000-0000-000000000010");
        var page = new CookedVegetationInstancePage(
            pageGuid,
            Guid.Parse("b9100000-0000-0000-0000-000000000011"),
            "com.arisen.test",
            VegetationInstancePageAssetCooker.CurrentGeneratedSchemaVersion,
            new WorldPosition(0.0, 0.0, 0.0),
            new WorldBounds(
                new WorldPosition(-1.0, -1.0, -1.0),
                new WorldPosition(1.0, 1.0, 1.0)),
            Array.AsReadOnly(
            [
                new CookedVegetationSpeciesReference(
                    Guid.Parse("f0000000-0000-0000-0000-000000000001"),
                    "a.package"),
                new CookedVegetationSpeciesReference(
                    Guid.Parse("10000000-0000-0000-0000-000000000001"),
                    "z.package")
            ]),
            Array.AsReadOnly(
            [
                new CookedVegetationInstance(
                    1,
                    0,
                    Vector3.Zero,
                    Quaternion.Identity,
                    1.0f,
                    0.25f),
                new CookedVegetationInstance(
                    2,
                    1,
                    new Vector3(0.5f, 0.0f, 0.0f),
                    Quaternion.Identity,
                    1.0f,
                    0.25f)
            ]));
        var payloadIdentity = new VegetationInstancePagePayloadIdentity(
            pageGuid,
            512,
            Enumerable.Repeat((byte)0x5a, SHA256.HashSizeInBytes).ToArray());

        VegetationPreparedPageData prepared = store.PreparePage(page, payloadIdentity);
        VegetationResidentResourceHandle handle = store.PublishPage(prepared);

        Assert.True(handle.IsValid);
        Assert.Equal(1, store.GetMetrics().ResidentPageCount);
        Assert.Equal("a.package", prepared.Page.Species[0].PackageId);
        Assert.Equal("z.package", prepared.Page.Species[1].PackageId);
        Assert.True(store.Remove(handle));
        Assert.Equal(default, store.GetMetrics());
    }

    [Fact]
    public void SharedSpeciesSurvivesIndependentClusterPageEviction()
    {
        using var fixture = new VegetationResidencyFixture();
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        var renderingProvider = new ImmediateRenderingPreparedProvider();
        residency.RegisterPreparedProvider(vegetationProvider);
        residency.RegisterPreparedProvider(renderingProvider);

        RuntimeAssetResidencyOwnerId firstOwner = CellOwner(1, generation: 1);
        RuntimeAssetResidencyOwnerId secondOwner = CellOwner(2, generation: 1);
        RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            firstOwner,
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);
        RuntimeAssetResidencyLease second = residency.AcquireSceneDependencies(
            secondOwner,
            fixture.GetClosure(clusterIndex: 1),
            pinned: false);

        ProcessUntilTerminal(residency, vegetationProvider, first, second);

        Assert.Equal(RuntimePreparedAssetState.Ready, first.State);
        Assert.Equal(RuntimePreparedAssetState.Ready, second.State);
        VegetationRuntimeDataMetrics complete = store.GetMetrics();
        Assert.Equal(2, complete.ResidentClusterCount);
        Assert.Equal(2, complete.ResidentPageCount);
        Assert.Equal(2, complete.CompleteClusterCount);
        RuntimeAssetResidencySnapshot species = residency.GetResources().Single(
            resource => resource.Key.Guid == VegetationResidencyFixture.SpeciesGuid);
        Assert.Equal(2, species.OwnerCount);
        Assert.Equal([firstOwner, secondOwner], species.Owners);

        first.Dispose();
        residency.ProcessAtFrameBoundary();

        VegetationRuntimeDataMetrics afterFirstUnload = store.GetMetrics();
        Assert.Equal(1, afterFirstUnload.ResidentClusterCount);
        Assert.Equal(1, afterFirstUnload.ResidentPageCount);
        Assert.Equal(1, afterFirstUnload.CompleteClusterCount);
        Assert.DoesNotContain(
            residency.GetResources(),
            resource => resource.Key.Guid == fixture.ClusterGuids[0] ||
                        resource.Key.Guid == fixture.PageGuids[0]);
        Assert.Contains(
            residency.GetResources(),
            resource => resource.Key.Guid == VegetationResidencyFixture.SpeciesGuid &&
                        resource.OwnerCount == 1);

        second.Dispose();
        residency.ProcessAtFrameBoundary();

        Assert.Empty(residency.GetResources());
        Assert.Equal(default, store.GetMetrics());
        Assert.True(store.GetSnapshot().IsEmpty);
    }

    [Fact]
    public void ProviderInvalidationRemovesPublicationsBeforeGenerationQualifiedRetry()
    {
        using var fixture = new VegetationResidencyFixture();
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        residency.RegisterPreparedProvider(vegetationProvider);
        residency.RegisterPreparedProvider(new ImmediateRenderingPreparedProvider());
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);
        ProcessUntilTerminal(residency, vegetationProvider, lease);
        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
        ulong firstGeneration = store.GetSnapshot().Clusters[0].Generation;

        Assert.True(residency.InvalidatePreparedProvider(
            vegetationProvider.ProviderId,
            "Vegetation CPU resources were invalidated for test."));

        Assert.Equal(RuntimePreparedAssetState.Waiting, lease.State);
        Assert.True(store.GetSnapshot().IsEmpty);
        Assert.Equal(default, store.GetMetrics());

        ProcessUntilTerminal(residency, vegetationProvider, lease);

        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
        VegetationClusterDataSnapshot replacement = store.GetSnapshot();
        Assert.Equal(1, replacement.ClusterCount);
        Assert.True(replacement.Clusters[0].Generation > firstGeneration);
        Assert.Equal(4, vegetationProvider.GetMetrics().PreparedResourceCount);
    }

    [Fact]
    public void CancellationAndShutdownDrainCookedAndPublishedOwnership()
    {
        using var fixture = new VegetationResidencyFixture();
        var store = new VegetationRuntimeDataStore();
        var residency = fixture.CreateResidency(maxInactiveResources: 4);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        residency.RegisterPreparedProvider(vegetationProvider);
        residency.RegisterPreparedProvider(new ImmediateRenderingPreparedProvider());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            residency.AcquireSceneDependencies(
                CellOwner(1, generation: 1),
                fixture.GetClosure(clusterIndex: 0),
                pinned: false,
                cancellation.Token));
        Assert.Empty(residency.GetResources());
        Assert.True(store.GetSnapshot().IsEmpty);

        RuntimeAssetResidencyLease active = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 2),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);
        ProcessUntilTerminal(residency, vegetationProvider, active);
        Assert.Equal(RuntimePreparedAssetState.Ready, active.State);
        Assert.False(store.GetSnapshot().IsEmpty);

        residency.Dispose();
        active.Dispose();

        Assert.True(store.GetSnapshot().IsEmpty);
        Assert.Equal(0, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Empty(fixture.Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public void PreparationUsesTheResidencyHeldClosureAcrossValidCatalogReplacement()
    {
        using var fixture = new VegetationResidencyFixture();
        using var gatedScheduler = new GatedBackgroundTaskScheduler(fixture.Scheduler);
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            gatedScheduler,
            store,
            residency);
        var clusterProvider = new AssetTypeFilteringPreparedProvider(
            vegetationProvider,
            VegetationAssetTypes.Cluster);
        residency.RegisterPreparedProvider(clusterProvider);
        RuntimeAssetResidencyKey key = fixture.ClusterKey(clusterIndex: 0);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);

        residency.ProcessAtFrameBoundary();
        Assert.True(
            gatedScheduler.WorkerEntered.Wait(TimeSpan.FromSeconds(10)),
            "Vegetation cluster preparation did not enter the gated worker.");
        CookedSceneDependency[] replacementClosure =
            fixture.ReplaceWithValidChangedClosure(clusterIndex: 0);
        CookedSceneDependency replacementPage = replacementClosure.Single(
            dependency => dependency.AssetType == VegetationAssetTypes.InstancePage);
        Assert.NotEqual(fixture.PageGuid, replacementPage.Guid);
        Assert.True(fixture.Database.TryGetCookedArtifact(
            fixture.PageGuid,
            VegetationInstancePageAssetCooker.RuntimeVariant,
            out CookedAssetRecord originalPageArtifact));
        Assert.True(fixture.Database.TryGetCookedArtifact(
            replacementPage.Guid,
            VegetationInstancePageAssetCooker.RuntimeVariant,
            out CookedAssetRecord replacementPageArtifact));
        Assert.False(SHA256.HashData(File.ReadAllBytes(originalPageArtifact.Path)).SequenceEqual(
            SHA256.HashData(File.ReadAllBytes(replacementPageArtifact.Path))));
        Assert.True(VegetationClusterAssetCooker.TryLoadCooked(
            fixture.Database,
            new AssetRef<VegetationClusterSourceAsset>(
                fixture.ClusterGuid,
                VegetationAssetTypes.Cluster,
                VegetationResidencyFixture.PackageId),
            out CookedVegetationCluster replacementCluster,
            out string replacementDiagnostic),
            replacementDiagnostic);
        Assert.Equal(replacementPage.Guid, Assert.Single(replacementCluster.Pages).Guid);

        try
        {
            gatedScheduler.AllowWorker.Set();
            vegetationProvider.WaitForWorkerIdle();
        }
        finally
        {
            gatedScheduler.AllowWorker.Set();
        }

        RuntimeAssetResidencySnapshot heldResult = ProcessResourceUntilTerminal(
            residency,
            vegetationProvider,
            key);
        Assert.Equal(RuntimePreparedAssetState.Ready, heldResult.PreparedState);
        Assert.Equal(1, store.GetMetrics().ResidentClusterCount);
        Assert.Equal(1, vegetationProvider.GetMetrics().PreparedResourceCount);

        lease.Dispose();
        residency.ProcessAtFrameBoundary();
        using RuntimeAssetResidencyLease replacement = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 2),
            replacementClosure,
            pinned: false);

        RuntimeAssetResidencySnapshot replacementResult = ProcessResourceUntilTerminal(
            residency,
            vegetationProvider,
            key);

        Assert.Equal(RuntimePreparedAssetState.Ready, replacementResult.PreparedState);
        Assert.Equal(1, store.GetMetrics().ResidentClusterCount);
    }

    [Fact]
    public void PagePreparationRejectsOwnerClosureWithoutParentCluster()
    {
        using var fixture = new VegetationResidencyFixture();
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        RuntimeAssetResidencyKey pageKey = fixture.PageKey(pageIndex: 0);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetPageClosure(pageIndex: 0, parentClusterIndex: null),
            pinned: false);

        RuntimePreparedAssetResult result = CompleteDirectPreparation(
            vegetationProvider,
            pageKey);

        Assert.Equal(RuntimePreparedAssetState.Failed, result.State);
        Assert.Contains(fixture.ClusterGuid.ToString("D"), result.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(default, store.GetMetrics());
    }

    [Fact]
    public void PagePreparationRejectsParentThatDoesNotReferenceThePage()
    {
        using var fixture = new VegetationResidencyFixture();
        fixture.ReplacePageParent(pageIndex: 0, parentClusterIndex: 1);
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        RuntimeAssetResidencyKey pageKey = fixture.PageKey(pageIndex: 0);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetPageClosure(pageIndex: 0, parentClusterIndex: 1),
            pinned: false);

        RuntimePreparedAssetResult result = CompleteDirectPreparation(
            vegetationProvider,
            pageKey);

        Assert.Equal(RuntimePreparedAssetState.Failed, result.State);
        Assert.Contains(fixture.PageGuid.ToString("D"), result.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("parent", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(default, store.GetMetrics());
    }

    [Fact]
    public void PagePreparationRejectsUnsupportedGeneratedSchema()
    {
        using var fixture = new VegetationResidencyFixture();
        fixture.ReplacePageWithUnsupportedSchema(pageIndex: 0);
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetPageClosure(pageIndex: 0, parentClusterIndex: 0),
            pinned: false);

        RuntimePreparedAssetResult result = CompleteDirectPreparation(
            vegetationProvider,
            fixture.PageKey(pageIndex: 0));

        Assert.Equal(RuntimePreparedAssetState.Failed, result.State);
        Assert.Contains("version", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(default, store.GetMetrics());
    }

    [Theory]
    [InlineData(PageClosureValidationCase.Size)]
    [InlineData(PageClosureValidationCase.SpeciesMembership)]
    public void DirectPageClosureValidationRejectsCrossRecordMismatch(
        PageClosureValidationCase validationCase)
    {
        using var fixture = new VegetationResidencyFixture();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetPageValidationClosure(
                includeAlternateSpecies:
                    validationCase == PageClosureValidationCase.SpeciesMembership),
            pinned: false);
        RuntimeAssetResidencyKey pageKey = fixture.PageKey(pageIndex: 0);
        Assert.True(residency.TryGetPreparationClaim(
            pageKey,
            out RuntimeAssetPreparationClaim pageClaim));
        Assert.True(VegetationResidentAssetLoader.TryLoadPage(
            fixture.Database,
            pageClaim,
            out CookedVegetationInstancePage page,
            out VegetationInstancePagePayloadIdentity payloadIdentity,
            out string loadDiagnostic),
            loadDiagnostic);
        RuntimeAssetResidencyKey[] requiredKeys =
            VegetationResidentAssetLoader.GetRequiredResidencyKeys(page);

        switch (validationCase)
        {
            case PageClosureValidationCase.Size:
                payloadIdentity = new VegetationInstancePagePayloadIdentity(
                    page.Guid,
                    checked(payloadIdentity.SizeInBytes + 1),
                    payloadIdentity.ContentHash.ToArray());
                break;
            case PageClosureValidationCase.SpeciesMembership:
                page = page with
                {
                    Species = Array.AsReadOnly(
                        [new CookedVegetationSpeciesReference(
                            fixture.AlternateSpeciesGuid,
                            VegetationResidencyFixture.PackageId)])
                };
                requiredKeys = VegetationResidentAssetLoader.GetRequiredResidencyKeys(page);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(validationCase));
        }

        RuntimeAssetPreparationClaim[] claims = CapturePreparationClaims(
            residency,
            requiredKeys);

        Assert.False(VegetationResidentAssetLoader.TryValidatePageClosure(
            fixture.Database,
            page,
            payloadIdentity,
            claims,
            out string diagnostic));
        Assert.Contains(
            validationCase switch
            {
                PageClosureValidationCase.Size => "byte size",
                PageClosureValidationCase.SpeciesMembership => "absent",
                _ => throw new ArgumentOutOfRangeException(nameof(validationCase))
            },
            diagnostic,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Mesh", false)]
    [InlineData("Mesh", true)]
    [InlineData("Material", false)]
    [InlineData("Material", true)]
    public void SpeciesPreparationRejectsMissingOrWrongRenderingDependency(
        string dependencyType,
        bool substituteWrongDependency)
    {
        using var fixture = new VegetationResidencyFixture();
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        RuntimeAssetResidencyKey speciesKey = fixture.SpeciesKey;
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetSpeciesClosure(dependencyType, substituteWrongDependency),
            pinned: false);

        RuntimePreparedAssetResult result = CompleteDirectPreparation(
            vegetationProvider,
            speciesKey);

        Assert.Equal(RuntimePreparedAssetState.Failed, result.State);
        Guid expectedGuid = dependencyType == "Mesh"
            ? fixture.MeshGuid
            : fixture.MaterialGuid;
        Assert.Contains(expectedGuid.ToString("D"), result.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(0, vegetationProvider.GetMetrics().PreparedResourceCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BiomePreparationRejectsMissingOrWrongSpeciesClosure(
        bool substituteWrongSpecies)
    {
        using var fixture = new VegetationResidencyFixture();
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetBiomeClosure(substituteWrongSpecies),
            pinned: false);

        RuntimePreparedAssetResult result = CompleteDirectPreparation(
            vegetationProvider,
            fixture.BiomeKey);

        Assert.Equal(RuntimePreparedAssetState.Failed, result.State);
        Assert.Contains(
            VegetationResidencyFixture.SpeciesGuid.ToString("D"),
            result.Diagnostic,
            StringComparison.Ordinal);
        Assert.Equal(0, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Equal(default, store.GetMetrics());
    }

    [Theory]
    [InlineData(ClusterClosureMismatch.PageHash)]
    [InlineData(ClusterClosureMismatch.InstanceCount)]
    [InlineData(ClusterClosureMismatch.Bounds)]
    [InlineData(ClusterClosureMismatch.SpeciesUnion)]
    public void ClusterPreparationRejectsCrossRecordClosureMismatch(
        ClusterClosureMismatch mismatch)
    {
        using var fixture = new VegetationResidencyFixture();
        CookedSceneDependency[] closure = fixture.ReplaceClusterRootWithMismatch(mismatch);
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        RuntimeAssetResidencyKey clusterKey = fixture.ClusterKey(clusterIndex: 0);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            closure,
            pinned: false);

        RuntimePreparedAssetResult result = CompleteDirectPreparation(
            vegetationProvider,
            clusterKey);

        Assert.Equal(RuntimePreparedAssetState.Failed, result.State);
        Assert.Contains(
            mismatch switch
            {
                ClusterClosureMismatch.PageHash => "hash",
                ClusterClosureMismatch.InstanceCount => "count",
                ClusterClosureMismatch.Bounds => "bounds",
                ClusterClosureMismatch.SpeciesUnion => "species",
                _ => throw new ArgumentOutOfRangeException(nameof(mismatch))
            },
            result.Diagnostic,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(default, store.GetMetrics());
    }

    [Fact]
    public void ClusterPreparationRejectsSpeciesOutsideItsHeldBiome()
    {
        using var fixture = new VegetationResidencyFixture();
        CookedSceneDependency[] closure = fixture.ReplaceClusterWithBiomeMembershipMismatch();
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        RuntimeAssetResidencyKey clusterKey = fixture.ClusterKey(clusterIndex: 0);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            closure,
            pinned: false);

        RuntimePreparedAssetResult result = CompleteDirectPreparation(
            vegetationProvider,
            clusterKey);

        Assert.Equal(RuntimePreparedAssetState.Failed, result.State);
        Assert.Contains("biome", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(VegetationResidencyFixture.SpeciesGuid.ToString("D"),
            result.Diagnostic,
            StringComparison.Ordinal);
        Assert.Equal(default, store.GetMetrics());
    }

    [Fact]
    public void ClusterPreparationRejectsDuplicateStableKeysAcrossHeldPages()
    {
        using var fixture = new VegetationResidencyFixture();
        CookedSceneDependency[] closure = fixture.ReplaceClusterWithDuplicateStableKeys();
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        RuntimeAssetResidencyKey clusterKey = fixture.ClusterKey(clusterIndex: 0);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            closure,
            pinned: false);

        RuntimePreparedAssetResult result = CompleteDirectPreparation(
            vegetationProvider,
            clusterKey);

        Assert.Equal(RuntimePreparedAssetState.Failed, result.State);
        Assert.Contains("stable", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(default, store.GetMetrics());
    }

    [Fact]
    public void SharedClusterRejectsOwnerWithDifferentPageClosure()
    {
        using var fixture = new VegetationResidencyFixture();
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        residency.RegisterPreparedProvider(new AssetTypeFilteringPreparedProvider(
            vegetationProvider,
            VegetationAssetTypes.Cluster));
        RuntimeAssetResidencyKey clusterKey = fixture.ClusterKey(clusterIndex: 0);
        using RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);
        RuntimeAssetResidencySnapshot prepared = ProcessResourceUntilTerminal(
            residency,
            vegetationProvider,
            clusterKey);
        Assert.Equal(RuntimePreparedAssetState.Ready, prepared.PreparedState);

        InvalidDataException failure = Assert.Throws<InvalidDataException>(() =>
            residency.AcquireSceneDependencies(
                CellOwner(2, generation: 1),
                fixture.GetSharedClusterMismatchClosure(),
                pinned: false));

        Assert.Contains(fixture.PageGuid.ToString("D"), failure.Message, StringComparison.Ordinal);
        RuntimeAssetResidencySnapshot cluster = residency.GetResources().Single(
            resource => resource.Key == clusterKey);
        Assert.Equal(1, cluster.OwnerCount);
        Assert.Equal([first.Owner], cluster.Owners);
    }

    [Fact]
    public void OwnerPlanChangeMakesInFlightClaimWaitAndRetry()
    {
        using var fixture = new VegetationResidencyFixture();
        using var gatedScheduler = new GatedBackgroundTaskScheduler(fixture.Scheduler);
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            gatedScheduler,
            store,
            residency);
        residency.RegisterPreparedProvider(new AssetTypeFilteringPreparedProvider(
            vegetationProvider,
            VegetationAssetTypes.Cluster));
        RuntimeAssetResidencyKey clusterKey = fixture.ClusterKey(clusterIndex: 0);
        using RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);

        residency.ProcessAtFrameBoundary();
        Assert.True(
            gatedScheduler.WorkerEntered.Wait(TimeSpan.FromSeconds(10)),
            "Vegetation cluster preparation did not enter the gated worker.");
        using RuntimeAssetResidencyLease second = residency.AcquireSceneDependencies(
            CellOwner(2, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);

        gatedScheduler.AllowWorker.Set();
        vegetationProvider.WaitForWorkerIdle();
        residency.ProcessAtFrameBoundary();
        RuntimeAssetResidencySnapshot stale = residency.GetResources().Single(
            resource => resource.Key == clusterKey);

        Assert.Equal(RuntimePreparedAssetState.Waiting, stale.PreparedState);
        Assert.DoesNotContain("failed", stale.Diagnostic, StringComparison.OrdinalIgnoreCase);
        vegetationProvider.WaitForWorkerIdle();
        RuntimeAssetResidencySnapshot retried = ProcessResourceUntilTerminal(
            residency,
            vegetationProvider,
            clusterKey);
        Assert.Equal(RuntimePreparedAssetState.Ready, retried.PreparedState);
        Assert.Equal(1, store.GetMetrics().ResidentClusterCount);
    }

    [Fact]
    public void ReleaseRetainsPublicationUntilThrowingAndRejectedStoreRemovalCanRetry()
    {
        using var fixture = new VegetationResidencyFixture();
        using var store = new FaultInjectingVegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        residency.RegisterPreparedProvider(vegetationProvider);
        residency.RegisterPreparedProvider(new ImmediateRenderingPreparedProvider());
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);
        ProcessUntilTerminal(residency, vegetationProvider, lease);
        RuntimeAssetResidencyKey clusterKey = fixture.ClusterKey(clusterIndex: 0);
        store.SetRemoveOutcomes(
            fixture.ClusterGuid,
            RemoveOutcome.Throw,
            RemoveOutcome.ReturnFalse);

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
            () => vegetationProvider.Release(clusterKey));

        Assert.Contains("injected", thrown.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, store.GetRemoveAttemptCount(fixture.ClusterGuid));
        Assert.Equal(4, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Equal(1, store.GetMetrics().CompleteClusterCount);
        VegetationClusterDataSnapshot afterThrow = store.GetSnapshot();
        Assert.Equal(1, afterThrow.ClusterCount);
        Assert.Equal(fixture.ClusterGuid, afterThrow.Clusters[0].Guid);

        InvalidOperationException rejected = Assert.Throws<InvalidOperationException>(
            () => vegetationProvider.Release(clusterKey));

        Assert.Contains("not owned", rejected.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, store.GetRemoveAttemptCount(fixture.ClusterGuid));
        Assert.Equal(4, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Equal(1, store.GetMetrics().CompleteClusterCount);
        VegetationClusterDataSnapshot afterRejection = store.GetSnapshot();
        Assert.Equal(1, afterRejection.ClusterCount);
        Assert.Equal(fixture.ClusterGuid, afterRejection.Clusters[0].Guid);

        vegetationProvider.Release(clusterKey);

        Assert.Equal(3, store.GetRemoveAttemptCount(fixture.ClusterGuid));
        Assert.Equal(3, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Equal(0, store.GetMetrics().ResidentClusterCount);
        Assert.Equal(1, store.GetMetrics().ResidentPageCount);
        Assert.True(store.GetSnapshot().IsEmpty);
    }

    [Theory]
    [InlineData((int)VegetationPreparedPublicationStage.PreparedEntryAdded)]
    [InlineData((int)VegetationPreparedPublicationStage.ClusterMappingAdded)]
    [InlineData((int)VegetationPreparedPublicationStage.GpuBytesCharged)]
    [InlineData((int)VegetationPreparedPublicationStage.PreparedCountUpdated)]
    public void PublicationFailureRollsBackOnlySuccessfullyPublishedLegs(
        int injectedStageValue)
    {
        var injectedStage = (VegetationPreparedPublicationStage)injectedStageValue;
        using var fixture = new VegetationResidencyFixture();
        using var store = new FaultInjectingVegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        var gpuResources = new TrackingVegetationGpuResourceFactory();
        int injectionRemaining = 1;
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency,
            gpuResources,
            (key, stage) =>
            {
                if (key.AssetType == VegetationAssetTypes.Cluster &&
                    stage == injectedStage &&
                    Interlocked.Exchange(ref injectionRemaining, 0) != 0)
                {
                    throw new InvalidOperationException(
                        $"Injected vegetation publication failure after '{stage}'.");
                }
            });
        residency.RegisterPreparedProvider(vegetationProvider);
        residency.RegisterPreparedProvider(new ImmediateRenderingPreparedProvider());
        RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);

        ProcessUntilTerminal(residency, vegetationProvider, first);

        Assert.Equal(RuntimePreparedAssetState.Failed, first.State);
        Assert.Equal(3, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Equal(0, vegetationProvider.GetMetrics().EstimatedGpuBytes);
        Assert.Equal(1, gpuResources.CreatedCount);
        Assert.Equal(1, gpuResources.ReleaseCount);
        Assert.Equal(0, gpuResources.LiveResourceCount);
        Assert.Equal(0, store.GetMetrics().ResidentClusterCount);

        first.Dispose();
        residency.ProcessAtFrameBoundary();
        Assert.Equal(default, vegetationProvider.GetMetrics());

        using RuntimeAssetResidencyLease retry = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 2),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);
        ProcessUntilTerminal(residency, vegetationProvider, retry);

        Assert.Equal(RuntimePreparedAssetState.Ready, retry.State);
        Assert.Equal(4, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Equal(
            TrackingVegetationGpuResourceFactory.ResourceBytes,
            vegetationProvider.GetMetrics().EstimatedGpuBytes);
        Assert.Equal(2, gpuResources.CreatedCount);
        Assert.Equal(1, gpuResources.ReleaseCount);
        Assert.Equal(1, gpuResources.LiveResourceCount);
    }

    [Fact]
    public void PublicationRollbackFailureRetainsLiveResourceForTeardownRetry()
    {
        using var fixture = new VegetationResidencyFixture();
        using var store = new FaultInjectingVegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        var gpuResources = new TrackingVegetationGpuResourceFactory();
        int injectionRemaining = 1;
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency,
            gpuResources,
            (key, stage) =>
            {
                if (key.AssetType == VegetationAssetTypes.Cluster &&
                    stage == VegetationPreparedPublicationStage.GpuBytesCharged &&
                    Interlocked.Exchange(ref injectionRemaining, 0) != 0)
                {
                    throw new InvalidOperationException(
                        "Injected vegetation publication failure after GPU byte charge.");
                }
            });
        residency.RegisterPreparedProvider(vegetationProvider);
        residency.RegisterPreparedProvider(new ImmediateRenderingPreparedProvider());
        store.SetRemoveOutcomes(fixture.ClusterGuid, RemoveOutcome.Throw);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);

        ProcessUntilTerminal(residency, vegetationProvider, lease);

        Assert.Equal(RuntimePreparedAssetState.Failed, lease.State);
        Assert.Equal(4, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Equal(
            TrackingVegetationGpuResourceFactory.ResourceBytes,
            vegetationProvider.GetMetrics().EstimatedGpuBytes);
        Assert.Equal(1, store.GetMetrics().ResidentClusterCount);
        Assert.Equal(1, gpuResources.CreatedCount);
        Assert.Equal(0, gpuResources.ReleaseCount);
        Assert.Equal(1, gpuResources.LiveResourceCount);

        vegetationProvider.Release(fixture.ClusterKey(clusterIndex: 0));

        Assert.Equal(3, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Equal(0, vegetationProvider.GetMetrics().EstimatedGpuBytes);
        Assert.Equal(0, store.GetMetrics().ResidentClusterCount);
        Assert.Equal(1, gpuResources.ReleaseCount);
        Assert.Equal(0, gpuResources.LiveResourceCount);

        lease.Dispose();
        residency.ProcessAtFrameBoundary();
        Assert.Equal(default, vegetationProvider.GetMetrics());
    }

    [Fact]
    public void GpuReleaseFailureRetainsPublicationForDisposeRetry()
    {
        using var fixture = new VegetationResidencyFixture();
        using var store = new FaultInjectingVegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        var gpuResources = new TrackingVegetationGpuResourceFactory();
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency,
            gpuResources);
        residency.RegisterPreparedProvider(vegetationProvider);
        residency.RegisterPreparedProvider(new ImmediateRenderingPreparedProvider());
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);
        ProcessUntilTerminal(residency, vegetationProvider, lease);
        gpuResources.ReleaseFailuresRemaining = 1;

        AggregateException failure = Assert.Throws<AggregateException>(
            vegetationProvider.Dispose);

        Assert.Contains(
            failure.InnerExceptions,
            exception => exception.ToString().Contains(
                "injected",
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Equal(
            TrackingVegetationGpuResourceFactory.ResourceBytes,
            vegetationProvider.GetMetrics().EstimatedGpuBytes);
        Assert.Equal(0, store.GetMetrics().ResidentClusterCount);
        Assert.Equal(0, gpuResources.ReleaseCount);
        Assert.Equal(1, gpuResources.LiveResourceCount);

        vegetationProvider.Dispose();

        Assert.Equal(0, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Equal(0, vegetationProvider.GetMetrics().EstimatedGpuBytes);
        Assert.Equal(1, gpuResources.ReleaseCount);
        Assert.Equal(0, gpuResources.LiveResourceCount);

        lease.Dispose();
        residency.ProcessAtFrameBoundary();
        Assert.Equal(default, vegetationProvider.GetMetrics());
    }

    [Fact]
    public void StaleGpuDependenciesInvalidateProviderAndReleasePreparedCluster()
    {
        using var fixture = new VegetationResidencyFixture();
        using var store = new FaultInjectingVegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        var gpuResources = new TrackingVegetationGpuResourceFactory();
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency,
            gpuResources);
        residency.RegisterPreparedProvider(vegetationProvider);
        vegetationProvider.SetResidencyRegistrationOwned(true);
        residency.RegisterPreparedProvider(new ImmediateRenderingPreparedProvider());
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);
        ProcessUntilTerminal(residency, vegetationProvider, lease);

        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
        Assert.Equal(1, gpuResources.LiveResourceCount);
        Assert.Equal(4, vegetationProvider.GetMetrics().PreparedResourceCount);

        gpuResources.InvalidateDependencies();

        Assert.True(vegetationProvider.InvalidateStaleDependencies());
        Assert.Equal(0, gpuResources.LiveResourceCount);
        Assert.Equal(1, gpuResources.ReleaseCount);
        Assert.Equal(default, vegetationProvider.GetMetrics());
        Assert.True(store.GetSnapshot().IsEmpty);
        Assert.False(vegetationProvider.InvalidateStaleDependencies());
    }

    [Fact]
    public void DisposeRetriesOnlyPublicationsWhoseStoreRemovalFailed()
    {
        using var fixture = new VegetationResidencyFixture();
        using var store = new FaultInjectingVegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        residency.RegisterPreparedProvider(vegetationProvider);
        residency.RegisterPreparedProvider(new ImmediateRenderingPreparedProvider());
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);

        try
        {
            ProcessUntilTerminal(residency, vegetationProvider, lease);
            store.SetRemoveOutcomes(fixture.ClusterGuid, RemoveOutcome.ReturnFalse);

            AggregateException failure = Assert.Throws<AggregateException>(
                vegetationProvider.Dispose);

            Assert.Single(failure.InnerExceptions);
            Assert.Equal(1, store.GetRemoveAttemptCount(fixture.ClusterGuid));
            Assert.Equal(1, store.GetRemoveAttemptCount(fixture.PageGuid));
            Assert.Equal(1, vegetationProvider.GetMetrics().PreparedResourceCount);
            Assert.Equal(1, store.GetMetrics().ResidentClusterCount);
            Assert.Equal(0, store.GetMetrics().ResidentPageCount);

            vegetationProvider.Dispose();

            Assert.Equal(2, store.GetRemoveAttemptCount(fixture.ClusterGuid));
            Assert.Equal(1, store.GetRemoveAttemptCount(fixture.PageGuid));
            Assert.Equal(0, vegetationProvider.GetMetrics().PreparedResourceCount);
            Assert.Equal(default, store.GetMetrics());
            Assert.True(store.GetSnapshot().IsEmpty);
        }
        finally
        {
            vegetationProvider.Dispose();
        }
    }

    [Fact]
    public async Task OwnerReleaseWaitsForAtomicVegetationPublicationBeforeInvalidatingClaim()
    {
        using var fixture = new VegetationResidencyFixture();
        using var store = new FaultInjectingVegetationRuntimeDataStore();
        VegetationResidentResourceHandle pageHandle = store.PublishPageForTest(
            fixture.GetCookedPage(pageIndex: 0));
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        residency.RegisterPreparedProvider(new AssetTypeFilteringPreparedProvider(
            vegetationProvider,
            VegetationAssetTypes.Cluster));
        RuntimeAssetResidencyKey clusterKey = fixture.ClusterKey(clusterIndex: 0);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);
        residency.ProcessAtFrameBoundary();
        vegetationProvider.WaitForWorkerIdle();
        store.BlockNextClusterPublication();
        Task publication = Task.Run(residency.ProcessAtFrameBoundary);

        try
        {
            Assert.True(
                store.ClusterPublicationVisible.Wait(TimeSpan.FromSeconds(10)),
                "Vegetation cluster publication did not reach the visible snapshot gate.");
            Assert.True(residency.TryGetPreparationClaim(clusterKey, out _));
            VegetationClusterDataSnapshot visible = store.GetSnapshot();
            Assert.Equal(1, visible.ClusterCount);
            Assert.Equal(fixture.ClusterGuid, visible.Clusters[0].Guid);
            Assert.Equal(1, residency.GetResources().Single(
                resource => resource.Key == clusterKey).OwnerCount);

            using var releaseStarted = new ManualResetEventSlim(false);
            Task release = Task.Run(() =>
            {
                releaseStarted.Set();
                lease.Dispose();
            });
            Assert.True(releaseStarted.Wait(TimeSpan.FromSeconds(10)));
            Assert.False(release.IsCompleted);
            Assert.True(residency.TryGetPreparationClaim(clusterKey, out _));
            VegetationClusterDataSnapshot releaseBlocked = store.GetSnapshot();
            Assert.Equal(1, releaseBlocked.ClusterCount);
            Assert.Equal(fixture.ClusterGuid, releaseBlocked.Clusters[0].Guid);

            store.AllowClusterPublicationReturn.Set();
            await Task.WhenAll(publication, release).WaitAsync(TimeSpan.FromSeconds(10));
            residency.ProcessAtFrameBoundary();

            Assert.False(residency.TryGetPreparationClaim(clusterKey, out _));
            Assert.Equal(0, vegetationProvider.GetMetrics().PreparedResourceCount);
            Assert.True(store.GetSnapshot().IsEmpty);
        }
        finally
        {
            store.AllowClusterPublicationReturn.Set();
            lease.Dispose();
            Assert.True(store.Remove(pageHandle));
        }
    }

    [Fact]
    public async Task DependencyOnlyPublicationRollsBackAndRetriesAfterDependencyInvalidation()
    {
        using var fixture = new VegetationResidencyFixture();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var gatedResidency = new GatedClaimResidencyService(residency);
        var store = new VegetationRuntimeDataStore();
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            gatedResidency);
        var renderingProvider = new ImmediateRenderingPreparedProvider();
        residency.RegisterPreparedProvider(vegetationProvider);
        residency.RegisterPreparedProvider(renderingProvider);
        RuntimeAssetResidencyKey speciesKey = new(
            VegetationResidencyFixture.SpeciesGuid,
            VegetationResidencyFixture.PackageId,
            VegetationAssetTypes.Species,
            VegetationSpeciesAssetCooker.RuntimeVariant);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetSpeciesClosure(
                dependencyType: "None",
                substituteWrongDependency: false),
            pinned: false);
        Task publication = Task.CompletedTask;
        Task<bool>? invalidation = null;

        try
        {
            residency.ProcessAtFrameBoundary();
            vegetationProvider.WaitForWorkerIdle();
            gatedResidency.GateNextPublication();
            publication = Task.Run(residency.ProcessAtFrameBoundary);

            Assert.True(
                gatedResidency.PublicationVisible.Wait(TimeSpan.FromSeconds(10)),
                "Vegetation species publication did not reach the post-publication gate.");
            Assert.Equal(1, gatedResidency.PublicationAttemptCount);
            Assert.Equal(1, vegetationProvider.GetMetrics().PreparedResourceCount);
            Assert.Equal(default, store.GetMetrics());

            invalidation = Task.Run(() => residency.InvalidatePreparedProvider(
                renderingProvider.ProviderId,
                "Rendering dependencies were invalidated during vegetation publication."));
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        RuntimeAssetResidencySnapshot[] dependencies = residency.GetResources()
                            .Where(resource => resource.Key.AssetType is "Mesh" or "Material")
                            .ToArray();
                        return dependencies.Length == 2 && dependencies.All(resource =>
                            resource.PreparedState == RuntimePreparedAssetState.Waiting);
                    },
                    TimeSpan.FromSeconds(10)),
                "Rendering dependency invalidation did not become observable.");

            gatedResidency.AllowPublicationReturn.Set();
            Assert.True(await invalidation.WaitAsync(TimeSpan.FromSeconds(10)));
            await publication.WaitAsync(TimeSpan.FromSeconds(10));

            RuntimeAssetResidencySnapshot species = residency.GetResources().Single(
                resource => resource.Key == speciesKey);
            Assert.Equal(RuntimePreparedAssetState.Waiting, species.PreparedState);
            Assert.Equal(0, vegetationProvider.GetMetrics().PreparedResourceCount);
            Assert.Equal(default, store.GetMetrics());

            ProcessUntilTerminal(residency, vegetationProvider, lease);

            Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
            Assert.Equal(2, gatedResidency.PublicationAttemptCount);
            Assert.Equal(1, vegetationProvider.GetMetrics().PreparedResourceCount);

            lease.Dispose();
            residency.ProcessAtFrameBoundary();

            Assert.Equal(0, vegetationProvider.GetMetrics().PreparedResourceCount);
            Assert.Empty(residency.GetResources());
        }
        finally
        {
            gatedResidency.AllowPublicationReturn.Set();
            await publication.WaitAsync(TimeSpan.FromSeconds(10));
            if (invalidation != null)
            {
                await invalidation.WaitAsync(TimeSpan.FromSeconds(10));
            }

            lease.Dispose();
        }
    }

    [Fact]
    public async Task OrdinaryWorkerFailureBecomesWaitingWhenClaimChangesBeforeConsumption()
    {
        using var fixture = new VegetationResidencyFixture();
        fixture.ReplacePageWithUnsupportedSchema(pageIndex: 0);
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var gatedResidency = new GatedClaimResidencyService(residency);
        var store = new VegetationRuntimeDataStore();
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            gatedResidency);
        residency.RegisterPreparedProvider(new AssetTypeFilteringPreparedProvider(
            vegetationProvider,
            VegetationAssetTypes.InstancePage));
        RuntimeAssetResidencyKey pageKey = fixture.PageKey(pageIndex: 0);
        using RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetPageClosure(pageIndex: 0, parentClusterIndex: 0),
            pinned: false);
        residency.ProcessAtFrameBoundary();
        vegetationProvider.WaitForWorkerIdle();
        gatedResidency.GateNextClaimCapture();
        Task consumeFailure = Task.Run(residency.ProcessAtFrameBoundary);

        try
        {
            Assert.True(
                gatedResidency.ClaimCaptured.Wait(TimeSpan.FromSeconds(10)),
                "Vegetation failure consumption did not capture the pending claim.");
            using RuntimeAssetResidencyLease second = residency.AcquireSceneDependencies(
                CellOwner(2, generation: 1),
                fixture.GetPageClosure(pageIndex: 0, parentClusterIndex: 0),
                pinned: false);
            gatedResidency.AllowClaimReturn.Set();
            await consumeFailure.WaitAsync(TimeSpan.FromSeconds(10));

            RuntimeAssetResidencySnapshot page = residency.GetResources().Single(
                resource => resource.Key == pageKey);
            Assert.Equal(RuntimePreparedAssetState.Waiting, page.PreparedState);
            Assert.DoesNotContain("failed", page.Diagnostic, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, residency.GetMetrics().FailedAssetCount);
            Assert.Equal(default, store.GetMetrics());
        }
        finally
        {
            gatedResidency.AllowClaimReturn.Set();
            await consumeFailure.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public async Task ProviderShutdownCancelsAndJoinsAnInFlightWorker()
    {
        using var fixture = new VegetationResidencyFixture();
        using var gatedScheduler = new GatedBackgroundTaskScheduler(fixture.Scheduler);
        var store = new VegetationRuntimeDataStore();
        var residency = fixture.CreateResidency(maxInactiveResources: 0);
        var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            gatedScheduler,
            store,
            residency);
        var pageProvider = new AssetTypeFilteringPreparedProvider(
            vegetationProvider,
            VegetationAssetTypes.InstancePage);
        var key = new RuntimeAssetResidencyKey(
            fixture.PageGuids[0],
            VegetationResidencyFixture.PackageId,
            VegetationAssetTypes.InstancePage,
            VegetationInstancePageAssetCooker.RuntimeVariant);
        residency.RegisterPreparedProvider(pageProvider);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetPageClosure(pageIndex: 0, parentClusterIndex: 0),
            pinned: false);

        residency.ProcessAtFrameBoundary();
        Assert.True(
            gatedScheduler.WorkerEntered.Wait(TimeSpan.FromSeconds(10)),
            "Vegetation page preparation did not enter the gated worker.");
        Assert.True(residency.UnregisterPreparedProvider(pageProvider.ProviderId));
        Assert.True(Assert.Single(
            gatedScheduler.GetDrainSnapshot().OutstandingTasks).CancellationRequested);

        Task dispose = Task.Run(vegetationProvider.Dispose);
        try
        {
            Assert.True(SpinWait.SpinUntil(
                () => vegetationProvider.Prepare(key).Diagnostic.Contains(
                    "disposed",
                    StringComparison.OrdinalIgnoreCase),
                TimeSpan.FromSeconds(10)));
            Assert.False(dispose.IsCompleted);
        }
        finally
        {
            gatedScheduler.AllowWorker.Set();
            await dispose.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(0, gatedScheduler.OutstandingTaskCount);
        Assert.Equal(0, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.True(store.GetSnapshot().IsEmpty);

        lease.Dispose();
        residency.Dispose();
        Assert.Empty(fixture.Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public void PendingWorkerEvictionCannotPublishAndFreshGenerationRetries()
    {
        using var fixture = new VegetationResidencyFixture();
        using var gatedScheduler = new GatedBackgroundTaskScheduler(fixture.Scheduler);
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            gatedScheduler,
            store,
            residency);
        var pageProvider = new AssetTypeFilteringPreparedProvider(
            vegetationProvider,
            VegetationAssetTypes.InstancePage);
        var key = new RuntimeAssetResidencyKey(
            fixture.PageGuids[0],
            VegetationResidencyFixture.PackageId,
            VegetationAssetTypes.InstancePage,
            VegetationInstancePageAssetCooker.RuntimeVariant);
        CookedSceneDependency[] dependencies =
            fixture.GetPageClosure(pageIndex: 0, parentClusterIndex: 0);
        residency.RegisterPreparedProvider(pageProvider);
        RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            dependencies,
            pinned: false);
        int callerThreadId = Environment.CurrentManagedThreadId;

        try
        {
            residency.ProcessAtFrameBoundary();

            Assert.True(
                gatedScheduler.WorkerEntered.Wait(TimeSpan.FromSeconds(10)),
                "Vegetation page preparation did not enter the worker-owned decode.");
            Assert.NotEqual(callerThreadId, gatedScheduler.WorkerThreadId);
            RuntimeAssetResidencySnapshot waiting = residency.GetResources().Single(
                resource => resource.Key == key);
            Assert.Equal(RuntimePreparedAssetState.Waiting, waiting.PreparedState);
            Assert.Equal(pageProvider.ProviderId, waiting.ProviderId);
            Assert.True(store.GetSnapshot().IsEmpty);

            lease.Dispose();
            residency.ProcessAtFrameBoundary();

            Assert.Empty(residency.GetResources());
            Assert.False(vegetationProvider.HasPendingPreparations);
            Assert.True(store.GetSnapshot().IsEmpty);
        }
        finally
        {
            gatedScheduler.AllowWorker.Set();
        }

        vegetationProvider.WaitForWorkerIdle();

        Assert.Equal(0, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Equal(default, store.GetMetrics());
        Assert.Empty(fixture.Database.GetLoadedCookedAssetDiagnostics());

        Assert.True(residency.UnregisterPreparedProvider(pageProvider.ProviderId));
        residency.RegisterPreparedProvider(vegetationProvider);
        residency.RegisterPreparedProvider(new ImmediateRenderingPreparedProvider());

        using RuntimeAssetResidencyLease retry = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 2),
            dependencies,
            pinned: false);
        ProcessUntilTerminal(residency, vegetationProvider, retry);

        Assert.Equal(RuntimePreparedAssetState.Ready, retry.State);
        Assert.Equal(1, store.GetMetrics().ResidentPageCount);
        Assert.Equal(1, store.GetMetrics().CompleteClusterCount);

        retry.Dispose();
        residency.ProcessAtFrameBoundary();

        Assert.Empty(residency.GetResources());
        Assert.Equal(0, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Equal(default, store.GetMetrics());
        Assert.Empty(fixture.Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public void PartialAcquisitionCancellationRollsBackOwnerAndCookedHandle()
    {
        using var fixture = new VegetationResidencyFixture();
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        residency.RegisterPreparedProvider(vegetationProvider);
        residency.RegisterPreparedProvider(new ImmediateRenderingPreparedProvider());
        using var cancellation = new CancellationTokenSource();
        Guid loadedGuid = Guid.Empty;
        fixture.Database.SuccessfulCookedAssetLoad = guid =>
        {
            loadedGuid = guid;
            fixture.Database.SuccessfulCookedAssetLoad = null;
            cancellation.Cancel();
        };

        Assert.Throws<OperationCanceledException>(() =>
            residency.AcquireSceneDependencies(
                CellOwner(1, generation: 1),
                fixture.GetClosure(clusterIndex: 0),
                pinned: false,
                cancellation.Token));

        Assert.Equal(VegetationResidencyFixture.SpeciesGuid, loadedGuid);
        RuntimeAssetResidencySnapshot rolledBack = Assert.Single(residency.GetResources());
        Assert.Equal(loadedGuid, rolledBack.Key.Guid);
        Assert.Equal(0, rolledBack.OwnerCount);
        Assert.Empty(rolledBack.Owners);
        RuntimeAssetResidencyMetrics rolledBackMetrics = residency.GetMetrics();
        Assert.Equal(0, rolledBackMetrics.ActiveOwnerCount);
        Assert.Equal(1, rolledBackMetrics.InactiveAssetCount);
        Assert.Equal(0, vegetationProvider.GetMetrics().PreparedResourceCount);
        Assert.Equal(default, store.GetMetrics());

        residency.ProcessAtFrameBoundary();

        Assert.Empty(residency.GetResources());
        Assert.Equal(1, residency.GetMetrics().EvictionCount);
        Assert.True(store.GetSnapshot().IsEmpty);
        Assert.Empty(fixture.Database.GetLoadedCookedAssetDiagnostics());
    }

    [Theory]
    [InlineData(VegetationAssetTypes.Cluster)]
    [InlineData(VegetationAssetTypes.InstancePage)]
    public void FailedVegetationPreparationRequiresFreshOwnerGenerationBeforeRetry(
        string failedAssetType)
    {
        using var fixture = new VegetationResidencyFixture();
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 0);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        Guid failedGuid = failedAssetType == VegetationAssetTypes.Cluster
            ? fixture.ClusterGuids[0]
            : fixture.PageGuids[0];
        var failFirst = new FailFirstPreparedProvider(vegetationProvider, failedGuid);
        residency.RegisterPreparedProvider(failFirst);
        residency.RegisterPreparedProvider(new ImmediateRenderingPreparedProvider());
        RuntimeAssetResidencyLease failed = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);

        ProcessUntilTerminal(residency, vegetationProvider, failed);

        Assert.Equal(RuntimePreparedAssetState.Failed, failed.State);
        Assert.Contains(failedGuid.ToString("D"), failed.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(1, failFirst.GetPrepareCount(failedGuid));
        VegetationRuntimeDataMetrics failedMetrics = store.GetMetrics();
        Assert.Equal(failedAssetType == VegetationAssetTypes.InstancePage ? 1 : 0,
            failedMetrics.ResidentClusterCount);
        Assert.Equal(failedAssetType == VegetationAssetTypes.Cluster ? 1 : 0,
            failedMetrics.ResidentPageCount);
        Assert.Equal(0, failedMetrics.CompleteClusterCount);
        Assert.True(store.GetSnapshot().IsEmpty);

        residency.ProcessAtFrameBoundary();
        Assert.Equal(1, failFirst.GetPrepareCount(failedGuid));
        failed.Dispose();
        residency.ProcessAtFrameBoundary();

        Assert.Empty(residency.GetResources());
        Assert.Equal(default, store.GetMetrics());
        Assert.Empty(fixture.Database.GetLoadedCookedAssetDiagnostics());

        using RuntimeAssetResidencyLease retry = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 2),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);
        Assert.Equal(2, retry.Owner.Generation);
        ProcessUntilTerminal(residency, vegetationProvider, retry);

        Assert.Equal(RuntimePreparedAssetState.Ready, retry.State);
        Assert.Equal(2, failFirst.GetPrepareCount(failedGuid));
        Assert.Equal(1, store.GetMetrics().CompleteClusterCount);
        Assert.Equal(1, store.GetMetrics().CompletePageCount);
    }

    [Fact]
    public void InactiveVegetationClosureUsesDeterministicLeastRecentlyNeededOrder()
    {
        using var fixture = new VegetationResidencyFixture();
        var store = new VegetationRuntimeDataStore();
        using var residency = fixture.CreateResidency(maxInactiveResources: 2);
        using var vegetationProvider = new VegetationPreparedAssetProvider(
            fixture.Database,
            fixture.Scheduler,
            store,
            residency);
        residency.RegisterPreparedProvider(vegetationProvider);
        residency.RegisterPreparedProvider(new ImmediateRenderingPreparedProvider());
        RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            fixture.GetClosure(clusterIndex: 0),
            pinned: false);
        RuntimeAssetResidencyLease second = residency.AcquireSceneDependencies(
            CellOwner(2, generation: 1),
            fixture.GetClosure(clusterIndex: 1),
            pinned: false);
        ProcessUntilTerminal(residency, vegetationProvider, first, second);
        Assert.Equal(RuntimePreparedAssetState.Ready, first.State);
        Assert.Equal(RuntimePreparedAssetState.Ready, second.State);

        first.Dispose();
        residency.ProcessAtFrameBoundary();

        Assert.Equal(0, residency.GetMetrics().EvictionCount);
        Assert.Equal(2, residency.GetMetrics().InactiveAssetCount);
        Assert.Equal(2, store.GetMetrics().CompleteClusterCount);

        second.Dispose();
        residency.ProcessAtFrameBoundary();

        RuntimeAssetResidencySnapshot[] retained = residency.GetResources().ToArray();
        Assert.Equal(2, retained.Length);
        Assert.Equal(
            [fixture.ClusterGuids[1], fixture.PageGuids[1]],
            retained.Select(resource => resource.Key.Guid).ToArray());
        Assert.All(retained, resource => Assert.Equal(0, resource.OwnerCount));
        RuntimeAssetResidencyMetrics metrics = residency.GetMetrics();
        Assert.Equal(2, metrics.InactiveAssetCount);
        Assert.Equal(6, metrics.EvictionCount);
        VegetationRuntimeDataMetrics vegetationMetrics = store.GetMetrics();
        Assert.Equal(1, vegetationMetrics.ResidentClusterCount);
        Assert.Equal(1, vegetationMetrics.ResidentPageCount);
        Assert.Equal(1, vegetationMetrics.CompleteClusterCount);
        Assert.Equal(fixture.ClusterGuids[1], store.GetSnapshot().Clusters[0].Guid);
        Assert.Equal(2, vegetationProvider.GetMetrics().PreparedResourceCount);
    }

    private static RuntimeAssetResidencyOwnerId CellOwner(int cell, long generation) =>
        RuntimeAssetResidencyOwnerId.Cell(
            s_WorldGuid,
            new WorldCellId(new Guid($"b9200000-0000-0000-0000-{cell:D12}")),
            generation);

    private static RuntimePreparedAssetResult CompleteDirectPreparation(
        VegetationPreparedAssetProvider provider,
        RuntimeAssetResidencyKey key)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            RuntimePreparedAssetResult result = provider.Prepare(key);
            if (result.State != RuntimePreparedAssetState.Waiting)
            {
                return result;
            }

            provider.WaitForWorkerIdle();
        }

        throw new Xunit.Sdk.XunitException(
            $"Vegetation direct preparation for '{key}' did not reach a terminal state.");
    }

    private static RuntimeAssetPreparationClaim[] CapturePreparationClaims(
        RuntimeAssetResidencyService residency,
        IReadOnlyList<RuntimeAssetResidencyKey> keys)
    {
        var claims = new RuntimeAssetPreparationClaim[keys.Count];
        for (int index = 0; index < keys.Count; index++)
        {
            Assert.True(residency.TryGetPreparationClaim(keys[index], out claims[index]));
        }

        return claims;
    }

    private static RuntimeAssetResidencySnapshot ProcessResourceUntilTerminal(
        RuntimeAssetResidencyService residency,
        VegetationPreparedAssetProvider provider,
        RuntimeAssetResidencyKey key)
    {
        for (int attempt = 0; attempt < 32; attempt++)
        {
            residency.ProcessAtFrameBoundary();
            provider.WaitForWorkerIdle();
            RuntimeAssetResidencySnapshot snapshot = residency.GetResources().Single(
                resource => resource.Key == key);
            if (!provider.HasPendingPreparations &&
                snapshot.PreparedState != RuntimePreparedAssetState.Waiting)
            {
                return snapshot;
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"Vegetation resource '{key}' did not reach a terminal prepared state.");
    }

    private static void ProcessUntilTerminal(
        RuntimeAssetResidencyService residency,
        VegetationPreparedAssetProvider provider,
        params RuntimeAssetResidencyLease[] leases)
    {
        for (int attempt = 0; attempt < 32; attempt++)
        {
            residency.ProcessAtFrameBoundary();
            provider.WaitForWorkerIdle();
            if (!provider.HasPendingPreparations &&
                leases.All(lease => lease.State != RuntimePreparedAssetState.Waiting))
            {
                return;
            }
        }

        throw new Xunit.Sdk.XunitException(
            "Vegetation worker preparation did not reach a terminal state.");
    }

    public enum ClusterClosureMismatch
    {
        PageHash,
        InstanceCount,
        Bounds,
        SpeciesUnion
    }

    public enum PageClosureValidationCase
    {
        Size,
        SpeciesMembership
    }

    private enum RemoveOutcome
    {
        Throw,
        ReturnFalse
    }

    private sealed class WaitOncePreparedProvider : IRuntimePreparedAssetProvider
    {
        private readonly VegetationPreparedAssetProvider m_Inner;
        private int m_Waited;

        public WaitOncePreparedProvider(VegetationPreparedAssetProvider inner)
        {
            m_Inner = inner;
        }

        public string ProviderId => "test.vegetation.wait-once";

        public bool Waited => Volatile.Read(ref m_Waited) != 0;

        public bool Supports(string assetType) => m_Inner.Supports(assetType);

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            if (Interlocked.Exchange(ref m_Waited, 1) == 0)
            {
                return RuntimePreparedAssetResult.Waiting(
                    "The test provider intentionally waits for one frame.");
            }

            return m_Inner.Prepare(key);
        }

        public void Release(RuntimeAssetResidencyKey key) => m_Inner.Release(key);

        public RuntimePreparedAssetProviderMetrics GetMetrics() => m_Inner.GetMetrics();
    }

    private sealed class AssetTypeFilteringPreparedProvider : IRuntimePreparedAssetProvider
    {
        private readonly IRuntimePreparedAssetProvider m_Inner;
        private readonly string m_AssetType;

        public AssetTypeFilteringPreparedProvider(
            IRuntimePreparedAssetProvider inner,
            string assetType)
        {
            m_Inner = inner;
            m_AssetType = assetType;
        }

        public string ProviderId => m_Inner.ProviderId;

        public bool Supports(string assetType) =>
            string.Equals(assetType, m_AssetType, StringComparison.Ordinal);

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key) =>
            m_Inner.Prepare(key);

        public void Release(RuntimeAssetResidencyKey key) => m_Inner.Release(key);

        public RuntimePreparedAssetProviderMetrics GetMetrics() => m_Inner.GetMetrics();
    }

    private sealed class ImmediateRenderingPreparedProvider : IRuntimePreparedAssetProvider
    {
        private readonly HashSet<RuntimeAssetResidencyKey> m_Prepared = new();

        public string ProviderId => "test.vegetation-rendering-dependencies";

        public bool Supports(string assetType) => assetType is "Mesh" or "Material";

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            m_Prepared.Add(key);
            return RuntimePreparedAssetResult.Ready(estimatedGpuBytes: 0);
        }

        public void Release(RuntimeAssetResidencyKey key) => m_Prepared.Remove(key);

        public RuntimePreparedAssetProviderMetrics GetMetrics() => new(
            m_Prepared.Count,
            EstimatedGpuBytes: 0,
            PendingDisposalCount: 0);
    }

    private sealed class TrackingVegetationGpuResourceFactory :
        IVegetationClusterGpuResourceFactory
    {
        public const long ResourceBytes = 4096;

        private readonly HashSet<TrackingVegetationGpuResource> m_Live = new();

        public int CreatedCount { get; private set; }

        public int ReleaseCount { get; private set; }

        public int ReleaseFailuresRemaining { get; set; }

        public int LiveResourceCount => m_Live.Count;

        public int PendingDisposalCount => 0;

        public void InvalidateDependencies()
        {
            foreach (TrackingVegetationGpuResource resource in m_Live)
            {
                resource.DependenciesCurrent = false;
            }
        }

        public void UpdateFrameContext(
            ArisenEngine.Core.RHI.RHIDevice device,
            ulong deviceGeneration)
        {
        }

        public VegetationGpuResourceBuildResult TryCreate(
            CookedVegetationCluster cluster,
            IReadOnlyList<CookedVegetationSpecies> species,
            IReadOnlyList<CookedVegetationInstancePage> pages)
        {
            var resource = new TrackingVegetationGpuResource();
            Assert.True(m_Live.Add(resource));
            CreatedCount++;
            return VegetationGpuResourceBuildResult.Ready(resource);
        }

        public void RequestRelease(IVegetationClusterGpuResource resource)
        {
            var tracked = Assert.IsType<TrackingVegetationGpuResource>(resource);
            if (ReleaseFailuresRemaining > 0)
            {
                ReleaseFailuresRemaining--;
                throw new InvalidOperationException(
                    "Injected vegetation GPU resource release failure.");
            }
            Assert.True(m_Live.Remove(tracked), "GPU resource was released more than once.");
            tracked.Dispose();
            ReleaseCount++;
        }

        public void UpdateSubmittedTicket(ulong submittedTicket)
        {
        }

        public void ReleaseAllDeviceResources()
        {
            Assert.Empty(m_Live);
        }

        private sealed class TrackingVegetationGpuResource :
            IVegetationClusterGpuResource
        {
            private bool m_Disposed;

            public long EstimatedGpuBytes => ResourceBytes;

            public bool DependenciesCurrent { get; set; } = true;

            public VegetationPreparedClusterView CreateView(ulong generation) => default;

            public void Dispose()
            {
                Assert.False(m_Disposed, "GPU resource was disposed more than once.");
                m_Disposed = true;
            }
        }
    }

    private sealed class FailFirstPreparedProvider : IRuntimePreparedAssetProvider
    {
        private readonly IRuntimePreparedAssetProvider m_Inner;
        private readonly Guid m_FailedGuid;
        private int m_PrepareCount;
        private bool m_AttemptActive;

        public FailFirstPreparedProvider(
            IRuntimePreparedAssetProvider inner,
            Guid failedGuid)
        {
            m_Inner = inner;
            m_FailedGuid = failedGuid;
        }

        public string ProviderId => m_Inner.ProviderId;

        public bool Supports(string assetType) => m_Inner.Supports(assetType);

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            if (key.Guid == m_FailedGuid)
            {
                if (!m_AttemptActive)
                {
                    m_AttemptActive = true;
                    m_PrepareCount++;
                }

                if (m_PrepareCount == 1)
                {
                    return RuntimePreparedAssetResult.Failed(
                        $"Injected first vegetation preparation failure for '{key}'.");
                }
            }

            return m_Inner.Prepare(key);
        }

        public void Release(RuntimeAssetResidencyKey key)
        {
            m_Inner.Release(key);
            if (key.Guid == m_FailedGuid)
            {
                m_AttemptActive = false;
            }
        }

        public RuntimePreparedAssetProviderMetrics GetMetrics() => m_Inner.GetMetrics();

        public int GetPrepareCount(Guid guid) => guid == m_FailedGuid ? m_PrepareCount : 0;
    }

    private sealed class FaultInjectingVegetationRuntimeDataStore :
        IVegetationRuntimeDataStore,
        IDisposable
    {
        private readonly object m_Gate = new();
        private readonly VegetationRuntimeDataStore m_Inner = new();
        private readonly Dictionary<Guid, Queue<RemoveOutcome>> m_RemoveOutcomes = new();
        private readonly Dictionary<Guid, int> m_RemoveAttempts = new();
        private int m_BlockNextClusterPublication;

        public ManualResetEventSlim ClusterPublicationVisible { get; } = new(false);

        public ManualResetEventSlim AllowClusterPublicationReturn { get; } = new(false);

        public VegetationPreparedClusterData PrepareCluster(CookedVegetationCluster cluster) =>
            m_Inner.PrepareCluster(cluster);

        public VegetationPreparedPageData PreparePage(
            CookedVegetationInstancePage page,
            VegetationInstancePagePayloadIdentity payloadIdentity) =>
            m_Inner.PreparePage(page, payloadIdentity);

        public VegetationResidentResourceHandle PublishCluster(
            VegetationPreparedClusterData cluster)
        {
            VegetationResidentResourceHandle handle = m_Inner.PublishCluster(cluster);
            if (Interlocked.Exchange(ref m_BlockNextClusterPublication, 0) != 0)
            {
                ClusterPublicationVisible.Set();
                AllowClusterPublicationReturn.Wait();
            }

            return handle;
        }

        public VegetationResidentResourceHandle PublishPage(VegetationPreparedPageData page) =>
            m_Inner.PublishPage(page);

        public bool Remove(VegetationResidentResourceHandle handle)
        {
            RemoveOutcome? outcome = null;
            lock (m_Gate)
            {
                m_RemoveAttempts.TryGetValue(handle.Guid, out int attempts);
                m_RemoveAttempts[handle.Guid] = checked(attempts + 1);
                if (m_RemoveOutcomes.TryGetValue(handle.Guid, out Queue<RemoveOutcome>? outcomes) &&
                    outcomes.Count != 0)
                {
                    outcome = outcomes.Dequeue();
                }
            }

            return outcome switch
            {
                RemoveOutcome.Throw => throw new InvalidOperationException(
                    $"Injected vegetation removal failure for '{handle.Guid:D}'."),
                RemoveOutcome.ReturnFalse => false,
                _ => m_Inner.Remove(handle)
            };
        }

        public VegetationClusterDataSnapshot GetSnapshot() => m_Inner.GetSnapshot();

        public VegetationRuntimeDataMetrics GetMetrics() => m_Inner.GetMetrics();

        public VegetationResidentResourceHandle PublishPageForTest(
            CookedVegetationInstancePage page) => m_Inner.PublishPage(page);

        public void SetRemoveOutcomes(Guid guid, params RemoveOutcome[] outcomes)
        {
            Assert.NotEmpty(outcomes);
            lock (m_Gate)
            {
                m_RemoveOutcomes[guid] = new Queue<RemoveOutcome>(outcomes);
            }
        }

        public int GetRemoveAttemptCount(Guid guid)
        {
            lock (m_Gate)
            {
                return m_RemoveAttempts.GetValueOrDefault(guid);
            }
        }

        public void BlockNextClusterPublication()
        {
            ClusterPublicationVisible.Reset();
            AllowClusterPublicationReturn.Reset();
            Volatile.Write(ref m_BlockNextClusterPublication, 1);
        }

        public void Dispose()
        {
            AllowClusterPublicationReturn.Set();
            ClusterPublicationVisible.Dispose();
            AllowClusterPublicationReturn.Dispose();
        }
    }

    private sealed class GatedClaimResidencyService : IRuntimeAssetResidencyService, IDisposable
    {
        private readonly IRuntimeAssetResidencyService m_Inner;
        private int m_GateNextClaimCapture;
        private int m_GateNextPublication;
        private int m_PublicationAttemptCount;

        public GatedClaimResidencyService(IRuntimeAssetResidencyService inner)
        {
            m_Inner = inner;
        }

        public ManualResetEventSlim ClaimCaptured { get; } = new(false);

        public ManualResetEventSlim AllowClaimReturn { get; } = new(false);

        public ManualResetEventSlim PublicationVisible { get; } = new(false);

        public ManualResetEventSlim AllowPublicationReturn { get; } = new(false);

        public int PublicationAttemptCount => Volatile.Read(ref m_PublicationAttemptCount);

        public RuntimeAssetResidencyBudgets Budgets => m_Inner.Budgets;

        public RuntimeAssetResidencyLease AcquireSceneDependencies(
            RuntimeAssetResidencyOwnerId owner,
            IReadOnlyList<CookedSceneDependency> dependencies,
            bool pinned,
            CancellationToken cancellationToken = default) =>
            m_Inner.AcquireSceneDependencies(owner, dependencies, pinned, cancellationToken);

        public void RegisterPreparedProvider(IRuntimePreparedAssetProvider provider) =>
            m_Inner.RegisterPreparedProvider(provider);

        public bool IsPreparedProviderRegistered(IRuntimePreparedAssetProvider provider) =>
            m_Inner.IsPreparedProviderRegistered(provider);

        public bool UnregisterPreparedProvider(string providerId) =>
            m_Inner.UnregisterPreparedProvider(providerId);

        public bool InvalidatePreparedProvider(string providerId, string diagnostic) =>
            m_Inner.InvalidatePreparedProvider(providerId, diagnostic);

        public bool TryGetPreparationClaim(
            RuntimeAssetResidencyKey key,
            out RuntimeAssetPreparationClaim claim)
        {
            bool found = m_Inner.TryGetPreparationClaim(key, out claim);
            if (Interlocked.Exchange(ref m_GateNextClaimCapture, 0) != 0)
            {
                ClaimCaptured.Set();
                AllowClaimReturn.Wait();
            }

            return found;
        }

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
            out string diagnostic)
        {
            return m_Inner.TryCommitPreparedPublication(
                claim,
                canonicalRequiredClaims,
                canonicalRequiredKeys,
                estimatedGpuBytes,
                () =>
                {
                    Interlocked.Increment(ref m_PublicationAttemptCount);
                    publish();
                    if (Interlocked.Exchange(ref m_GateNextPublication, 0) != 0)
                    {
                        PublicationVisible.Set();
                        AllowPublicationReturn.Wait();
                    }
                },
                out diagnostic);
        }

        public void ProcessAtFrameBoundary() => m_Inner.ProcessAtFrameBoundary();

        public IReadOnlyList<RuntimeAssetResidencySnapshot> GetResources() =>
            m_Inner.GetResources();

        public RuntimeAssetResidencyMetrics GetMetrics() => m_Inner.GetMetrics();

        public void GateNextClaimCapture()
        {
            ClaimCaptured.Reset();
            AllowClaimReturn.Reset();
            Volatile.Write(ref m_GateNextClaimCapture, 1);
        }

        public void GateNextPublication()
        {
            PublicationVisible.Reset();
            AllowPublicationReturn.Reset();
            Volatile.Write(ref m_GateNextPublication, 1);
        }

        public void Dispose()
        {
            AllowClaimReturn.Set();
            AllowPublicationReturn.Set();
            ClaimCaptured.Dispose();
            AllowClaimReturn.Dispose();
            PublicationVisible.Dispose();
            AllowPublicationReturn.Dispose();
        }
    }

    private sealed class GatedBackgroundTaskScheduler : IBackgroundTaskScheduler, IDisposable
    {
        private readonly IBackgroundTaskScheduler m_Inner;
        private int m_WorkerThreadId;

        public GatedBackgroundTaskScheduler(IBackgroundTaskScheduler inner)
        {
            m_Inner = inner;
        }

        public ManualResetEventSlim WorkerEntered { get; } = new(false);

        public ManualResetEventSlim AllowWorker { get; } = new(false);

        public int OutstandingTaskCount => m_Inner.OutstandingTaskCount;

        public int WorkerThreadId => Volatile.Read(ref m_WorkerThreadId);

        public BackgroundTaskDrainSnapshot GetDrainSnapshot() => m_Inner.GetDrainSnapshot();

        public BackgroundTask<T> Schedule<T>(
            string name,
            Func<CancellationToken, T> operation,
            CancellationToken cancellationToken = default)
        {
            return m_Inner.Schedule(
                name,
                token =>
                {
                    Volatile.Write(ref m_WorkerThreadId, Environment.CurrentManagedThreadId);
                    WorkerEntered.Set();
                    AllowWorker.Wait();
                    return operation(token);
                },
                cancellationToken);
        }

        public void Dispose()
        {
            AllowWorker.Set();
            WorkerEntered.Dispose();
            AllowWorker.Dispose();
        }
    }

    private sealed class VegetationResidencyFixture : IDisposable
    {
        public const string PackageId = "com.arisen.vegetation.residency-test";
        public static readonly Guid SpeciesGuid =
            Guid.Parse("b9300000-0000-0000-0000-000000000001");
        private static readonly Guid s_BiomeGuid =
            Guid.Parse("b9400000-0000-0000-0000-000000000001");
        private static readonly Guid s_MeshGuid =
            Guid.Parse("b9500000-0000-0000-0000-000000000001");
        private static readonly Guid s_MaterialGuid =
            Guid.Parse("b9600000-0000-0000-0000-000000000001");
        private static readonly Guid s_AlternateSpeciesGuid =
            Guid.Parse("b9300000-0000-0000-0000-000000000002");
        private static readonly Guid s_AlternateMeshGuid =
            Guid.Parse("b9500000-0000-0000-0000-000000000002");
        private static readonly Guid s_AlternateMaterialGuid =
            Guid.Parse("b9600000-0000-0000-0000-000000000002");
        private static readonly Guid s_AllSpeciesBiomeGuid =
            Guid.Parse("b9400000-0000-0000-0000-000000000002");
        private static readonly Guid s_AlternateOnlyBiomeGuid =
            Guid.Parse("b9400000-0000-0000-0000-000000000003");
        private static readonly Guid s_ReplacementPageGuid =
            Guid.Parse("b9800000-0000-0000-0000-000000000003");

        private readonly string m_Root;
        private readonly bool m_IncludeWorldStreamingFixture;

        public VegetationResidencyFixture(bool includeWorldStreamingFixture = false)
        {
            m_IncludeWorldStreamingFixture = includeWorldStreamingFixture;
            Scheduler = new TaskGraph(workerCount: 2);
            m_Root = Path.Combine(
                Path.GetTempPath(),
                "ArisenVegetationResidencyTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Root);
            Database = new TestAssetDatabase(
                AssetSourceAccessMode.RuntimeAssetCook,
                Path.Combine(m_Root, "Cooked"));
            ClusterGuids =
            [
                Guid.Parse("b9700000-0000-0000-0000-000000000001"),
                Guid.Parse("b9700000-0000-0000-0000-000000000002")
            ];
            PageGuids =
            [
                Guid.Parse("b9800000-0000-0000-0000-000000000001"),
                Guid.Parse("b9800000-0000-0000-0000-000000000002")
            ];

            AddAsset(SpeciesGuid, VegetationAssetTypes.Species, "Species.yaml", SpeciesSource());
            AddAsset(s_BiomeGuid, VegetationAssetTypes.Biome, "Biome.yaml", BiomeSource());
            AddAsset(s_MeshGuid, "Mesh", "Mesh.asset", "mesh");
            AddAsset(s_MaterialGuid, "Material", "Material.asset", "material");
            AddAsset(
                s_AlternateSpeciesGuid,
                VegetationAssetTypes.Species,
                "AlternateSpecies.yaml",
                AlternateSpeciesSource());
            AddAsset(
                s_AllSpeciesBiomeGuid,
                VegetationAssetTypes.Biome,
                "AllSpeciesBiome.yaml",
                AllSpeciesBiomeSource());
            AddAsset(
                s_AlternateOnlyBiomeGuid,
                VegetationAssetTypes.Biome,
                "AlternateOnlyBiome.yaml",
                AlternateOnlyBiomeSource());
            AddAsset(s_AlternateMeshGuid, "Mesh", "AlternateMesh.asset", "alternate-mesh");
            AddAsset(
                s_AlternateMaterialGuid,
                "Material",
                "AlternateMaterial.asset",
                "alternate-material");
            for (int index = 0; index < ClusterGuids.Length; index++)
            {
                AddAsset(
                    ClusterGuids[index],
                    VegetationAssetTypes.Cluster,
                    $"Cluster{index}.generated",
                    "cluster");
                AddAsset(
                    PageGuids[index],
                    VegetationAssetTypes.InstancePage,
                    $"Page{index}.generated",
                    "page");
            }
            AddAsset(
                s_ReplacementPageGuid,
                VegetationAssetTypes.InstancePage,
                "ReplacementPage.generated",
                "replacement-page");

            RegisterRenderingArtifact(s_MeshGuid, "Mesh", "staticmesh.uint32");
            RegisterRenderingArtifact(s_MaterialGuid, "Material", "material.runtime");
            RegisterRenderingArtifact(s_AlternateMeshGuid, "Mesh", "staticmesh.uint32");
            RegisterRenderingArtifact(
                s_AlternateMaterialGuid,
                "Material",
                "material.runtime");
            VegetationSpeciesAssetCooker.Cook(
                Database,
                new AssetRef<VegetationSpeciesSourceAsset>(
                    SpeciesGuid,
                    VegetationAssetTypes.Species,
                    PackageId));
            VegetationSpeciesAssetCooker.Cook(
                Database,
                new AssetRef<VegetationSpeciesSourceAsset>(
                    s_AlternateSpeciesGuid,
                    VegetationAssetTypes.Species,
                    PackageId));
            VegetationBiomeAssetCooker.Cook(
                Database,
                new AssetRef<VegetationBiomeSourceAsset>(
                    s_BiomeGuid,
                    VegetationAssetTypes.Biome,
                    PackageId));
            VegetationBiomeAssetCooker.Cook(
                Database,
                new AssetRef<VegetationBiomeSourceAsset>(
                    s_AllSpeciesBiomeGuid,
                    VegetationAssetTypes.Biome,
                    PackageId));
            VegetationBiomeAssetCooker.Cook(
                Database,
                new AssetRef<VegetationBiomeSourceAsset>(
                    s_AlternateOnlyBiomeGuid,
                    VegetationAssetTypes.Biome,
                    PackageId));
            for (int index = 0; index < ClusterGuids.Length; index++)
            {
                Guid biomeGuid = m_IncludeWorldStreamingFixture && index == 0
                    ? s_AllSpeciesBiomeGuid
                    : s_BiomeGuid;
                CookedVegetationClusterArtifact artifact =
                    VegetationClusterAssetCooker.Cook(
                        Database,
                        CreateCluster(index, biomeGuid));
                if (index == 0)
                {
                    ClusterBounds = artifact.Bounds;
                }
            }
            VegetationInstancePageAssetCooker.Cook(
                Database,
                CreateReplacementPageDescriptor(clusterIndex: 0));

            if (m_IncludeWorldStreamingFixture)
            {
                CreateWorldStreamingFixture();
            }

            Database.UseReadOnlyRuntime();
        }

        public TestAssetDatabase Database { get; }

        public TaskGraph Scheduler { get; }

        public Guid[] ClusterGuids { get; }

        public Guid[] PageGuids { get; }

        public Guid ClusterGuid => ClusterGuids[0];

        public Guid PageGuid => PageGuids[0];

        public Guid MeshGuid => s_MeshGuid;

        public Guid MaterialGuid => s_MaterialGuid;

        public Guid AlternateSpeciesGuid => s_AlternateSpeciesGuid;

        public Guid AlternateMeshGuid => s_AlternateMeshGuid;

        public Guid AlternateMaterialGuid => s_AlternateMaterialGuid;

        public RuntimeAssetResidencyKey SpeciesKey => new(
            SpeciesGuid,
            PackageId,
            VegetationAssetTypes.Species,
            VegetationSpeciesAssetCooker.RuntimeVariant);

        public RuntimeAssetResidencyKey BiomeKey => new(
            s_BiomeGuid,
            PackageId,
            VegetationAssetTypes.Biome,
            VegetationBiomeAssetCooker.RuntimeVariant);

        public Guid PersistentSceneGuid { get; private set; }

        public Guid CellSceneGuid { get; private set; }

        public AssetRef<WorldSourceAsset> WorldAsset { get; private set; }

        public WorldCellId CellId { get; private set; }

        public WorldBounds ClusterBounds { get; private set; }

        public RuntimeAssetResidencyService CreateResidency(int maxInactiveResources) => new(
            Database,
            new RuntimeAssetResidencyBudgets(
                MaxCpuCookedBytes: 16 * 1024 * 1024,
                MaxPreparedGpuBytes: 16 * 1024 * 1024,
                MaxSetupsPerFrame: 32,
                MaxSetupMilliseconds: 1_000,
                MaxInactiveResources: maxInactiveResources));

        public CookedSceneDependency[] GetClosure(int clusterIndex) =>
        [
            Dependency(
                ClusterGuids[clusterIndex],
                VegetationAssetTypes.Cluster,
                VegetationClusterAssetCooker.RuntimeVariant),
            Dependency(
                PageGuids[clusterIndex],
                VegetationAssetTypes.InstancePage,
                VegetationInstancePageAssetCooker.RuntimeVariant),
            Dependency(
                s_BiomeGuid,
                VegetationAssetTypes.Biome,
                VegetationBiomeAssetCooker.RuntimeVariant),
            Dependency(
                SpeciesGuid,
                VegetationAssetTypes.Species,
                VegetationSpeciesAssetCooker.RuntimeVariant),
            Dependency(s_MeshGuid, "Mesh", "staticmesh.uint32"),
            Dependency(s_MaterialGuid, "Material", "material.runtime")
        ];

        public RuntimeAssetResidencyKey ClusterKey(int clusterIndex) => new(
            ClusterGuids[clusterIndex],
            PackageId,
            VegetationAssetTypes.Cluster,
            VegetationClusterAssetCooker.RuntimeVariant);

        public RuntimeAssetResidencyKey PageKey(int pageIndex) => new(
            PageGuids[pageIndex],
            PackageId,
            VegetationAssetTypes.InstancePage,
            VegetationInstancePageAssetCooker.RuntimeVariant);

        public CookedVegetationInstancePage GetCookedPage(int pageIndex) =>
            BuildPage(pageIndex);

        public CookedSceneDependency[] GetPageClosure(
            int pageIndex,
            int? parentClusterIndex)
        {
            var dependencies = new List<CookedSceneDependency>
            {
                Dependency(
                    PageGuids[pageIndex],
                    VegetationAssetTypes.InstancePage,
                    VegetationInstancePageAssetCooker.RuntimeVariant),
                Dependency(
                    SpeciesGuid,
                    VegetationAssetTypes.Species,
                    VegetationSpeciesAssetCooker.RuntimeVariant),
                Dependency(s_MeshGuid, "Mesh", "staticmesh.uint32"),
                Dependency(s_MaterialGuid, "Material", "material.runtime")
            };
            if (parentClusterIndex.HasValue)
            {
                int parentIndex = parentClusterIndex.Value;
                dependencies.Add(Dependency(
                    ClusterGuids[parentIndex],
                    VegetationAssetTypes.Cluster,
                    VegetationClusterAssetCooker.RuntimeVariant));
                dependencies.Add(Dependency(
                    PageGuids[parentIndex],
                    VegetationAssetTypes.InstancePage,
                    VegetationInstancePageAssetCooker.RuntimeVariant));
                dependencies.Add(Dependency(
                    s_BiomeGuid,
                    VegetationAssetTypes.Biome,
                    VegetationBiomeAssetCooker.RuntimeVariant));
            }

            return dependencies.ToArray();
        }

        public CookedSceneDependency[] GetPageValidationClosure(
            bool includeAlternateSpecies)
        {
            var dependencies = new List<CookedSceneDependency>(
                GetPageClosure(pageIndex: 0, parentClusterIndex: 0));
            if (includeAlternateSpecies)
            {
                dependencies.Add(Dependency(
                    s_AlternateSpeciesGuid,
                    VegetationAssetTypes.Species,
                    VegetationSpeciesAssetCooker.RuntimeVariant));
                dependencies.Add(Dependency(
                    s_AlternateMeshGuid,
                    "Mesh",
                    "staticmesh.uint32"));
                dependencies.Add(Dependency(
                    s_AlternateMaterialGuid,
                    "Material",
                    "material.runtime"));
            }

            return dependencies.ToArray();
        }

        public CookedSceneDependency[] GetSpeciesClosure(
            string dependencyType,
            bool substituteWrongDependency)
        {
            var dependencies = new List<CookedSceneDependency>
            {
                Dependency(
                    SpeciesGuid,
                    VegetationAssetTypes.Species,
                    VegetationSpeciesAssetCooker.RuntimeVariant)
            };
            if (!string.Equals(dependencyType, "Mesh", StringComparison.Ordinal) ||
                substituteWrongDependency)
            {
                dependencies.Add(Dependency(
                    string.Equals(dependencyType, "Mesh", StringComparison.Ordinal)
                        ? s_AlternateMeshGuid
                        : s_MeshGuid,
                    "Mesh",
                    "staticmesh.uint32"));
            }

            if (!string.Equals(dependencyType, "Material", StringComparison.Ordinal) ||
                substituteWrongDependency)
            {
                dependencies.Add(Dependency(
                    string.Equals(dependencyType, "Material", StringComparison.Ordinal)
                        ? s_AlternateMaterialGuid
                        : s_MaterialGuid,
                    "Material",
                    "material.runtime"));
            }

            return dependencies.ToArray();
        }

        public CookedSceneDependency[] GetBiomeClosure(bool substituteWrongSpecies)
        {
            var dependencies = new List<CookedSceneDependency>
            {
                Dependency(
                    s_BiomeGuid,
                    VegetationAssetTypes.Biome,
                    VegetationBiomeAssetCooker.RuntimeVariant)
            };
            if (substituteWrongSpecies)
            {
                dependencies.Add(Dependency(
                    s_AlternateSpeciesGuid,
                    VegetationAssetTypes.Species,
                    VegetationSpeciesAssetCooker.RuntimeVariant));
                dependencies.Add(Dependency(
                    s_AlternateMeshGuid,
                    "Mesh",
                    "staticmesh.uint32"));
                dependencies.Add(Dependency(
                    s_AlternateMaterialGuid,
                    "Material",
                    "material.runtime"));
            }

            return dependencies.ToArray();
        }

        public CookedSceneDependency[] GetSharedClusterMismatchClosure() =>
        [
            Dependency(
                ClusterGuids[0],
                VegetationAssetTypes.Cluster,
                VegetationClusterAssetCooker.RuntimeVariant),
            Dependency(
                PageGuids[1],
                VegetationAssetTypes.InstancePage,
                VegetationInstancePageAssetCooker.RuntimeVariant),
            Dependency(
                s_BiomeGuid,
                VegetationAssetTypes.Biome,
                VegetationBiomeAssetCooker.RuntimeVariant),
            Dependency(
                SpeciesGuid,
                VegetationAssetTypes.Species,
                VegetationSpeciesAssetCooker.RuntimeVariant),
            Dependency(s_MeshGuid, "Mesh", "staticmesh.uint32"),
            Dependency(s_MaterialGuid, "Material", "material.runtime")
        ];

        public CookedSceneDependency[] ReplaceWithValidChangedClosure(int clusterIndex)
        {
            if (clusterIndex != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(clusterIndex));
            }

            CookedVegetationInstancePage page = VegetationInstancePageAssetCooker.BuildForCook(
                Database,
                CreateReplacementPageDescriptor(clusterIndex));
            CookedVegetationCluster cluster = BuildCluster(
                ClusterGuids[clusterIndex],
                new CookedVegetationBiomeReference(s_BiomeGuid, PackageId),
                [page]);

            ReplaceCookedPayload(
                cluster.Guid,
                VegetationClusterAssetCooker.RuntimeVariant,
                VegetationClusterAssetCooker.WritePayload(cluster));
            return
            [
                Dependency(
                    cluster.Guid,
                    VegetationAssetTypes.Cluster,
                    VegetationClusterAssetCooker.RuntimeVariant),
                Dependency(
                    page.Guid,
                    VegetationAssetTypes.InstancePage,
                    VegetationInstancePageAssetCooker.RuntimeVariant),
                Dependency(
                    s_BiomeGuid,
                    VegetationAssetTypes.Biome,
                    VegetationBiomeAssetCooker.RuntimeVariant),
                Dependency(
                    SpeciesGuid,
                    VegetationAssetTypes.Species,
                    VegetationSpeciesAssetCooker.RuntimeVariant),
                Dependency(s_MeshGuid, "Mesh", "staticmesh.uint32"),
                Dependency(s_MaterialGuid, "Material", "material.runtime")
            ];
        }

        public void ReplacePageParent(int pageIndex, int parentClusterIndex)
        {
            VegetationInstancePageCookDescriptor descriptor = CreateCluster(pageIndex).Pages[0];
            CookedVegetationInstancePage page = VegetationInstancePageAssetCooker.BuildForCook(
                Database,
                descriptor with { ClusterGuid = ClusterGuids[parentClusterIndex] });
            ReplaceCookedPayload(
                page.Guid,
                VegetationInstancePageAssetCooker.RuntimeVariant,
                VegetationInstancePageAssetCooker.WritePayload(page));
        }

        public void ReplacePageWithUnsupportedSchema(int pageIndex)
        {
            Assert.True(Database.TryGetCookedArtifact(
                PageGuids[pageIndex],
                VegetationInstancePageAssetCooker.RuntimeVariant,
                out CookedAssetRecord artifact));
            byte[] payload = File.ReadAllBytes(artifact.Path);
            BinaryPrimitives.WriteInt32LittleEndian(
                payload.AsSpan(16),
                checked(VegetationInstancePageAssetCooker.CurrentGeneratedSchemaVersion + 1));
            ReplaceCookedPayload(
                PageGuids[pageIndex],
                VegetationInstancePageAssetCooker.RuntimeVariant,
                payload);
        }

        public CookedSceneDependency[] ReplaceClusterRootWithMismatch(
            ClusterClosureMismatch mismatch)
        {
            CookedVegetationInstancePage page = BuildPage(pageIndex: 0);
            CookedVegetationCluster cluster = BuildCluster(
                ClusterGuids[0],
                new CookedVegetationBiomeReference(s_BiomeGuid, PackageId),
                [page]);
            CookedVegetationInstancePageReference pageReference = cluster.Pages[0];
            switch (mismatch)
            {
                case ClusterClosureMismatch.PageHash:
                    byte[] wrongHash = pageReference.ContentHash.ToArray();
                    wrongHash[0] ^= 0xff;
                    cluster = cluster with
                    {
                        Pages = Array.AsReadOnly(
                            [pageReference with { ContentHash = wrongHash }])
                    };
                    break;
                case ClusterClosureMismatch.InstanceCount:
                    int wrongCount = checked(pageReference.InstanceCount + 1);
                    cluster = cluster with
                    {
                        Pages = Array.AsReadOnly(
                            [pageReference with { InstanceCount = wrongCount }]),
                        InstanceCount = wrongCount
                    };
                    break;
                case ClusterClosureMismatch.Bounds:
                    WorldBounds wrongBounds = ShiftBounds(pageReference.Bounds, 16.0);
                    cluster = cluster with
                    {
                        Pages = Array.AsReadOnly(
                            [pageReference with { Bounds = wrongBounds }]),
                        Bounds = wrongBounds
                    };
                    break;
                case ClusterClosureMismatch.SpeciesUnion:
                    cluster = cluster with
                    {
                        Biome = new CookedVegetationBiomeReference(
                            s_AllSpeciesBiomeGuid,
                            PackageId),
                        Species = Array.AsReadOnly(
                            [
                                new CookedVegetationSpeciesReference(SpeciesGuid, PackageId),
                                new CookedVegetationSpeciesReference(
                                    s_AlternateSpeciesGuid,
                                    PackageId)
                            ])
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mismatch));
            }

            ReplaceCookedPayload(
                cluster.Guid,
                VegetationClusterAssetCooker.RuntimeVariant,
                VegetationClusterAssetCooker.WritePayload(cluster));
            return mismatch == ClusterClosureMismatch.SpeciesUnion
                ? GetAllSpeciesClosure(s_AllSpeciesBiomeGuid, includeSecondPage: false)
                : GetClosure(clusterIndex: 0);
        }

        public CookedSceneDependency[] ReplaceClusterWithBiomeMembershipMismatch()
        {
            CookedVegetationCluster cluster = BuildCluster(
                ClusterGuids[0],
                new CookedVegetationBiomeReference(s_AlternateOnlyBiomeGuid, PackageId),
                [BuildPage(pageIndex: 0)]);
            ReplaceCookedPayload(
                cluster.Guid,
                VegetationClusterAssetCooker.RuntimeVariant,
                VegetationClusterAssetCooker.WritePayload(cluster));
            return GetAllSpeciesClosure(s_AlternateOnlyBiomeGuid, includeSecondPage: false);
        }

        public CookedSceneDependency[] ReplaceClusterWithDuplicateStableKeys()
        {
            CookedVegetationInstancePage first = BuildPage(pageIndex: 0);
            VegetationInstancePageCookDescriptor secondDescriptor = CreateCluster(1).Pages[0];
            CookedVegetationInstancePage second = VegetationInstancePageAssetCooker.BuildForCook(
                Database,
                secondDescriptor with { ClusterGuid = ClusterGuids[0] });
            CookedVegetationCluster cluster = BuildCluster(
                ClusterGuids[0],
                new CookedVegetationBiomeReference(s_BiomeGuid, PackageId),
                [first, second]);
            ReplaceCookedPayload(
                second.Guid,
                VegetationInstancePageAssetCooker.RuntimeVariant,
                VegetationInstancePageAssetCooker.WritePayload(second));
            ReplaceCookedPayload(
                cluster.Guid,
                VegetationClusterAssetCooker.RuntimeVariant,
                VegetationClusterAssetCooker.WritePayload(cluster));
            return
            [
                Dependency(
                    ClusterGuids[0],
                    VegetationAssetTypes.Cluster,
                    VegetationClusterAssetCooker.RuntimeVariant),
                Dependency(
                    PageGuids[0],
                    VegetationAssetTypes.InstancePage,
                    VegetationInstancePageAssetCooker.RuntimeVariant),
                Dependency(
                    PageGuids[1],
                    VegetationAssetTypes.InstancePage,
                    VegetationInstancePageAssetCooker.RuntimeVariant),
                Dependency(
                    s_BiomeGuid,
                    VegetationAssetTypes.Biome,
                    VegetationBiomeAssetCooker.RuntimeVariant),
                Dependency(
                    SpeciesGuid,
                    VegetationAssetTypes.Species,
                    VegetationSpeciesAssetCooker.RuntimeVariant),
                Dependency(s_MeshGuid, "Mesh", "staticmesh.uint32"),
                Dependency(s_MaterialGuid, "Material", "material.runtime")
            ];
        }

        private CookedVegetationInstancePage BuildPage(int pageIndex) =>
            VegetationInstancePageAssetCooker.BuildForCook(
                Database,
                CreateCluster(pageIndex).Pages[0]);

        private static CookedVegetationCluster BuildCluster(
            Guid clusterGuid,
            CookedVegetationBiomeReference biome,
            IReadOnlyList<CookedVegetationInstancePage> pages)
        {
            CookedVegetationInstancePage[] canonicalPages = pages
                .OrderBy(page => page.PackageId, StringComparer.Ordinal)
                .ThenBy(page => page.Guid)
                .ToArray();
            CookedVegetationSpeciesReference[] species = canonicalPages
                .SelectMany(page => page.Species)
                .Distinct()
                .OrderBy(reference => reference.PackageId, StringComparer.Ordinal)
                .ThenBy(reference => reference.Guid)
                .ToArray();
            var references = new CookedVegetationInstancePageReference[canonicalPages.Length];
            int instanceCount = 0;
            for (int index = 0; index < canonicalPages.Length; index++)
            {
                CookedVegetationInstancePage page = canonicalPages[index];
                byte[] payload = VegetationInstancePageAssetCooker.WritePayload(page);
                references[index] = new CookedVegetationInstancePageReference(
                    page.Guid,
                    page.PackageId,
                    page.Instances.Count,
                    page.Origin,
                    page.Bounds,
                    payload.LongLength,
                    SHA256.HashData(payload));
                instanceCount = checked(instanceCount + page.Instances.Count);
            }

            return new CookedVegetationCluster(
                clusterGuid,
                PackageId,
                VegetationClusterAssetCooker.CurrentGeneratedSchemaVersion,
                biome,
                UnionBounds(canonicalPages.Select(page => page.Bounds)),
                Array.AsReadOnly(species),
                Array.AsReadOnly(references),
                instanceCount);
        }

        private CookedSceneDependency[] GetAllSpeciesClosure(
            Guid biomeGuid,
            bool includeSecondPage)
        {
            var dependencies = new List<CookedSceneDependency>
            {
                Dependency(
                    ClusterGuids[0],
                    VegetationAssetTypes.Cluster,
                    VegetationClusterAssetCooker.RuntimeVariant),
                Dependency(
                    PageGuids[0],
                    VegetationAssetTypes.InstancePage,
                    VegetationInstancePageAssetCooker.RuntimeVariant),
                Dependency(
                    biomeGuid,
                    VegetationAssetTypes.Biome,
                    VegetationBiomeAssetCooker.RuntimeVariant),
                Dependency(
                    SpeciesGuid,
                    VegetationAssetTypes.Species,
                    VegetationSpeciesAssetCooker.RuntimeVariant),
                Dependency(
                    s_AlternateSpeciesGuid,
                    VegetationAssetTypes.Species,
                    VegetationSpeciesAssetCooker.RuntimeVariant),
                Dependency(s_MeshGuid, "Mesh", "staticmesh.uint32"),
                Dependency(s_MaterialGuid, "Material", "material.runtime"),
                Dependency(s_AlternateMeshGuid, "Mesh", "staticmesh.uint32"),
                Dependency(s_AlternateMaterialGuid, "Material", "material.runtime")
            };
            if (includeSecondPage)
            {
                dependencies.Add(Dependency(
                    PageGuids[1],
                    VegetationAssetTypes.InstancePage,
                    VegetationInstancePageAssetCooker.RuntimeVariant));
            }

            return dependencies.ToArray();
        }

        private void ReplaceCookedPayload(Guid guid, string variant, byte[] payload)
        {
            Assert.True(Database.TryGetCookedArtifact(guid, variant, out CookedAssetRecord artifact));
            string replacementPath = Path.Combine(
                m_Root,
                "CatalogReplacement",
                $"{guid:N}.{variant}.{Guid.NewGuid():N}.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(replacementPath)!);
            File.WriteAllBytes(replacementPath, payload);
            var replacement = new FileInfo(replacementPath);
            Database.ReplaceCookedArtifactForTest(new CookedAssetRecord(
                guid,
                artifact.AssetType,
                variant,
                replacement.FullName,
                replacement.Length,
                replacement.LastWriteTimeUtc));
        }

        private static WorldBounds ShiftBounds(WorldBounds bounds, double offset) => new(
            new WorldPosition(
                bounds.Min.X + offset,
                bounds.Min.Y,
                bounds.Min.Z),
            new WorldPosition(
                bounds.Max.X + offset,
                bounds.Max.Y,
                bounds.Max.Z));

        private static WorldBounds UnionBounds(IEnumerable<WorldBounds> bounds)
        {
            WorldBounds[] values = bounds.ToArray();
            Assert.NotEmpty(values);
            double minX = values.Min(value => value.Min.X);
            double minY = values.Min(value => value.Min.Y);
            double minZ = values.Min(value => value.Min.Z);
            double maxX = values.Max(value => value.Max.X);
            double maxY = values.Max(value => value.Max.Y);
            double maxZ = values.Max(value => value.Max.Z);
            return new WorldBounds(
                new WorldPosition(minX, minY, minZ),
                new WorldPosition(maxX, maxY, maxZ));
        }

        public void Dispose()
        {
            Scheduler.Dispose();
            try
            {
                if (Directory.Exists(m_Root)) Directory.Delete(m_Root, recursive: true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }

        private void CreateWorldStreamingFixture()
        {
            PersistentSceneGuid = Guid.Parse("ba100000-0000-0000-0000-000000000001");
            CellSceneGuid = Guid.Parse("ba100000-0000-0000-0000-000000000002");
            Guid worldGuid = Guid.Parse("ba100000-0000-0000-0000-000000000003");
            CellId = WorldCellIdentity.Create(
                worldGuid,
                new WorldCellCoordinate(0, 0, 0),
                "surface");
            WorldAsset = new AssetRef<WorldSourceAsset>(worldGuid, "World", PackageId);

            string persistentPath = Path.Combine(m_Root, "Sources", "Persistent.arisenscene");
            Directory.CreateDirectory(Path.GetDirectoryName(persistentPath)!);
            File.WriteAllText(persistentPath, """
                Version: 2
                Name: Persistent
                ComponentSchemas:
                - TypeId: 1
                  Name: Transform
                  Version: 1
                  Required: true
                Entities:
                - Guid: ba200000-0000-0000-0000-000000000001
                  Name: PersistentRoot
                  Transform:
                    Position: { X: 0, Y: 0, Z: 0 }
                    Rotation: { X: 0, Y: 0, Z: 0, W: 1 }
                    Scale: { X: 1, Y: 1, Z: 1 }
                """);
            Database.AddAsset(PersistentSceneGuid, "Scene", persistentPath, PackageId);

            string cellPath = Path.Combine(m_Root, "Sources", "VegetationCell.arisenscene");
            File.WriteAllText(cellPath, $$"""
                Version: 2
                Name: VegetationCell
                ComponentSchemas:
                - TypeId: 1
                  Name: Transform
                  Version: 1
                  Required: true
                - TypeId: {{VegetationClusterSceneComponentCodec.TypeId}}
                  Name: VegetationCluster
                  Version: 1
                  Required: true
                Entities:
                - Guid: ba200000-0000-0000-0000-000000000002
                  Name: VegetationCluster
                  Transform:
                    Position: { X: 0, Y: 0, Z: 0 }
                    Rotation: { X: 0, Y: 0, Z: 0, W: 1 }
                    Scale: { X: 1, Y: 1, Z: 1 }
                  VegetationCluster:
                    Cluster: { Guid: {{ClusterGuid:D}}, PackageId: {{PackageId}} }
                    Biome: { Guid: {{s_AllSpeciesBiomeGuid:D}}, PackageId: {{PackageId}} }
                    Species: { Guid: {{SpeciesGuid:D}}, PackageId: {{PackageId}} }
                    WorldGuid: {{worldGuid:D}}
                    OwningCellGuid: {{CellId.Value:D}}
                    Cell: { X: 0, Y: 0, Z: 0, Layer: surface }
                    Origin: { X: 0, Y: 0, Z: 0 }
                    Bounds:
                      Min: { X: {{ClusterBounds.Min.X:R}}, Y: {{ClusterBounds.Min.Y:R}}, Z: {{ClusterBounds.Min.Z:R}} }
                      Max: { X: {{ClusterBounds.Max.X:R}}, Y: {{ClusterBounds.Max.Y:R}}, Z: {{ClusterBounds.Max.Z:R}} }
                    Visible: true
                    CastShadows: true
                    ReceiveShadows: true
                    QualityGroup: 0
                    PageCount: 1
                    InstanceCount: 2
                """);
            Database.AddAsset(CellSceneGuid, "Scene", cellPath, PackageId);

            string worldPath = Path.Combine(m_Root, "Sources", "VegetationWorld.arisenworld");
            File.WriteAllText(worldPath, $$"""
                Version: 1
                WorldGuid: {{worldGuid:D}}
                Name: VegetationWorld
                PersistentScene:
                  Guid: {{PersistentSceneGuid:D}}
                  PackageId: {{PackageId}}
                Partition:
                  Origin: { X: 0, Y: 0, Z: 0 }
                  CellSize: { X: 100, Y: 100, Z: 100 }
                  LoadRadius: 0
                  UnloadHysteresis: 1
                  MaxActiveCells: 8
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
                    Guid: {{CellSceneGuid:D}}
                    PackageId: {{PackageId}}
                  Bounds:
                    Min: { X: 0, Y: 0, Z: 0 }
                    Max: { X: 100, Y: 100, Z: 100 }
                  EstimatedCpuBytes: 4096
                  EstimatedGpuBytes: 4096
                """);
            Database.AddAsset(worldGuid, "World", worldPath, PackageId);
            WorldAssetCooker.Cook(Database, WorldAsset);
        }

        private VegetationInstancePageCookDescriptor CreateReplacementPageDescriptor(
            int clusterIndex)
        {
            VegetationInstancePageCookDescriptor descriptor =
                CreateCluster(clusterIndex).Pages[0];
            VegetationCookedInstanceInput[] changedInstances = descriptor.Instances.ToArray();
            changedInstances[0] = changedInstances[0] with
            {
                LocalPosition = changedInstances[0].LocalPosition + new Vector3(7.0f, 0.0f, 0.0f)
            };
            return descriptor with
            {
                Guid = s_ReplacementPageGuid,
                Instances = Array.AsReadOnly(changedInstances)
            };
        }

        private VegetationClusterCookDescriptor CreateCluster(
            int index,
            Guid? biomeGuid = null)
        {
            var species = new CookedVegetationSpeciesReference(SpeciesGuid, PackageId);
            var origin = new WorldPosition(index * 256.0, 0.0, 0.0);
            var page = new VegetationInstancePageCookDescriptor(
                PageGuids[index],
                ClusterGuids[index],
                PackageId,
                VegetationInstancePageAssetCooker.CurrentGeneratedSchemaVersion,
                origin,
                Array.AsReadOnly([species]),
                Array.AsReadOnly<VegetationCookedInstanceInput>(
                [
                    new(
                        StableKey: 0x101UL,
                        SpeciesIndex: 0,
                        new Vector3(-2.0f, 0.0f, 1.0f),
                        Quaternion.Identity,
                        UniformScale: 1.0f,
                        ConservativeRadius: 1.5f),
                    new(
                        StableKey: 0x102UL,
                        SpeciesIndex: 0,
                        new Vector3(3.0f, 1.0f, -4.0f),
                        Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f),
                        UniformScale: 1.2f,
                        ConservativeRadius: 2.0f)
                ]));
            return new VegetationClusterCookDescriptor(
                ClusterGuids[index],
                PackageId,
                VegetationClusterAssetCooker.CurrentGeneratedSchemaVersion,
                new CookedVegetationBiomeReference(
                    biomeGuid ?? s_BiomeGuid,
                    PackageId),
                Array.AsReadOnly([page]));
        }

        private void AddAsset(Guid guid, string type, string fileName, string contents)
        {
            string path = Path.Combine(m_Root, "Sources", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
            Database.AddAsset(guid, type, path, PackageId);
        }

        private void RegisterRenderingArtifact(Guid guid, string type, string variant)
        {
            string path = Path.Combine(m_Root, "RenderingCooked", $"{guid:N}.{variant}.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, [0x41, 0x52, 0x49, 0x53, 0x45, 0x4e]);
            Database.RegisterCookedArtifact(new CookedAssetRecord(
                guid,
                type,
                variant,
                path,
                new FileInfo(path).Length,
                File.GetLastWriteTimeUtc(path)));
        }

        private static CookedSceneDependency Dependency(
            Guid guid,
            string assetType,
            string variant) => new(
                guid,
                PackageId,
                assetType,
                Required: true,
                Variant: variant);

        private static string SpeciesSource() => $$"""
            Version: 1
            SpeciesGuid: {{SpeciesGuid:D}}
            Name: Residency Test Species
            Lods:
            - Mesh: { Guid: {{s_MeshGuid:D}}, PackageId: {{PackageId}} }
              Material: { Guid: {{s_MaterialGuid:D}}, PackageId: {{PackageId}} }
              MaximumDistance: 120.0
              MaximumScreenError: 2.0
            ShadowPolicy: Cast
            ScaleRange: { Minimum: 0.8, Maximum: 1.2 }
            YawRangeDegrees: { Minimum: 0.0, Maximum: 360.0 }
            TiltRangeDegrees: { Minimum: -5.0, Maximum: 5.0 }
            CollisionPromotion:
              Mode: None
              CapsuleRadius: 0.0
              CapsuleHalfHeight: 0.0
              MaximumDistance: 0.0
            WindResponse: 0.4
            """;

        private static string AlternateSpeciesSource() => $$"""
            Version: 1
            SpeciesGuid: {{s_AlternateSpeciesGuid:D}}
            Name: Alternate Residency Test Species
            Lods:
            - Mesh: { Guid: {{s_AlternateMeshGuid:D}}, PackageId: {{PackageId}} }
              Material: { Guid: {{s_AlternateMaterialGuid:D}}, PackageId: {{PackageId}} }
              MaximumDistance: 140.0
              MaximumScreenError: 2.5
            ShadowPolicy: Cast
            ScaleRange: { Minimum: 0.9, Maximum: 1.3 }
            YawRangeDegrees: { Minimum: 0.0, Maximum: 360.0 }
            TiltRangeDegrees: { Minimum: -4.0, Maximum: 4.0 }
            CollisionPromotion:
              Mode: None
              CapsuleRadius: 0.0
              CapsuleHalfHeight: 0.0
              MaximumDistance: 0.0
            WindResponse: 0.35
            """;

        private static string BiomeSource() => $$"""
            Version: 1
            BiomeGuid: {{s_BiomeGuid:D}}
            Name: Residency Test Biome
            GlobalSeed: 1469598103934665603
            Entries:
            - EntryId: fixture
              Species: { Guid: {{SpeciesGuid:D}}, PackageId: {{PackageId}} }
              Density: 0.125
              SeedSalt: 29
              AltitudeRange: { Minimum: -500.0, Maximum: 4000.0 }
              SlopeRangeDegrees: { Minimum: 0.0, Maximum: 80.0 }
              LayerWeightRules: []
              MinimumSpacing: 1.5
              ClusterSize: 64
              ExclusionPolicy: Respect
            """;

        private static string AllSpeciesBiomeSource() => $$"""
            Version: 1
            BiomeGuid: {{s_AllSpeciesBiomeGuid:D}}
            Name: All Species Residency Test Biome
            GlobalSeed: 1469598103934665604
            Entries:
            - EntryId: alternate
              Species: { Guid: {{s_AlternateSpeciesGuid:D}}, PackageId: {{PackageId}} }
              Density: 0.0625
              SeedSalt: 31
              AltitudeRange: { Minimum: -500.0, Maximum: 4000.0 }
              SlopeRangeDegrees: { Minimum: 0.0, Maximum: 80.0 }
              LayerWeightRules: []
              MinimumSpacing: 2.0
              ClusterSize: 64
              ExclusionPolicy: Respect
            - EntryId: fixture
              Species: { Guid: {{SpeciesGuid:D}}, PackageId: {{PackageId}} }
              Density: 0.125
              SeedSalt: 29
              AltitudeRange: { Minimum: -500.0, Maximum: 4000.0 }
              SlopeRangeDegrees: { Minimum: 0.0, Maximum: 80.0 }
              LayerWeightRules: []
              MinimumSpacing: 1.5
              ClusterSize: 64
              ExclusionPolicy: Respect
            """;

        private static string AlternateOnlyBiomeSource() => $$"""
            Version: 1
            BiomeGuid: {{s_AlternateOnlyBiomeGuid:D}}
            Name: Alternate Only Residency Test Biome
            GlobalSeed: 1469598103934665605
            Entries:
            - EntryId: alternate
              Species: { Guid: {{s_AlternateSpeciesGuid:D}}, PackageId: {{PackageId}} }
              Density: 0.0625
              SeedSalt: 31
              AltitudeRange: { Minimum: -500.0, Maximum: 4000.0 }
              SlopeRangeDegrees: { Minimum: 0.0, Maximum: 80.0 }
              LayerWeightRules: []
              MinimumSpacing: 2.0
              ClusterSize: 64
              ExclusionPolicy: Respect
            """;
    }
}
