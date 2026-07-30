using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderFrameResourceSlotStateTests
{
    private static readonly IntPtr Device = new(0x1234);

    [Fact]
    public void EqualModuloSurfaceFramesReceiveDistinctDeviceSlots()
    {
        const uint sceneOutputFrame = 172;
        const uint gameOutputFrame = 168;
        Assert.Equal(sceneOutputFrame % 2, gameOutputFrame % 2);

        var state = new RenderFrameResourceSlotState();
        RenderFrameResourceReservation scene = state.Reserve(Device, deviceGeneration: 7, slotCount: 2);
        state.Complete(scene, lastTicket: 100);
        RenderFrameResourceReservation game = state.Reserve(Device, deviceGeneration: 7, slotCount: 2);

        Assert.Equal(0u, scene.SlotIndex);
        Assert.Equal(1u, game.SlotIndex);
        Assert.NotEqual(scene.SlotIndex, game.SlotIndex);
    }

    [Fact]
    public void ReuseCarriesThePreviousSubmissionTicket()
    {
        var state = new RenderFrameResourceSlotState();
        RenderFrameResourceReservation first = state.Reserve(Device, 3, 2);
        state.Complete(first, 41);
        RenderFrameResourceReservation second = state.Reserve(Device, 3, 2);
        state.Complete(second, 57);

        RenderFrameResourceReservation reused = state.Reserve(Device, 3, 2);

        Assert.Equal(first.SlotIndex, reused.SlotIndex);
        Assert.Equal(41ul, reused.PreviousTicket);
    }

    [Fact]
    public void CancelPreservesTheTicketRequiredForReuse()
    {
        var state = new RenderFrameResourceSlotState();
        RenderFrameResourceReservation first = state.Reserve(Device, 5, 1);
        state.Complete(first, 91);

        RenderFrameResourceReservation cancelled = state.Reserve(Device, 5, 1);
        state.Cancel(cancelled);
        RenderFrameResourceReservation retried = state.Reserve(Device, 5, 1);

        Assert.Equal(91ul, retried.PreviousTicket);
    }

    [Fact]
    public void DeviceGenerationReplacementRejectsStaleCompletion()
    {
        var state = new RenderFrameResourceSlotState();
        RenderFrameResourceReservation oldGeneration = state.Reserve(Device, 1, 2);
        state.Complete(oldGeneration, 12);

        RenderFrameResourceReservation newGeneration = state.Reserve(Device, 2, 2);

        Assert.Equal(0ul, newGeneration.PreviousTicket);
        Assert.NotEqual(oldGeneration.Epoch, newGeneration.Epoch);
        Assert.Throws<InvalidOperationException>(() => state.Complete(oldGeneration, 99));
    }

    [Fact]
    public void ActiveReservationBlocksConcurrentSlotReuseAndReset()
    {
        var state = new RenderFrameResourceSlotState();
        RenderFrameResourceReservation reservation = state.Reserve(Device, 1, 1);

        Assert.Throws<InvalidOperationException>(() => state.Reserve(Device, 1, 1));
        Assert.Throws<InvalidOperationException>(state.Reset);

        state.Cancel(reservation);
        state.Reset();
    }

    [Fact]
    public void CompletionRejectsAStaleOrRepeatedGraphicsTicket()
    {
        var state = new RenderFrameResourceSlotState();
        RenderFrameResourceReservation first = state.Reserve(Device, 1, 1);
        state.Complete(first, 40);

        RenderFrameResourceReservation reused = state.Reserve(Device, 1, 1);

        Assert.Throws<InvalidOperationException>(() => state.Complete(reused, 40));
        state.Cancel(reused);
    }
}
