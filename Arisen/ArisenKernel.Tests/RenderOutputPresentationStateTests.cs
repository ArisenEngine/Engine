using System;
using ArisenKernel.Contracts;
using Xunit;

namespace ArisenKernel.Tests;

public sealed class RenderOutputPresentationStateTests
{
    [Fact]
    public void EvaluateSkipsWarmingUpOutput()
    {
        var state = new RenderOutputPresentationState();

        var decision = state.Evaluate(new RenderOutputInfo
        {
            ResizeGeneration = 1,
            Width = 1280,
            Height = 720
        }, requiresSemaphores: false);

        Assert.False(decision.ShouldPresent);
        Assert.False(decision.ShouldReleaseSignalSemaphore);
        Assert.Equal(RenderOutputPresentationSkipReason.WarmingUp, decision.SkipReason);
    }

    [Fact]
    public void EvaluateSkipsDuplicatePresentedTicket()
    {
        var state = new RenderOutputPresentationState();
        var info = CreatePresentableInfo(ticket: 42);

        Assert.True(state.Evaluate(info, requiresSemaphores: false).ShouldPresent);
        state.MarkPresented(info);

        var duplicate = state.Evaluate(info, requiresSemaphores: false);

        Assert.False(duplicate.ShouldPresent);
        Assert.Equal(RenderOutputPresentationSkipReason.DuplicateTicket, duplicate.SkipReason);
    }

    [Fact]
    public void EvaluateRequiresSemaphoresWhenCompositorDoes()
    {
        var state = new RenderOutputPresentationState();
        var info = CreatePresentableInfo(ticket: 7);
        info.WaitSemaphoreHandle = IntPtr.Zero;

        var decision = state.Evaluate(info, requiresSemaphores: true);

        Assert.False(decision.ShouldPresent);
        Assert.True(decision.ShouldReleaseSignalSemaphore);
        Assert.Equal(RenderOutputPresentationSkipReason.MissingSemaphore, decision.SkipReason);
    }

    [Fact]
    public void ResizeGenerationOrSizeChangeResetsImportedImageCache()
    {
        var state = new RenderOutputPresentationState();
        var initial = CreatePresentableInfo(ticket: 1, generation: 1, width: 640, height: 360);

        Assert.True(state.ShouldResetImportedImageCache(initial));
        state.MarkImportedImageCacheCurrent(initial);
        Assert.False(state.ShouldResetImportedImageCache(initial));

        var resized = CreatePresentableInfo(ticket: 2, generation: 2, width: 1280, height: 720);

        Assert.True(state.ShouldResetImportedImageCache(resized));
        state.MarkImportedImageCacheCurrent(resized);
        Assert.False(state.ShouldResetImportedImageCache(resized));
    }

    private static RenderOutputInfo CreatePresentableInfo(
        ulong ticket,
        uint generation = 1,
        uint width = 1280,
        uint height = 720)
    {
        return new RenderOutputInfo
        {
            Ticket = ticket,
            FrameIndex = 0,
            ResizeGeneration = generation,
            SharedHandle = new IntPtr(0x1000),
            MemorySize = 4096,
            WaitSemaphoreHandle = new IntPtr(0x2000),
            SignalSemaphoreHandle = new IntPtr(0x3000),
            Width = width,
            Height = height
        };
    }
}
