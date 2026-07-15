using Arisen.Native.RHI;
using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderOutputEncodingTests
{
    [Fact]
    public void RequiresExplicitSrgbEncoding_OnlyForEightBitUnormOutputs()
    {
        Assert.True(RenderOutputEncoding.RequiresExplicitSrgbEncoding(EFormat.FORMAT_R8G8B8A8_UNORM));
        Assert.True(RenderOutputEncoding.RequiresExplicitSrgbEncoding(EFormat.FORMAT_B8G8R8A8_UNORM));
        Assert.False(RenderOutputEncoding.RequiresExplicitSrgbEncoding(EFormat.FORMAT_R8G8B8A8_SRGB));
        Assert.False(RenderOutputEncoding.RequiresExplicitSrgbEncoding(EFormat.FORMAT_B8G8R8A8_SRGB));
        Assert.False(RenderOutputEncoding.RequiresExplicitSrgbEncoding(EFormat.FORMAT_R16G16B16A16_SFLOAT));
    }
}
