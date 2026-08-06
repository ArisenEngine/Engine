using ArisenEngine.Rendering;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class GenericPreparedAssetProviderLifecycleTests
{
    [Fact]
    public async Task CallerThreadReleaseImmediatelyTombstonesAndPublishesMetrics()
    {
        var state = new GenericPreparedAssetProviderLifecycleState();
        var key = new RuntimeAssetResidencyKey(
            Guid.Parse("5f6ab5ea-b661-4a2c-bad9-2db739b6be31"),
            "com.arisen.test",
            "Mesh",
            RuntimeAssetVariantPolicy.StaticMesh);
        state.PublishPhysicalMetrics(new RuntimePreparedAssetProviderMetrics(
            PreparedResourceCount: 1,
            EstimatedGpuBytes: 4096,
            PendingDisposalCount: 0,
            DescriptorCount: 0));

        Assert.True(await Task.Run(() => state.RequestRelease(key)));
        Assert.False(await Task.Run(() => state.RequestRelease(key)));
        Assert.True(state.IsReleasePending(key));
        Assert.Equal(1, state.PendingReleaseCount);

        RuntimePreparedAssetProviderMetrics pendingMetrics =
            await Task.Run(state.ReadMetrics);
        Assert.Equal(1, pendingMetrics.PreparedResourceCount);
        Assert.Equal(4096, pendingMetrics.EstimatedGpuBytes);
        Assert.Equal(1, pendingMetrics.PendingDisposalCount);

        Assert.True(state.TryPeekPendingRelease(out RuntimeAssetResidencyKey pendingKey));
        Assert.Equal(key, pendingKey);
        state.CompletePendingRelease(
            pendingKey,
            new RuntimePreparedAssetProviderMetrics(
                PreparedResourceCount: 0,
                EstimatedGpuBytes: 0,
                PendingDisposalCount: 1,
                DescriptorCount: 0));

        Assert.False(state.IsReleasePending(key));
        Assert.Equal(0, state.PendingReleaseCount);
        RuntimePreparedAssetProviderMetrics retiredMetrics =
            await Task.Run(state.ReadMetrics);
        Assert.Equal(0, retiredMetrics.PreparedResourceCount);
        Assert.Equal(0, retiredMetrics.EstimatedGpuBytes);
        Assert.Equal(1, retiredMetrics.PendingDisposalCount);
    }

    [Fact]
    public void EnvironmentRetirementRetriesOnlyTheUntransferredResource()
    {
        var lighting = new TrackingDisposable();
        var texture = new TrackingDisposable();
        var retirement = new GenericPreparedEnvironmentRetirementState(
            lighting,
            texture);
        int lightingTransfers = 0;
        int textureTransfers = 0;
        bool rejectFirstTextureTransfer = true;

        void Transfer(IDisposable resource)
        {
            if (ReferenceEquals(resource, lighting))
            {
                lightingTransfers++;
                return;
            }

            Assert.Same(texture, resource);
            if (rejectFirstTextureTransfer)
            {
                rejectFirstTextureTransfer = false;
                throw new InvalidOperationException("Injected texture retirement failure.");
            }

            textureTransfers++;
        }

        Assert.True(retirement.IsCurrent);
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => retirement.TransferOwnership(Transfer));
        Assert.Contains("Injected", failure.Message, StringComparison.Ordinal);
        Assert.False(retirement.IsCurrent);
        Assert.False(retirement.IsComplete);
        Assert.Equal(1, lightingTransfers);
        Assert.Equal(0, textureTransfers);

        retirement.TransferOwnership(Transfer);
        retirement.TransferOwnership(Transfer);

        Assert.True(retirement.IsComplete);
        Assert.Equal(1, lightingTransfers);
        Assert.Equal(1, textureTransfers);
    }

    [Fact]
    public async Task MetricsPublicationRemainsCoherentDuringConcurrentReleaseRequests()
    {
        var state = new GenericPreparedAssetProviderLifecycleState();
        RuntimeAssetResidencyKey[] keys = Enumerable.Range(0, 256)
            .Select(index => new RuntimeAssetResidencyKey(
                CreateGuid(index),
                "com.arisen.test",
                "Mesh",
                RuntimeAssetVariantPolicy.StaticMesh))
            .ToArray();
        var firstMetrics = new RuntimePreparedAssetProviderMetrics(
            PreparedResourceCount: 1,
            EstimatedGpuBytes: 101,
            PendingDisposalCount: 3,
            DescriptorCount: 11);
        var secondMetrics = new RuntimePreparedAssetProviderMetrics(
            PreparedResourceCount: 2,
            EstimatedGpuBytes: 202,
            PendingDisposalCount: 7,
            DescriptorCount: 22);
        state.PublishPhysicalMetrics(firstMetrics);
        using var start = new Barrier(4);

        Task publisher = Task.Run(() =>
        {
            start.SignalAndWait();
            for (int index = 0; index < 4096; index++)
            {
                state.PublishPhysicalMetrics(
                    (index & 1) == 0 ? firstMetrics : secondMetrics);
            }
        });
        Task releaser = Task.Run(() =>
        {
            start.SignalAndWait();
            for (int index = 0; index < keys.Length; index++)
            {
                Assert.True(state.RequestRelease(keys[index]));
            }
        });
        Task reader = Task.Run(() =>
        {
            start.SignalAndWait();
            for (int index = 0; index < 4096; index++)
            {
                RuntimePreparedAssetProviderMetrics metrics = state.ReadMetrics();
                Assert.InRange(
                    metrics.PendingDisposalCount,
                    firstMetrics.PendingDisposalCount,
                    secondMetrics.PendingDisposalCount + keys.Length);
                Assert.True(
                    (metrics.PreparedResourceCount == firstMetrics.PreparedResourceCount &&
                     metrics.EstimatedGpuBytes == firstMetrics.EstimatedGpuBytes &&
                     metrics.DescriptorCount == firstMetrics.DescriptorCount) ||
                    (metrics.PreparedResourceCount == secondMetrics.PreparedResourceCount &&
                     metrics.EstimatedGpuBytes == secondMetrics.EstimatedGpuBytes &&
                     metrics.DescriptorCount == secondMetrics.DescriptorCount));
            }
        });

        start.SignalAndWait();
        await Task.WhenAll(publisher, releaser, reader);

        Assert.Equal(keys.Length, state.PendingReleaseCount);
        RuntimePreparedAssetProviderMetrics final = state.ReadMetrics();
        Assert.InRange(
            final.PendingDisposalCount,
            firstMetrics.PendingDisposalCount + keys.Length,
            secondMetrics.PendingDisposalCount + keys.Length);
    }

    [Fact]
    public void WarmedMetricsReadsAllocateNoManagedMemory()
    {
        var state = new GenericPreparedAssetProviderLifecycleState();
        state.PublishPhysicalMetrics(new RuntimePreparedAssetProviderMetrics(
            PreparedResourceCount: 3,
            EstimatedGpuBytes: 8192,
            PendingDisposalCount: 2,
            DescriptorCount: 5));
        for (int index = 0; index < 256; index++)
        {
            _ = state.ReadMetrics();
        }

        long checksum = 0;
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 4096; index++)
        {
            RuntimePreparedAssetProviderMetrics metrics = state.ReadMetrics();
            checksum += metrics.EstimatedGpuBytes;
        }
        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, allocatedAfter - allocatedBefore);
        Assert.Equal(4096L * 8192, checksum);
    }

    private static Guid CreateGuid(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value + 1);
        return new Guid(bytes);
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
