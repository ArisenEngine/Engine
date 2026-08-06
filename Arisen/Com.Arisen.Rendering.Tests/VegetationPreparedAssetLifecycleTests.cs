using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation;
using ArisenEngine.Vegetation.Assets;
using ArisenEngine.Vegetation.GenericRenderPipeline;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationPreparedAssetLifecycleTests
{
    [Fact]
    public async Task CallerThreadTombstoneIsImmediateAndMetricsAreAtomic()
    {
        var state = new VegetationPreparedAssetProviderLifecycleState();
        RuntimeAssetResidencyKey key = CreateKey();
        state.PublishPhysicalMetrics(new RuntimePreparedAssetProviderMetrics(
            PreparedResourceCount: 1,
            EstimatedGpuBytes: 4096,
            PendingDisposalCount: 0,
            DescriptorCount: 1));

        Assert.True(await Task.Run(() =>
        {
            lock (state.Gate)
            {
                return state.RequestReleaseLocked(key);
            }
        }));

        Assert.True(state.IsReleasePending(key));
        Assert.Equal(
            new RuntimePreparedAssetProviderMetrics(1, 4096, 1, 1),
            state.ReadMetrics());

        lock (state.Gate)
        {
            state.CompleteReleaseLocked(
                key,
                new RuntimePreparedAssetProviderMetrics(
                    PreparedResourceCount: 0,
                    EstimatedGpuBytes: 0,
                    PendingDisposalCount: 1,
                    DescriptorCount: 0));
        }

        Assert.False(state.IsReleasePending(key));
        Assert.Equal(
            new RuntimePreparedAssetProviderMetrics(0, 0, 1, 0),
            state.ReadMetrics());
    }

    [Fact]
    public void WarmMetricsReadsAllocateNothing()
    {
        var state = new VegetationPreparedAssetProviderLifecycleState();
        state.PublishPhysicalMetrics(new RuntimePreparedAssetProviderMetrics(
            PreparedResourceCount: 3,
            EstimatedGpuBytes: 12288,
            PendingDisposalCount: 2,
            DescriptorCount: 1));
        _ = state.ReadMetrics();

        long before = GC.GetAllocatedBytesForCurrentThread();
        RuntimePreparedAssetProviderMetrics observed = default;
        for (int iteration = 0; iteration < 4096; iteration++)
        {
            observed = state.ReadMetrics();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(3, observed.PreparedResourceCount);
    }

    [Fact]
    public async Task GpuRetirementTransfersOnlyWhenSetupThreadDrains()
    {
        var state = new VegetationGpuResourceRetirementState();
        var resource = new TestGpuResource();
        int requestThread = 0;

        Assert.True(await Task.Run(() =>
        {
            requestThread = Environment.CurrentManagedThreadId;
            return state.RequestRelease(resource);
        }));

        Assert.Equal(1, state.PendingCount);
        Assert.False(resource.Disposed);
        Assert.False(state.RequestRelease(resource));
        int retirementThread = 0;
        state.Drain(pending =>
        {
            Assert.Same(resource, pending);
            retirementThread = Environment.CurrentManagedThreadId;
        });

        Assert.NotEqual(requestThread, retirementThread);
        Assert.Equal(0, state.PendingCount);
        Assert.False(resource.Disposed);
    }

    [Fact]
    public void FailedGpuRetirementRemainsFifoOwnedForExactRetry()
    {
        var state = new VegetationGpuResourceRetirementState();
        var first = new TestGpuResource();
        var second = new TestGpuResource();
        Assert.True(state.RequestRelease(first));
        Assert.True(state.RequestRelease(second));
        int firstAttempts = 0;
        var retired = new List<IVegetationClusterGpuResource>();

        Assert.Throws<InvalidOperationException>(() => state.Drain(resource =>
        {
            firstAttempts++;
            throw new InvalidOperationException("Injected retirement failure.");
        }));

        Assert.Equal(1, firstAttempts);
        Assert.Equal(2, state.PendingCount);
        state.Drain(retired.Add);

        Assert.Equal(new[] { first, second }, retired);
        Assert.Equal(0, state.PendingCount);
    }

    private static RuntimeAssetResidencyKey CreateKey() => new(
        Guid.Parse("e90ae5ab-24fb-2617-9983-3ed656bd652c"),
        "com.arisen.packagegame",
        VegetationAssetTypes.Cluster,
        VegetationClusterAssetCooker.RuntimeVariant);

    private sealed class TestGpuResource : IVegetationClusterGpuResource
    {
        public long EstimatedGpuBytes => 4096;

        public bool DependenciesCurrent => true;

        public bool Disposed { get; private set; }

        public VegetationPreparedClusterView CreateView(ulong generation) => default;

        public void Dispose()
        {
            Assert.False(Disposed);
            Disposed = true;
        }
    }
}
