using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RuntimeStartupPresentationStateTests
{
    [Fact]
    public void RuntimeWithoutPendingStartupWorldNeverArms()
    {
        var gate = new RuntimeStartupPresentationState();

        Assert.False(gate.TryArm(Observation(revision: 1, pending: false, active: false)));
        Assert.False(gate.IsArmed);
        Assert.Equal(
            RuntimeStartupPresentationDecision.None,
            gate.Evaluate(Observation(revision: 1, pending: false, active: true, content: true)));
    }

    [Fact]
    public void RuntimeWithActiveWorldNeverArms()
    {
        var gate = new RuntimeStartupPresentationState();

        Assert.False(gate.TryArm(Observation(revision: 1, pending: true, active: true)));
        Assert.False(gate.IsArmed);
    }

    [Fact]
    public void DeferredStartupWorldHoldsPresentationUntilTheFrameOwnsSceneContent()
    {
        var gate = new RuntimeStartupPresentationState();
        Assert.True(gate.TryArm(Observation(revision: 4, pending: true, active: false)));
        Assert.True(gate.IsArmed);
        Assert.Equal(4, gate.ArmedRevision);

        Assert.Equal(
            RuntimeStartupPresentationDecision.WaitForWorldActivation,
            gate.Evaluate(Observation(revision: 4, pending: true, active: false)));

        Assert.Equal(
            RuntimeStartupPresentationDecision.WaitForWorldContent,
            gate.Evaluate(Observation(
                revision: 5,
                pending: false,
                active: true,
                content: false)));

        Assert.Equal(2, gate.WaitedFrames);

        Assert.Equal(
            RuntimeStartupPresentationDecision.Release,
            gate.Evaluate(Observation(revision: 5, pending: false, active: true, content: true)));
        Assert.False(gate.IsArmed);
        Assert.True(gate.HasCompleted);
    }

    [Fact]
    public void ReleasedGateKeepsPresentingWithoutRearming()
    {
        var gate = new RuntimeStartupPresentationState();
        Assert.True(gate.TryArm(Observation(revision: 1, pending: true, active: false)));
        Assert.Equal(
            RuntimeStartupPresentationDecision.Release,
            gate.Evaluate(Observation(revision: 2, pending: false, active: true, content: true)));

        Assert.Equal(
            RuntimeStartupPresentationDecision.None,
            gate.Evaluate(Observation(revision: 3, pending: true, active: false)));
        Assert.False(gate.TryArm(Observation(revision: 3, pending: true, active: false)));
        Assert.False(gate.IsArmed);
    }

    [Fact]
    public void WithdrawnStartupWorldReleasesPresentation()
    {
        var gate = new RuntimeStartupPresentationState();
        Assert.True(gate.TryArm(Observation(revision: 1, pending: true, active: false)));

        Assert.Equal(
            RuntimeStartupPresentationDecision.Release,
            gate.Evaluate(Observation(revision: 2, pending: false, active: false)));
        Assert.False(gate.IsArmed);
    }

    [Fact]
    public void FailedStartupStreamingReleasesPresentation()
    {
        var gate = new RuntimeStartupPresentationState();
        Assert.True(gate.TryArm(Observation(revision: 1, pending: true, active: false)));
        Assert.Equal(
            RuntimeStartupPresentationDecision.WaitForWorldActivation,
            gate.Evaluate(Observation(revision: 1, pending: true, active: false)));

        Assert.Equal(
            RuntimeStartupPresentationDecision.Release,
            gate.Evaluate(Observation(
                revision: 5,
                pending: false,
                active: true,
                content: false,
                failed: true)));
        Assert.False(gate.IsArmed);
    }

    [Fact]
    public void ActivatedWorldWithoutDrawContentKeepsWaiting()
    {
        var gate = new RuntimeStartupPresentationState();
        Assert.True(gate.TryArm(Observation(revision: 1, pending: true, active: false)));

        Assert.Equal(
            RuntimeStartupPresentationDecision.WaitForWorldContent,
            gate.Evaluate(Observation(revision: 2, pending: false, active: true, content: false)));
        Assert.True(gate.IsArmed);
    }

    [Fact]
    public void ActiveWorldWithoutStreamedCellsKeepsWaiting()
    {
        var gate = new RuntimeStartupPresentationState();
        Assert.True(gate.TryArm(Observation(revision: 1, pending: true, active: false)));

        Assert.Equal(
            RuntimeStartupPresentationDecision.WaitForStreamedCells,
            gate.Evaluate(Observation(
                revision: 2,
                pending: false,
                active: true,
                content: true,
                streamedCells: 0)));
        Assert.True(gate.IsArmed);

        Assert.Equal(
            RuntimeStartupPresentationDecision.Release,
            gate.Evaluate(Observation(
                revision: 3,
                pending: false,
                active: true,
                content: true,
                streamedCells: 1)));
        Assert.False(gate.IsArmed);
        Assert.True(gate.HasCompleted);
    }

    [Fact]
    public void WorldWithoutCellsReleasesOnSceneContent()
    {
        var gate = new RuntimeStartupPresentationState();
        Assert.True(gate.TryArm(Observation(revision: 1, pending: true, active: false)));

        Assert.Equal(
            RuntimeStartupPresentationDecision.Release,
            gate.Evaluate(Observation(
                revision: 2,
                pending: false,
                active: true,
                content: true,
                streamedCells: 0,
                worldDeclaresCells: false)));
        Assert.False(gate.IsArmed);
    }

    [Fact]
    public void SceneContentBeforeActivationStillWaitsForTheActiveWorld()
    {
        var gate = new RuntimeStartupPresentationState();
        Assert.True(gate.TryArm(Observation(revision: 1, pending: true, active: false)));

        Assert.Equal(
            RuntimeStartupPresentationDecision.WaitForWorldActivation,
            gate.Evaluate(Observation(revision: 1, pending: true, active: false, content: true)));
        Assert.True(gate.IsArmed);
    }

    [Fact]
    public void UnarmedGateNeverReportsARelease()
    {
        var gate = new RuntimeStartupPresentationState();

        Assert.False(gate.TryArm(Observation(revision: 1, pending: false, active: true, content: true)));
        Assert.Equal(
            RuntimeStartupPresentationDecision.None,
            gate.Evaluate(Observation(revision: 2, pending: true, active: false, content: false)));
        Assert.False(gate.IsArmed);
        Assert.False(gate.HasCompleted);
    }

    private static RuntimeStartupPresentationObservation Observation(
        long revision,
        bool pending,
        bool active,
        bool content = false,
        bool failed = false,
        int streamedCells = 1,
        bool worldDeclaresCells = true) =>
        new(revision, pending, active, worldDeclaresCells, streamedCells, content, failed);
}
