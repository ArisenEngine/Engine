using ArisenEditor.Core.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class AssetImportSchedulerTests
{
    [Fact]
    public async Task RepeatedImportWorkerCyclesDrainToStableBaseline()
    {
        const int cycleCount = 8;
        const int requestsPerCycle = 32;

        for (int cycle = 0; cycle < cycleCount; cycle++)
        {
            var processed = new List<AssetImportRequest>();
            var scheduler = new AssetImportScheduler(
                request =>
                {
                    processed.Add(request);
                    return true;
                },
                debounceDelay: TimeSpan.Zero,
                retryDelay: TimeSpan.Zero,
                maxAttempts: 1);

            try
            {
                for (int request = 0; request < requestsPerCycle; request++)
                {
                    Assert.True(scheduler.Enqueue(new AssetImportRequest(
                        AssetImportWorkKind.Changed,
                        Path.Combine(
                            Path.GetTempPath(),
                            $"arisen-reimport-cycle-{cycle:D2}-{request:D2}.asset"))));
                }

                await scheduler.WaitForIdleAsync();

                Assert.Equal(requestsPerCycle, processed.Count);
                Assert.Equal(requestsPerCycle, processed.Select(item => item.FullPath).Distinct().Count());
                Assert.Empty(scheduler.TerminalFailures);
                Assert.Equal(AssetImportSchedulerState.Accepting, scheduler.State);

                scheduler.RequestStop();
                await scheduler.Completion;
                Assert.Equal(AssetImportSchedulerState.Completed, scheduler.State);

                scheduler.Dispose();
                Assert.Equal(AssetImportSchedulerState.Disposed, scheduler.State);
            }
            finally
            {
                scheduler.Dispose();
            }
        }
    }

    [Fact]
    public async Task StopRetainsWorkerOwnershipUntilActiveImportCompletes()
    {
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduler = new AssetImportScheduler(
            _ =>
            {
                entered.TrySetResult(true);
                release.Task.GetAwaiter().GetResult();
                return true;
            },
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.Zero,
            maxAttempts: 1);

        try
        {
            Assert.True(scheduler.Enqueue(new AssetImportRequest(
                AssetImportWorkKind.Changed,
                Path.Combine(Path.GetTempPath(), "active.asset"))));
            await entered.Task;

            scheduler.RequestStop();

            Assert.Equal(AssetImportSchedulerState.StopRequested, scheduler.State);
            Assert.False(scheduler.Completion.IsCompleted);
            Assert.False(scheduler.Enqueue(new AssetImportRequest(
                AssetImportWorkKind.Created,
                Path.Combine(Path.GetTempPath(), "rejected.asset"))));

            release.TrySetResult(true);
            await scheduler.Completion;
            Assert.Equal(AssetImportSchedulerState.Completed, scheduler.State);

            scheduler.Dispose();
            Assert.Equal(AssetImportSchedulerState.Disposed, scheduler.State);
        }
        finally
        {
            release.TrySetResult(true);
            scheduler.Dispose();
        }
    }

    [Fact]
    public async Task RetryExhaustionRetainsAndReportsTerminalFailure()
    {
        int attempts = 0;
        var observed = new List<AssetImportFailure>();
        using var scheduler = new AssetImportScheduler(
            _ =>
            {
                attempts++;
                return false;
            },
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.Zero,
            maxAttempts: 3);
        scheduler.ImportFailed += observed.Add;
        var request = new AssetImportRequest(
            AssetImportWorkKind.Changed,
            Path.Combine(Path.GetTempPath(), "terminal.asset"));

        Assert.True(scheduler.Enqueue(request));
        await scheduler.WaitForIdleAsync();

        Assert.Equal(3, attempts);
        AssetImportFailure failure = Assert.Single(scheduler.TerminalFailures);
        Assert.Equal(request with { FullPath = Path.GetFullPath(request.FullPath) }, failure.Request);
        Assert.Equal(3, failure.Attempts);
        Assert.Contains("retryable failure", failure.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Same(failure, Assert.Single(observed));
    }

    [Fact]
    public async Task DisposeReleasesOwnershipAfterWorkerCompletionFaults()
    {
        var scheduler = new AssetImportScheduler(
            _ => true,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.Zero,
            maxAttempts: 1)
        {
            BeforeWorkerCompletion = () =>
                throw new InvalidOperationException("Injected worker completion failure.")
        };

        scheduler.RequestStop();
        InvalidOperationException workerError = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await scheduler.Completion);
        Assert.Contains("Injected worker completion failure", workerError.Message);
        Assert.Equal(AssetImportSchedulerState.Completed, scheduler.State);

        AggregateException disposeError = Assert.Throws<AggregateException>(scheduler.Dispose);
        Assert.Contains(
            disposeError.Flatten().InnerExceptions,
            error => error.Message.Contains(
                "Injected worker completion failure",
                StringComparison.Ordinal));
        Assert.Equal(AssetImportSchedulerState.Disposed, scheduler.State);
        scheduler.Dispose();
    }

}
