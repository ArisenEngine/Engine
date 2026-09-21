using Arisen.Native.RHI;
using ArisenEngine.RHI.Vulkan.Native;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

/// <summary>
/// Pins the display-paced default of the runtime present mode. The engine loop owns no other
/// pacing source, so uncapped IMMEDIATE presentation stays an explicit opt-in and every unset or
/// unrecognized value must keep FIFO instead of silently uncapping a still window.
/// </summary>
public sealed class PresentPacingPolicyTests
{
    [Fact]
    public void OptOutEnvironmentVariableNameIsStable()
    {
        Assert.Equal("ARISEN_PRESENT_MODE", PresentPacingPolicy.EnvironmentVariable);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bogus")]
    [InlineData("immediate-ish")]
    public void UnsetOrUnrecognizedValueKeepsDisplayPacedFifo(string? value)
    {
        Assert.False(PresentPacingPolicy.TryResolveRequestedMode(value, out EPresentMode mode));
        Assert.Equal(EPresentMode.PRESENT_MODE_FIFO, mode);
    }

    [Theory]
    [InlineData("immediate")]
    [InlineData("IMMEDIATE")]
    [InlineData(" uncapped ")]
    [InlineData("off")]
    [InlineData("0")]
    public void UncappedOptInSelectsImmediatePresentation(string value)
    {
        Assert.True(PresentPacingPolicy.TryResolveRequestedMode(value, out EPresentMode mode));
        Assert.Equal(EPresentMode.PRESENT_MODE_IMMEDIATE, mode);
    }

    [Theory]
    [InlineData("fifo", EPresentMode.PRESENT_MODE_FIFO)]
    [InlineData("VSync", EPresentMode.PRESENT_MODE_FIFO)]
    [InlineData("on", EPresentMode.PRESENT_MODE_FIFO)]
    [InlineData("1", EPresentMode.PRESENT_MODE_FIFO)]
    [InlineData("relaxed", EPresentMode.PRESENT_MODE_FIFO_RELAXED)]
    [InlineData("fifo-relaxed", EPresentMode.PRESENT_MODE_FIFO_RELAXED)]
    [InlineData("Mailbox", EPresentMode.PRESENT_MODE_MAILBOX)]
    public void RecognizedValueResolvesToItsPresentMode(string value, EPresentMode expected)
    {
        Assert.True(PresentPacingPolicy.TryResolveRequestedMode(value, out EPresentMode mode));
        Assert.Equal(expected, mode);
    }
}
