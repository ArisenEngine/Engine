using ArisenEngine.RHI.Vulkan.Native;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderDocStartupPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("no")]
    public void ResolveDisablesOrdinaryStartup(string? optInValue)
    {
        Assert.Equal(
            RenderDocStartupMode.Disabled,
            RenderDocStartupPolicy.Resolve(optInValue, moduleAlreadyLoaded: false));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("yes")]
    public void ResolvePreloadsOnlyForExplicitOptIn(string optInValue)
    {
        Assert.Equal(
            RenderDocStartupMode.PreloadRequested,
            RenderDocStartupPolicy.Resolve(optInValue, moduleAlreadyLoaded: false));
    }

    [Fact]
    public void ResolvePreservesExternalPreInitializationInjection()
    {
        Assert.Equal(
            RenderDocStartupMode.AlreadyInjected,
            RenderDocStartupPolicy.Resolve(optInValue: null, moduleAlreadyLoaded: true));
    }
}
