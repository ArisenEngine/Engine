using Arisen.Native.RHI;
using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RHIHandleAndRenderTargetStateTests
{
    [Fact]
    public void DefaultHandles_AreInvalidUntilAssignedAPoolGeneration()
    {
        Assert.False(default(RHIBufferHandle).IsValid);
        Assert.False(default(RHIImageHandle).IsValid);
        Assert.True(new RHIBufferHandle { Index = 0, Generation = 1 }.IsValid);
        Assert.True(new RHIImageHandle { Index = 0, Generation = 1 }.IsValid);
    }

    [Fact]
    public void TargetImageState_RequiresInitializationForEachNewGeneration()
    {
        var tracker = new RenderTargetImageStateTracker();
        var first = new RHIImageHandle { Index = 3, Generation = 1 };
        var recycled = new RHIImageHandle { Index = 3, Generation = 2 };

        Assert.True(tracker.RequiresInitialization(first));
        tracker.MarkInitialized(first);
        Assert.False(tracker.RequiresInitialization(first));

        Assert.True(tracker.RequiresInitialization(recycled));
        tracker.MarkInitialized(recycled);
        Assert.False(tracker.RequiresInitialization(recycled));
        Assert.False(tracker.RequiresInitialization(RHIImageHandle.Invalid));
    }
}
