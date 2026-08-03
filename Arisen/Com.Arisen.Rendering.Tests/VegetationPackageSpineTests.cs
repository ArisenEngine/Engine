using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation;
using Xunit;

namespace ArisenEngine.Tests;

public sealed class VegetationPackageSpineTests
{
    [Fact]
    public void ClusterSnapshotRequiresRevisionForNonEmptyData()
    {
        Assert.Throws<ArgumentException>(
            () => new VegetationClusterDataSnapshot(0, 1, 4));

        var snapshot = new VegetationClusterDataSnapshot(7, 1, 4);

        Assert.False(snapshot.IsEmpty);
        Assert.Equal((ulong)7, snapshot.Revision);
        Assert.Equal(1, snapshot.ClusterCount);
        Assert.Equal(4, snapshot.InstanceCount);
    }

    [Fact]
    public void RuntimeDataStorePublishesAndClearsWholeSnapshots()
    {
        var store = new VegetationRuntimeDataStore();
        var published = new VegetationClusterDataSnapshot(3, 2, 64);

        store.Publish(published);

        Assert.Same(published, store.GetSnapshot());
        store.Clear();
        Assert.Same(VegetationClusterDataSnapshot.Empty, store.GetSnapshot());
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
}
