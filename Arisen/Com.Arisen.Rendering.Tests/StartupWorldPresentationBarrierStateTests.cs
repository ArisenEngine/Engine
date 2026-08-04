using ArisenEditor.Views;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class StartupWorldPresentationBarrierStateTests
{
    private static readonly StartupWorldPresentationTarget s_StartupWorld = new(
        Guid.Parse("ad000000-0000-0000-0000-000000000001"),
        "com.arisen.startup");
    private static readonly StartupWorldPresentationTarget s_ReplacementWorld = new(
        Guid.Parse("ad000000-0000-0000-0000-000000000002"),
        "com.arisen.replacement");
    private static readonly StartupWorldPresentationTarget s_InterveningWorld = new(
        Guid.Parse("ad000000-0000-0000-0000-000000000003"),
        "com.arisen.intervening");

    [Fact]
    public void ActivationBoundaryDiscardsQueuedOutputsAndReleasesOnlyForNewerTicket()
    {
        var barrier = new StartupWorldPresentationBarrierState();
        Assert.True(barrier.TryBegin(s_StartupWorld, Pending(1, s_StartupWorld)));

        Assert.Equal(
            StartupWorldPresentationReconcileDecision.ActivationBoundaryCaptured,
            barrier.Reconcile(2, Active(2, s_StartupWorld), activationOutputTicket: 10));

        Assert.True(barrier.HasActivationBoundary);
        Assert.Equal(2, barrier.ActivationRevision);
        Assert.Equal(10ul, barrier.ActivationOutputTicket);
        Assert.True(barrier.IsCurrentActivation(Active(2, s_StartupWorld)));
        Assert.Equal(
            StartupWorldPresentationBarrierDecision.WaitForOutput,
            barrier.Evaluate(outputTicket: 0));
        Assert.Equal(
            StartupWorldPresentationBarrierDecision.DiscardOutput,
            barrier.Evaluate(outputTicket: 7));
        Assert.Equal(
            StartupWorldPresentationBarrierDecision.DiscardOutput,
            barrier.Evaluate(outputTicket: 10));
        Assert.Equal(
            StartupWorldPresentationBarrierDecision.PresentAndRelease,
            barrier.Evaluate(outputTicket: 11));

        barrier.CompleteAfterPresented(outputTicket: 11);

        Assert.False(barrier.IsActive);
        Assert.Equal(
            StartupWorldPresentationBarrierDecision.None,
            barrier.Evaluate(outputTicket: 12));
    }

    [Fact]
    public void BoundaryTicketCannotCompleteBarrier()
    {
        var barrier = ActiveBarrier(activationTicket: 42);

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => barrier.CompleteAfterPresented(outputTicket: 42));

        Assert.Contains("does not cross", failure.Message, StringComparison.Ordinal);
        Assert.True(barrier.IsActive);
        Assert.Equal(
            StartupWorldPresentationBarrierDecision.PresentAndRelease,
            barrier.Evaluate(outputTicket: 43));
    }

    [Fact]
    public void ZeroBoundaryRequiresFirstProducedOutput()
    {
        var barrier = ActiveBarrier(activationTicket: 0);

        Assert.Equal(
            StartupWorldPresentationBarrierDecision.WaitForOutput,
            barrier.Evaluate(outputTicket: 0));
        Assert.Equal(
            StartupWorldPresentationBarrierDecision.PresentAndRelease,
            barrier.Evaluate(outputTicket: 1));
    }

    [Fact]
    public void ActiveReplacementWorldDoesNotRearmForReattachOrRestart()
    {
        var barrier = new StartupWorldPresentationBarrierState();

        Assert.False(barrier.TryBegin(s_StartupWorld, Active(7, s_ReplacementWorld)));
        Assert.False(barrier.IsActive);
        Assert.Equal(
            StartupWorldPresentationBarrierDecision.None,
            barrier.Evaluate(outputTicket: 7));

        Assert.False(barrier.TryBegin(s_StartupWorld, Active(7, s_ReplacementWorld)));
        Assert.False(barrier.IsActive);
    }

    [Fact]
    public void PendingStartupFollowsSupersedingWinnerAndCapturesItsExactTicket()
    {
        var barrier = new StartupWorldPresentationBarrierState();
        Assert.True(barrier.TryBegin(s_StartupWorld, Pending(1, s_StartupWorld)));

        Assert.Equal(
            StartupWorldPresentationReconcileDecision.WaitForActivation,
            barrier.Reconcile(2, Pending(2, s_ReplacementWorld), activationOutputTicket: 11));
        Assert.Equal(s_ReplacementWorld, barrier.Target);
        Assert.Equal(2, barrier.ObservedRevision);

        Assert.Equal(
            StartupWorldPresentationReconcileDecision.ActivationBoundaryCaptured,
            barrier.Reconcile(3, Active(3, s_ReplacementWorld), activationOutputTicket: 20));
        Assert.Equal(s_ReplacementWorld, barrier.Target);
        Assert.Equal(3, barrier.ActivationRevision);
        Assert.Equal(20ul, barrier.ActivationOutputTicket);
        Assert.Equal(
            StartupWorldPresentationBarrierDecision.DiscardOutput,
            barrier.Evaluate(outputTicket: 20));
        Assert.Equal(
            StartupWorldPresentationBarrierDecision.PresentAndRelease,
            barrier.Evaluate(outputTicket: 21));
    }

    [Fact]
    public void FreshBarrierArmsForSupersedingPendingWorldAfterRestart()
    {
        var beforeRestart = new StartupWorldPresentationBarrierState();
        Assert.True(beforeRestart.TryBegin(
            s_StartupWorld,
            Pending(7, s_ReplacementWorld)));
        Assert.Equal(s_ReplacementWorld, beforeRestart.Target);

        var restored = new StartupWorldPresentationBarrierState();
        Assert.True(restored.TryBegin(
            s_StartupWorld,
            Pending(7, s_ReplacementWorld)));
        Assert.True(restored.IsActive);
        Assert.Equal(s_ReplacementWorld, restored.Target);
        Assert.Equal(7, restored.ObservedRevision);
    }

    [Fact]
    public void StaleAndAbaCallbacksCannotCaptureObsoleteTicket()
    {
        var barrier = new StartupWorldPresentationBarrierState();
        Assert.True(barrier.TryBegin(s_StartupWorld, Pending(1, s_StartupWorld)));

        Assert.Equal(
            StartupWorldPresentationReconcileDecision.StaleNotification,
            barrier.Reconcile(1, Pending(2, s_ReplacementWorld), activationOutputTicket: 5));
        Assert.Equal(s_StartupWorld, barrier.Target);
        Assert.Equal(
            StartupWorldPresentationReconcileDecision.WaitForActivation,
            barrier.Reconcile(2, Pending(2, s_ReplacementWorld), activationOutputTicket: 6));

        // Revision 3 was B's first activation. Revisions 4 and 5 model B -> C -> B
        // before the original B callback reaches the UI thread.
        Assert.Equal(
            StartupWorldPresentationReconcileDecision.StaleNotification,
            barrier.Reconcile(3, Active(5, s_ReplacementWorld), activationOutputTicket: 20));
        Assert.False(barrier.HasActivationBoundary);

        Assert.Equal(
            StartupWorldPresentationReconcileDecision.ActivationBoundaryCaptured,
            barrier.Reconcile(5, Active(5, s_ReplacementWorld), activationOutputTicket: 40));
        Assert.Equal(40ul, barrier.ActivationOutputTicket);
        Assert.Equal(5, barrier.ActivationRevision);
    }

    [Fact]
    public void ReplacementAfterCapturedBoundaryRearmsUntilNewWinnerActivates()
    {
        var barrier = ActiveBarrier(activationTicket: 10);

        Assert.Equal(
            StartupWorldPresentationReconcileDecision.WaitForActivation,
            barrier.Reconcile(3, Pending(3, s_InterveningWorld), activationOutputTicket: 12));
        Assert.False(barrier.HasActivationBoundary);
        Assert.Equal(s_InterveningWorld, barrier.Target);
        Assert.Equal(
            StartupWorldPresentationBarrierDecision.WaitForActivation,
            barrier.Evaluate(outputTicket: 11));

        Assert.Equal(
            StartupWorldPresentationReconcileDecision.ActivationBoundaryCaptured,
            barrier.Reconcile(4, Active(4, s_InterveningWorld), activationOutputTicket: 30));
        Assert.Equal(30ul, barrier.ActivationOutputTicket);
        Assert.False(barrier.IsCurrentActivation(Active(3, s_InterveningWorld)));
        Assert.True(barrier.IsCurrentActivation(Active(4, s_InterveningWorld)));
    }

    [Fact]
    public void PendingSuccessorWinsOverOutgoingActiveWorldInSameRevision()
    {
        var barrier = ActiveBarrier(activationTicket: 10);
        StartupWorldPresentationObservation admitted = ActiveAndPending(
            revision: 3,
            activeWorld: s_StartupWorld,
            pendingWorld: s_ReplacementWorld);

        Assert.Equal(
            StartupWorldPresentationReconcileDecision.WaitForActivation,
            barrier.Reconcile(3, admitted, activationOutputTicket: 12));

        Assert.True(barrier.IsActive);
        Assert.False(barrier.HasActivationBoundary);
        Assert.Equal(s_ReplacementWorld, barrier.Target);
        Assert.Equal(3, barrier.ObservedRevision);
        Assert.False(barrier.IsCurrentActivation(admitted));
        Assert.Equal(
            StartupWorldPresentationBarrierDecision.WaitForActivation,
            barrier.Evaluate(outputTicket: 13));

        Assert.Equal(
            StartupWorldPresentationReconcileDecision.ActivationBoundaryCaptured,
            barrier.Reconcile(
                4,
                Active(4, s_ReplacementWorld),
                activationOutputTicket: 20));
        Assert.Equal(s_ReplacementWorld, barrier.Target);
        Assert.Equal(20ul, barrier.ActivationOutputTicket);
    }

    [Fact]
    public void PendingFailureWithoutActivationReleasesBarrier()
    {
        var barrier = new StartupWorldPresentationBarrierState();
        Assert.True(barrier.TryBegin(s_StartupWorld, Pending(1, s_StartupWorld)));

        Assert.Equal(
            StartupWorldPresentationReconcileDecision.ReleaseWithoutActivation,
            barrier.Reconcile(2, Empty(2), activationOutputTicket: 7));
        Assert.False(barrier.IsActive);
        Assert.Equal(
            StartupWorldPresentationBarrierDecision.None,
            barrier.Evaluate(outputTicket: 7));
    }

    private static StartupWorldPresentationBarrierState ActiveBarrier(ulong activationTicket)
    {
        var barrier = new StartupWorldPresentationBarrierState();
        Assert.True(barrier.TryBegin(s_StartupWorld, Pending(1, s_StartupWorld)));
        Assert.Equal(
            StartupWorldPresentationReconcileDecision.ActivationBoundaryCaptured,
            barrier.Reconcile(2, Active(2, s_StartupWorld), activationTicket));
        return barrier;
    }

    private static StartupWorldPresentationObservation Empty(long revision) => new(
        revision,
        ActiveWorld: null,
        ActiveWorldGuid: Guid.Empty,
        PendingWorld: null);

    private static StartupWorldPresentationObservation Pending(
        long revision,
        StartupWorldPresentationTarget pendingWorld) => new(
        revision,
        ActiveWorld: null,
        ActiveWorldGuid: Guid.Empty,
        PendingWorld: pendingWorld);

    private static StartupWorldPresentationObservation Active(
        long revision,
        StartupWorldPresentationTarget activeWorld) => new(
        revision,
        ActiveWorld: activeWorld,
        ActiveWorldGuid: activeWorld.Guid,
        PendingWorld: null);

    private static StartupWorldPresentationObservation ActiveAndPending(
        long revision,
        StartupWorldPresentationTarget activeWorld,
        StartupWorldPresentationTarget pendingWorld) => new(
        revision,
        ActiveWorld: activeWorld,
        ActiveWorldGuid: activeWorld.Guid,
        PendingWorld: pendingWorld);
}
