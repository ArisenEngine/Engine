using ArisenEngine.Core.Diagnostics;
using Xunit;

namespace ArisenEngine.Tests;

public sealed class OrderedNotificationDispatcherTests
{
    [Fact]
    public void StopRetainsWorkerUntilBlockedDispatchCompletesAndRejectsLateAdmission()
    {
        using var dispatchEntered = new ManualResetEventSlim(false);
        using var releaseDispatch = new ManualResetEventSlim(false);
        using var disposeEntered = new ManualResetEventSlim(false);
        using var disposeReturned = new ManualResetEventSlim(false);
        var processed = new List<int>();
        var dispatcher = new OrderedNotificationDispatcher<int>(
            "Ordered notification drain test",
            4,
            value =>
            {
                if (value == 1)
                {
                    dispatchEntered.Set();
                    releaseDispatch.Wait();
                }

                lock (processed)
                {
                    processed.Add(value);
                }
            });

        Assert.Equal(NotificationPostResult.Accepted, dispatcher.Post(1));
        Assert.True(dispatchEntered.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(NotificationPostResult.Accepted, dispatcher.Post(2));

        NotificationDispatcherSnapshot stopping = dispatcher.RequestStop();
        Assert.Equal(NotificationDispatcherState.StopRequested, stopping.State);
        Assert.True(stopping.OwnsDispatchTarget);
        Assert.Equal(NotificationPostResult.Stopped, dispatcher.Post(3));

        Exception? disposalFailure = null;
        var disposer = new Thread(() =>
        {
            disposeEntered.Set();
            try
            {
                dispatcher.Dispose();
            }
            catch (Exception error)
            {
                disposalFailure = error;
            }
            finally
            {
                disposeReturned.Set();
            }
        });
        disposer.Start();

        Assert.True(disposeEntered.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(disposeReturned.IsSet);
        releaseDispatch.Set();
        Assert.True(disposeReturned.Wait(TimeSpan.FromSeconds(5)));
        disposer.Join();

        Assert.Null(disposalFailure);
        Assert.Equal(new[] { 1, 2 }, processed);
        NotificationDispatcherSnapshot terminal = dispatcher.GetSnapshot();
        Assert.Equal(NotificationDispatcherState.Disposed, terminal.State);
        Assert.Equal(2, terminal.AcceptedCount);
        Assert.Equal(2, terminal.ProcessedCount);
        Assert.Equal(1, terminal.RejectedCount);
        Assert.Equal(0, terminal.DroppedCount);
        Assert.False(terminal.OwnsDispatchTarget);
    }

    [Fact]
    public void OverflowAndDispatchFailureRemainAttributableAfterCompleteDrain()
    {
        using var dispatchEntered = new ManualResetEventSlim(false);
        using var releaseDispatch = new ManualResetEventSlim(false);
        var processed = new List<int>();
        var dispatcher = new OrderedNotificationDispatcher<int>(
            "Ordered notification failure test",
            1,
            value =>
            {
                lock (processed)
                {
                    processed.Add(value);
                }

                if (value == 1)
                {
                    dispatchEntered.Set();
                    releaseDispatch.Wait();
                    throw new InvalidOperationException("subscriber failure");
                }
            });

        Assert.Equal(NotificationPostResult.Accepted, dispatcher.Post(1));
        Assert.True(dispatchEntered.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(NotificationPostResult.Accepted, dispatcher.Post(2));
        Assert.Equal(NotificationPostResult.Full, dispatcher.Post(3));
        dispatcher.RequestStop();
        releaseDispatch.Set();

        AggregateException firstFailure = Assert.Throws<AggregateException>(dispatcher.Dispose);
        Assert.Contains(
            firstFailure.Flatten().InnerExceptions,
            error => error.Message.Contains("Notification #1", StringComparison.Ordinal));
        Assert.Contains(
            firstFailure.Flatten().InnerExceptions,
            error => error.Message.Contains("dropped 1 notification", StringComparison.Ordinal));
        Assert.Equal(new[] { 1, 2 }, processed);

        NotificationDispatcherSnapshot terminal = dispatcher.GetSnapshot();
        Assert.Equal(NotificationDispatcherState.Faulted, terminal.State);
        Assert.Equal(2, terminal.ProcessedCount);
        Assert.Equal(1, terminal.DroppedCount);
        Assert.False(terminal.OwnsDispatchTarget);

        AggregateException repeatedFailure = Assert.Throws<AggregateException>(dispatcher.Dispose);
        Assert.Same(firstFailure, repeatedFailure);
    }
}
