using ArisenEngine.Rendering;
using ArisenKernel.Contracts;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderDocCaptureRequestStateTests
{
    private static readonly RenderSurfaceRegistration Scene = new(new IntPtr(0x100), 7);
    private static readonly RenderSurfaceRegistration Game = new(new IntPtr(0x200), 8);

    [Fact]
    public void UnrelatedSurfaceLeavesRequestPendingUntilExactTargetClaimsIt()
    {
        var state = new RenderDocCaptureRequestState();

        Assert.True(state.TryRequest(Scene, out var requested));
        Assert.False(state.TryBeginCapture(Game, out _, out var skipped));
        Assert.Equal(requested, skipped);
        Assert.Equal(RenderDocCaptureRequestStatus.Pending, state.Snapshot.Status);

        Assert.True(state.TryBeginCapture(Scene, out var lease, out var capturing));
        Assert.True(lease.IsValid);
        Assert.Equal(Scene, lease.Target);
        Assert.Equal(RenderDocCaptureRequestStatus.Capturing, capturing.Status);
    }

    [Fact]
    public void ReplacementGenerationCannotClaimStaleRequest()
    {
        var state = new RenderDocCaptureRequestState();
        var replacement = new RenderSurfaceRegistration(Scene.Host, Scene.Generation + 1);

        Assert.True(state.TryRequest(Scene, out _));
        Assert.False(state.TryBeginCapture(replacement, out _, out var skipped));

        Assert.Equal(RenderDocCaptureRequestStatus.Pending, skipped.Status);
        Assert.Equal(Scene, skipped.Target);
    }

    [Fact]
    public void SurfaceFailurePublishesAttributableTerminalEvidence()
    {
        var state = new RenderDocCaptureRequestState();
        Assert.True(state.TryRequest(Scene, out var requested));
        Assert.True(state.TryBeginCapture(Scene, out var lease, out _));

        Assert.True(state.TryFail(
            lease,
            RenderDocCaptureFailureStage.SurfaceFrame,
            "submission failed",
            out var failed));

        Assert.Equal(requested.RequestId, failed.RequestId);
        Assert.Equal(Scene, failed.Target);
        Assert.Equal(RenderDocCaptureRequestStatus.Failed, failed.Status);
        Assert.Equal(RenderDocCaptureFailureStage.SurfaceFrame, failed.FailureStage);
        Assert.Equal("submission failed", failed.Diagnostic);
        Assert.True(failed.IsTerminal);
        Assert.False(failed.IsActive);
    }

    [Fact]
    public void TargetUnregistrationFailsPendingRequestWithoutAffectingAnotherSurface()
    {
        var state = new RenderDocCaptureRequestState();
        Assert.True(state.TryRequest(Scene, out _));

        Assert.False(state.TryFailActiveForSurface(
            Game,
            RenderDocCaptureFailureStage.SurfaceUnregistered,
            "game removed",
            out var unchanged));
        Assert.Equal(RenderDocCaptureRequestStatus.Pending, unchanged.Status);

        Assert.True(state.TryFailActiveForSurface(
            Scene,
            RenderDocCaptureFailureStage.SurfaceUnregistered,
            "scene removed",
            out var failed));
        Assert.Equal(RenderDocCaptureRequestStatus.Failed, failed.Status);
        Assert.Equal(RenderDocCaptureFailureStage.SurfaceUnregistered, failed.FailureStage);
        Assert.Equal("scene removed", failed.Diagnostic);
    }

    [Fact]
    public void ActiveRequestRejectsOverlapWithoutChangingIdentityOrTarget()
    {
        var state = new RenderDocCaptureRequestState();
        Assert.True(state.TryRequest(Scene, out var first));

        Assert.False(state.TryRequest(Game, out var rejected));

        Assert.Equal(first, rejected);
        Assert.Equal(first, state.Snapshot);
    }

    [Fact]
    public void EndedCaptureRemainsActiveUntilArtifactPathIsPublished()
    {
        var state = new RenderDocCaptureRequestState();
        Assert.True(state.TryRequest(Scene, out var first));
        Assert.True(state.TryBeginCapture(Scene, out var lease, out _));
        Assert.True(state.TryBeginArtifactPublication(
            lease,
            "EndFrameCapture succeeded.",
            out var publishing));

        Assert.Equal(RenderDocCaptureRequestStatus.PublishingArtifact, publishing.Status);
        Assert.True(publishing.IsActive);
        Assert.False(publishing.IsTerminal);
        Assert.Equal(string.Empty, publishing.CapturePath);
        Assert.False(state.TryRequest(Game, out var rejected));
        Assert.Equal(publishing, rejected);

        Assert.False(state.TryFailActiveForSurface(
            Scene,
            RenderDocCaptureFailureStage.SurfaceUnregistered,
            "surface removed after capture ended",
            out var stillPublishing));
        Assert.Equal(publishing, stillPublishing);
        Assert.Equal(first.RequestId, stillPublishing.RequestId);
    }

    [Fact]
    public void PublishedArtifactPathCompletesRequestAndAllowsMonotonicReuse()
    {
        var state = new RenderDocCaptureRequestState();
        Assert.True(state.TryRequest(Scene, out var first));
        Assert.True(state.TryBeginCapture(Scene, out var lease, out _));
        Assert.True(state.TryBeginArtifactPublication(
            lease,
            "EndFrameCapture succeeded.",
            out _));
        Assert.True(state.TryCompleteArtifact(
            lease,
            @"C:\captures\scene.rdc",
            "capture artifact published",
            out var completed));

        Assert.Equal(RenderDocCaptureRequestStatus.Succeeded, completed.Status);
        Assert.Equal("capture artifact published", completed.Diagnostic);
        Assert.Equal(@"C:\captures\scene.rdc", completed.CapturePath);
        Assert.False(state.TryCompleteArtifact(
            lease,
            @"C:\captures\duplicate.rdc",
            "duplicate",
            out var unchanged));
        Assert.Equal(completed, unchanged);

        Assert.True(state.TryRequest(Game, out var second));
        Assert.Equal(first.RequestId + 1, second.RequestId);
        Assert.Equal(Game, second.Target);
        Assert.Equal(RenderDocCaptureRequestStatus.Pending, second.Status);
    }

    [Fact]
    public void ShutdownFailsPublishingRequestAndLateArtifactCannotOverwriteIt()
    {
        var state = new RenderDocCaptureRequestState();
        Assert.True(state.TryRequest(Scene, out _));
        Assert.True(state.TryBeginCapture(Scene, out var lease, out _));
        Assert.True(state.TryBeginArtifactPublication(
            lease,
            "EndFrameCapture succeeded.",
            out _));

        Assert.True(state.TryFailActive(
            RenderDocCaptureFailureStage.RenderSubsystemShutdown,
            "shutdown",
            out var failed));
        Assert.False(state.TryCompleteArtifact(
            lease,
            @"C:\captures\late.rdc",
            "late completion",
            out var unchanged));

        Assert.Equal(failed, unchanged);
        Assert.Equal(RenderDocCaptureFailureStage.RenderSubsystemShutdown, failed.FailureStage);

        Assert.True(state.TryRequest(Game, out var reloaded));
        Assert.Equal(failed.RequestId + 1, reloaded.RequestId);
        Assert.Equal(Game, reloaded.Target);
        Assert.Equal(RenderDocCaptureRequestStatus.Pending, reloaded.Status);
    }
}
