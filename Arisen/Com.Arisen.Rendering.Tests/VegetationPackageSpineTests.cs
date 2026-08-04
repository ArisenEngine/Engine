using System.Numerics;
using System.Security.Cryptography;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation;
using ArisenEngine.Vegetation.Assets;
using Xunit;

namespace ArisenEngine.Tests;

public sealed class VegetationPackageSpineTests
{
    private const string PackageId = "com.arisen.tests.vegetation";
    private static readonly Guid s_WorldGuid =
        Guid.Parse("9a000000-0000-0000-0000-000000000001");
    private static readonly Guid s_CellGuid =
        Guid.Parse("9a000000-0000-0000-0000-000000000002");
    private static readonly Guid s_BiomeGuid =
        Guid.Parse("9a000000-0000-0000-0000-000000000003");
    private static readonly Guid s_SpeciesGuid =
        Guid.Parse("9a000000-0000-0000-0000-000000000004");
    private static readonly Guid s_ClusterGuid =
        Guid.Parse("9a000000-0000-0000-0000-000000000005");
    private static readonly Guid s_PageGuid =
        Guid.Parse("9a000000-0000-0000-0000-000000000006");
    private static readonly WorldPosition s_Origin = new(100.0, 10.0, 200.0);
    private static readonly WorldBounds s_Bounds = new(
        new WorldPosition(99.5, 9.5, 199.5),
        new WorldPosition(103.5, 10.5, 201.5));

    [Fact]
    public void ClusterSnapshotCountsAreDerivedFromImmutableViews()
    {
        Assert.Empty(typeof(VegetationClusterDataSnapshot).GetConstructors());
        Assert.Equal(0, VegetationClusterDataSnapshot.Empty.ClusterCount);
        Assert.Equal(0, VegetationClusterDataSnapshot.Empty.PageCount);
        Assert.Equal(0, VegetationClusterDataSnapshot.Empty.InstanceCount);
        Assert.True(VegetationClusterDataSnapshot.Empty.Clusters.IsEmpty);
    }

    [Fact]
    public void RuntimeDataStorePublishesCompleteClosureAndProtectsReplacementGeneration()
    {
        var store = new VegetationRuntimeDataStore();
        CookedVegetationInstancePage page = CreatePage();
        CookedVegetationCluster cluster = CreateCluster(page);

        VegetationResidentResourceHandle clusterHandle = store.PublishCluster(cluster);

        Assert.True(clusterHandle.IsValid);
        Assert.True(store.GetSnapshot().IsEmpty);
        Assert.Equal(
            new VegetationRuntimeDataMetrics(1, 0, 0, 0, 0),
            store.GetMetrics());

        VegetationResidentResourceHandle firstPage = store.PublishPage(page);
        VegetationClusterDataSnapshot complete = store.GetSnapshot();

        Assert.False(complete.IsEmpty);
        Assert.Equal(1, complete.ClusterCount);
        Assert.Equal(1, complete.PageCount);
        Assert.Equal(page.Instances.Count, complete.InstanceCount);
        Assert.True(typeof(VegetationResidentClusterData).IsPublic);
        Assert.True(typeof(VegetationResidentPageData).IsPublic);
        Assert.True(typeof(VegetationResidentInstance).IsPublic);
        ReadOnlySpan<VegetationResidentClusterData> clusters = complete.Clusters;
        Assert.Equal(1, clusters.Length);
        Assert.Equal(s_ClusterGuid, clusters[0].Guid);
        ReadOnlySpan<VegetationResidentPageData> pages = clusters[0].Pages;
        Assert.Equal(1, pages.Length);
        Assert.Equal(s_PageGuid, pages[0].Guid);
        Assert.Equal(page.Instances.Count, pages[0].Instances.Length);
        Assert.Equal(
            new VegetationRuntimeDataMetrics(1, 1, 1, 1, page.Instances.Count),
            store.GetMetrics());

        CookedVegetationInstancePage alteredPage = CreatePage(firstLocalX: 1.5f);
        InvalidOperationException alteredError = Assert.Throws<InvalidOperationException>(
            () => store.PublishPage(alteredPage));
        Assert.Contains("does not match", alteredError.Message, StringComparison.Ordinal);
        Assert.Same(complete, store.GetSnapshot());
        Assert.Equal(firstPage.Generation, store.GetSnapshot().Clusters[0].Pages[0].Generation);

        VegetationResidentResourceHandle replacementPage = store.PublishPage(page);
        Assert.True(replacementPage.Generation > firstPage.Generation);
        Assert.False(store.Remove(firstPage));
        Assert.Equal(replacementPage.Generation, store.GetSnapshot().Clusters[0].Pages[0].Generation);
        Assert.True(store.Remove(replacementPage));
        Assert.True(store.GetSnapshot().IsEmpty);
        Assert.True(store.Remove(clusterHandle));
        Assert.Equal(
            new VegetationRuntimeDataMetrics(0, 0, 0, 0, 0),
            store.GetMetrics());

        store.Clear();
        Assert.Same(VegetationClusterDataSnapshot.Empty, store.GetSnapshot());
    }

    [Fact]
    public void RuntimeInstanceIdentityMatchesCanonicalCustomVersionEightFixture()
    {
        Guid identity = VegetationRuntimeInstanceIdentity.Create(
            s_ClusterGuid,
            s_PageGuid,
            stableKey: 10);

        Assert.Equal(
            Guid.Parse("8addfb8d-db69-85d2-9e79-960cde56b9cc"),
            identity);
        Span<byte> bytes = stackalloc byte[16];
        Assert.True(identity.TryWriteBytes(bytes, bigEndian: true, out int written));
        Assert.Equal(16, written);
        Assert.Equal(8, bytes[6] >> 4);
        Assert.Equal(2, bytes[8] >> 6);
        Assert.NotEqual(
            identity,
            VegetationRuntimeInstanceIdentity.Create(s_WorldGuid, s_PageGuid, 10));
        Assert.NotEqual(
            identity,
            VegetationRuntimeInstanceIdentity.Create(s_ClusterGuid, s_CellGuid, 10));
        Assert.NotEqual(
            identity,
            VegetationRuntimeInstanceIdentity.Create(s_ClusterGuid, s_PageGuid, 11));
    }

    [Fact]
    public void QueryRequiresMatchingActiveClusterAndReturnsNearestDeterministically()
    {
        var store = new VegetationRuntimeDataStore();
        CookedVegetationInstancePage page = CreatePage();
        CookedVegetationCluster cluster = CreateCluster(page);
        VegetationResidentResourceHandle clusterHandle = store.PublishCluster(cluster);
        store.PublishPage(page);
        var world = new EntityManager();
        var query = new VegetationQueryService(store, () => world);
        var results = new VegetationInstanceQueryResult[2];
        var request = new VegetationQueryRequest(s_Origin, Radius: 10.0, MaximumResults: 2);

        Assert.Equal(
            VegetationQueryStatus.Unavailable,
            query.QueryNearby(request, results, out int inactiveCount));
        Assert.Equal(0, inactiveCount);

        Entity entity = world.CreateEntity();
        world.AddComponent(entity, CreateComponent());
        VegetationQueryStatus status = query.QueryNearby(
            request,
            results,
            out int resultCount);

        Assert.Equal(VegetationQueryStatus.Available, status);
        Assert.Equal(2, resultCount);
        Guid[] expectedNearest =
        [
            VegetationRuntimeInstanceIdentity.Create(s_ClusterGuid, s_PageGuid, 10),
            VegetationRuntimeInstanceIdentity.Create(s_ClusterGuid, s_PageGuid, 20)
        ];
        Array.Sort(expectedNearest);
        Assert.Equal(expectedNearest[0], results[0].InstanceGuid);
        Assert.Equal(expectedNearest[1], results[1].InstanceGuid);
        Assert.Equal(1.0, results[0].DistanceSquared);
        Assert.Equal(1.0, results[1].DistanceSquared);
        Assert.All(results, result => Assert.Equal(clusterHandle.Generation, result.ClusterGeneration));

        VegetationQueryStatus outside = query.QueryNearby(
            request with { Center = new WorldPosition(1000.0, 0.0, 1000.0) },
            results,
            out int outsideCount);
        Assert.Equal(VegetationQueryStatus.OutsideCoverage, outside);
        Assert.Equal(0, outsideCount);

        world.DestroyEntity(entity);
        Assert.Equal(
            VegetationQueryStatus.Unavailable,
            query.QueryNearby(request, results, out int unloadedCount));
        Assert.Equal(0, unloadedCount);
    }

    [Fact]
    public void QueryRejectsMismatchedOwnershipAndHonorsZeroResultBound()
    {
        var store = new VegetationRuntimeDataStore();
        CookedVegetationInstancePage page = CreatePage();
        store.PublishCluster(CreateCluster(page));
        store.PublishPage(page);
        var world = new EntityManager();
        Entity entity = world.CreateEntity();
        VegetationClusterComponent component = CreateComponent();
        component.OwningCellGuid = Guid.Empty;
        world.AddComponent(entity, component);
        var query = new VegetationQueryService(store, () => world);
        Span<VegetationInstanceQueryResult> noResults = [];

        Assert.Equal(
            VegetationQueryStatus.Unavailable,
            query.QueryNearby(
                new VegetationQueryRequest(s_Origin, 10.0, 0),
                noResults,
                out int mismatchedCount));
        Assert.Equal(0, mismatchedCount);

        world.DestroyEntity(entity);
        entity = world.CreateEntity();
        world.AddComponent(entity, CreateComponent());
        Assert.Equal(
            VegetationQueryStatus.Available,
            query.QueryNearby(
                new VegetationQueryRequest(s_Origin, 10.0, 0),
                noResults,
                out int boundedCount));
        Assert.Equal(0, boundedCount);
    }

    [Fact]
    public void QueryReportsInvalidAndUnavailableStatesWithoutLoading()
    {
        var store = new VegetationRuntimeDataStore();
        var query = new VegetationQueryService(store);
        var results = new VegetationInstanceQueryResult[2];

        VegetationQueryStatus invalid = query.QueryNearby(
            new VegetationQueryRequest(
                new WorldPosition(double.NaN, 0.0, 0.0),
                10.0,
                1),
            results,
            out int invalidCount);
        VegetationQueryStatus unavailable = query.QueryNearby(
            new VegetationQueryRequest(
                new WorldPosition(0.0, 0.0, 0.0),
                10.0,
                2),
            results,
            out int unavailableCount);

        Assert.Equal(VegetationQueryStatus.InvalidRequest, invalid);
        Assert.Equal(0, invalidCount);
        Assert.Equal(VegetationQueryStatus.Unavailable, unavailable);
        Assert.Equal(0, unavailableCount);
    }

    [Fact]
    public void DiagnosticsAndPreviewPublicationUseImmutableSnapshots()
    {
        var diagnostics = new VegetationDiagnosticsService();
        var diagnosticSnapshot = new VegetationDiagnosticsSnapshot(
            12,
            3,
            2,
            64,
            1,
            32,
            1,
            4096,
            8192);
        diagnostics.Publish(diagnosticSnapshot);

        Assert.Same(diagnosticSnapshot, diagnostics.GetSnapshot());

        var previews = new VegetationAuthoringPreviewService();
        var previewSnapshot = new VegetationAuthoringPreviewSnapshot(
            Guid.Parse("899210c5-93bb-4e7b-aa65-61983bb9cc59"),
            5,
            VegetationAuthoringPreviewState.Ready,
            2,
            64,
            string.Empty);
        previews.Publish(previewSnapshot);

        Assert.Same(previewSnapshot, previews.GetSnapshot());
        diagnostics.Clear();
        previews.Clear();
        Assert.Same(VegetationDiagnosticsSnapshot.Empty, diagnostics.GetSnapshot());
        Assert.Same(VegetationAuthoringPreviewSnapshot.Empty, previews.GetSnapshot());
    }

    private static CookedVegetationInstancePage CreatePage(float firstLocalX = 1.0f)
    {
        var species = new CookedVegetationSpeciesReference(s_SpeciesGuid, PackageId);
        return new CookedVegetationInstancePage(
            s_PageGuid,
            s_ClusterGuid,
            PackageId,
            GeneratedSchemaVersion: 1,
            s_Origin,
            s_Bounds,
            [species],
            [
                CreateInstance(10, new Vector3(firstLocalX, 0.0f, 0.0f)),
                CreateInstance(20, new Vector3(0.0f, 0.0f, 1.0f)),
                CreateInstance(30, new Vector3(2.0f, 0.0f, 0.0f)),
                CreateInstance(40, new Vector3(3.0f, 0.0f, 0.0f))
            ]);
    }

    private static CookedVegetationCluster CreateCluster(CookedVegetationInstancePage page)
    {
        var species = new CookedVegetationSpeciesReference(s_SpeciesGuid, PackageId);
        byte[] payload = VegetationInstancePageAssetCooker.WritePayload(page);
        return new CookedVegetationCluster(
            s_ClusterGuid,
            PackageId,
            GeneratedSchemaVersion: 1,
            new CookedVegetationBiomeReference(s_BiomeGuid, PackageId),
            s_Bounds,
            [species],
            [
                new CookedVegetationInstancePageReference(
                    s_PageGuid,
                    PackageId,
                    page.Instances.Count,
                    s_Origin,
                    s_Bounds,
                    payload.LongLength,
                    SHA256.HashData(payload))
            ],
            page.Instances.Count);
    }

    private static CookedVegetationInstance CreateInstance(
        ulong stableKey,
        Vector3 localPosition) =>
        new(
            stableKey,
            SpeciesIndex: 0,
            localPosition,
            Quaternion.Identity,
            UniformScale: 1.0f,
            ConservativeRadius: 0.5f);

    private static VegetationClusterComponent CreateComponent() => new()
    {
        ClusterGuid = s_ClusterGuid,
        BiomeGuid = s_BiomeGuid,
        SpeciesGuid = s_SpeciesGuid,
        WorldGuid = s_WorldGuid,
        OwningCellGuid = s_CellGuid,
        CellX = 1,
        CellY = 0,
        CellZ = 2,
        OriginX = s_Origin.X,
        OriginY = s_Origin.Y,
        OriginZ = s_Origin.Z,
        BoundsMinX = s_Bounds.Min.X,
        BoundsMinY = s_Bounds.Min.Y,
        BoundsMinZ = s_Bounds.Min.Z,
        BoundsMaxX = s_Bounds.Max.X,
        BoundsMaxY = s_Bounds.Max.Y,
        BoundsMaxZ = s_Bounds.Max.Z,
        Flags = VegetationClusterFlags.Visible,
        QualityGroup = 0,
        PageCount = 1,
        InstanceCount = 4
    };
}
