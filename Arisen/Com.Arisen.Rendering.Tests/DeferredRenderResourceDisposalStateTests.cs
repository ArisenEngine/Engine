using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class DeferredRenderResourceDisposalStateTests
{
    [Fact]
    public void RejectsRebindingBeforeCurrentGenerationIsReleased()
    {
        var state = new DeferredRenderResourceDisposalState();
        state.Bind(new IntPtr(0x1000), 1);

        var exception = Assert.Throws<InvalidOperationException>(
            () => state.Bind(new IntPtr(0x2000), 2));

        Assert.Contains("still bound to graphics generation 1", exception.Message);
    }

    [Fact]
    public void RejectsTicketBeyondGenerationSubmissionBoundary()
    {
        var state = new DeferredRenderResourceDisposalState();
        state.Bind(new IntPtr(0x1000), 7);

        var exception = Assert.Throws<InvalidOperationException>(
            () => state.ValidateDrainBoundary(
                maximumPendingTicket: 6746,
                submittedThroughTicket: 6502));

        Assert.Contains("ticket 6746 exceeds", exception.Message);
        Assert.Contains("last submitted ticket 6502", exception.Message);
    }

    [Fact]
    public void AllowsNextGenerationOnlyAfterEmptyRelease()
    {
        var state = new DeferredRenderResourceDisposalState();
        state.Bind(new IntPtr(0x1000), 1);

        Assert.Throws<InvalidOperationException>(
            () => state.Unbind(new IntPtr(0x1000), 1, pendingCount: 1));

        state.Unbind(new IntPtr(0x1000), 1, pendingCount: 0);
        state.Bind(new IntPtr(0x2000), 2);

        Assert.True(state.IsBound);
        Assert.Equal(2UL, state.DeviceGeneration);
    }
}
