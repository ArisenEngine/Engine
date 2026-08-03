using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderFrameSubmissionStateTests
{
    [Fact]
    public void RecordingFailureLeavesAcquiredFrameRetirable()
    {
        var state = AcquireFrame();

        Assert.True(state.HasFrameOwnership);
        Assert.Equal(RenderFrameEndAction.Retire, state.GetEndAction());
        Assert.True(state.TryBeginRetirement());

        state.CommitRetirement(0);

        Assert.False(state.HasFrameOwnership);
        Assert.Equal(RenderFrameSubmissionPhase.Retired, state.Phase);
        state.ResetForBegin();
    }

    [Fact]
    public void ZeroWorkFrameRequiresRetirement()
    {
        var state = AcquireFrame();

        Assert.Equal(0, state.SubmitCount);
        Assert.Equal(RenderFrameEndAction.Retire, state.GetEndAction());
    }

    [Fact]
    public void PartialSubmissionRetainsTicketAndRequiresRetirement()
    {
        var state = AcquireFrame();
        state.CommitSubmit(ticket: 41, waitForFrameAcquire: true, signalFrameComplete: false);

        Assert.Equal(RenderFrameEndAction.Retire, state.GetEndAction());
        Assert.Equal(41ul, state.LastTicket);
        Assert.True(state.TryBeginRetirement());

        state.CommitRetirement(42);

        Assert.Equal(42ul, state.LastTicket);
        Assert.Equal(RenderFrameSubmissionPhase.Retired, state.Phase);
    }

    [Fact]
    public void PublicationFailureLeavesPresentedVirtualFrameRetirable()
    {
        var state = AcquireFrame();
        state.CommitSubmit(ticket: 51, waitForFrameAcquire: true, signalFrameComplete: true);
        state.MarkPresented();

        Assert.True(state.HasFrameOwnership);
        Assert.Equal(RenderFrameSubmissionPhase.Presented, state.Phase);
        Assert.True(state.TryBeginRetirement());

        state.CommitRetirement(51);

        Assert.False(state.HasFrameOwnership);
        Assert.Equal(RenderFrameSubmissionPhase.Retired, state.Phase);
    }

    [Fact]
    public void CommittedOutputTransfersFrameOwnership()
    {
        var state = AcquireFrame();
        state.CommitSubmit(ticket: 61, waitForFrameAcquire: true, signalFrameComplete: true);
        state.MarkPresented();

        state.CommitOutput();

        Assert.False(state.HasFrameOwnership);
        Assert.Equal(RenderFrameSubmissionPhase.Committed, state.Phase);
        Assert.False(state.TryBeginRetirement());
    }

    [Fact]
    public void FailedNativeSubmitDoesNotCommitManagedSubmissionState()
    {
        var state = AcquireFrame();
        state.ValidateSubmit(waitForFrameAcquire: true, signalFrameComplete: true);

        Assert.Equal(0, state.SubmitCount);
        Assert.Equal(0ul, state.LastTicket);
        Assert.Equal(RenderFrameEndAction.Retire, state.GetEndAction());
    }

    [Fact]
    public void FirstSubmissionMustOwnTheAcquireWaitExactlyOnce()
    {
        var state = AcquireFrame();

        Assert.Throws<InvalidOperationException>(() =>
            state.ValidateSubmit(waitForFrameAcquire: false, signalFrameComplete: false));

        state.CommitSubmit(ticket: 71, waitForFrameAcquire: true, signalFrameComplete: false);

        Assert.Throws<InvalidOperationException>(() =>
            state.ValidateSubmit(waitForFrameAcquire: true, signalFrameComplete: true));
        state.CommitSubmit(ticket: 72, waitForFrameAcquire: false, signalFrameComplete: true);
        Assert.Equal(RenderFrameEndAction.Present, state.GetEndAction());
    }

    [Fact]
    public void RetirementFailurePreservesOwnershipForAttributableRetry()
    {
        var state = AcquireFrame();
        state.CommitSubmit(ticket: 81, waitForFrameAcquire: true, signalFrameComplete: false);
        Assert.True(state.TryBeginRetirement());

        state.CancelRetirement();

        Assert.True(state.HasFrameOwnership);
        Assert.False(state.RetirementPending);
        Assert.Equal(81ul, state.LastTicket);
        Assert.True(state.TryBeginRetirement());
        state.CommitRetirement(82);
    }

    [Fact]
    public void CleanupFailuresAggregateWithOriginalPublicationFailure()
    {
        var publicationFailure = new InvalidOperationException("publication failed");
        var retirementFailure = new InvalidOperationException("retirement failed");
        var completionFailure = new InvalidOperationException("completion failed");

        Exception failure = RenderFrameFailureAggregator.Append(
            publicationFailure,
            "retirement",
            retirementFailure);
        failure = RenderFrameFailureAggregator.Append(
            failure,
            "resource completion",
            completionFailure);

        var aggregate = Assert.IsType<AggregateException>(failure).Flatten();
        Assert.Equal(3, aggregate.InnerExceptions.Count);
        Assert.Contains(publicationFailure, aggregate.InnerExceptions);
        Assert.Contains(
            aggregate.InnerExceptions,
            exception => exception.Message == "Render frame retirement failed." &&
                         exception.InnerException == retirementFailure);
        Assert.Contains(
            aggregate.InnerExceptions,
            exception => exception.Message == "Render frame resource completion failed." &&
                         exception.InnerException == completionFailure);
    }

    private static RenderFrameSubmissionState AcquireFrame()
    {
        var state = new RenderFrameSubmissionState();
        state.ResetForBegin();
        state.MarkAcquired();
        return state;
    }
}
