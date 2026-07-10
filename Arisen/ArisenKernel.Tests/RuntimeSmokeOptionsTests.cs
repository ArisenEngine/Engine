using ArisenKernel.Lifecycle;
using Xunit;

namespace ArisenKernel.Tests;

public sealed class RuntimeSmokeOptionsTests
{
    [Fact]
    public void ParseKeepsLegacySmokeAsBootSmoke()
    {
        var options = RuntimeSmokeOptions.Parse(new[] { "--smoke", "--frames", "3" });

        Assert.True(options.Enabled);
        Assert.Equal(RuntimeSmokeMode.Boot, options.Mode);
        Assert.Equal("boot", options.ModeName);
        Assert.Equal(3u, options.RequestedFrameCount);
        Assert.Equal(3u, options.EffectiveFrameCount);
    }

    [Fact]
    public void SceneSmokeRunsAtLeastSetupAndRenderFrames()
    {
        var options = RuntimeSmokeOptions.Parse(new[] { "--smoke-mode", "scene", "--frames", "1" });

        Assert.True(options.Enabled);
        Assert.Equal(RuntimeSmokeMode.Scene, options.Mode);
        Assert.Equal("scene", options.ModeName);
        Assert.Equal(1u, options.RequestedFrameCount);
        Assert.Equal(2u, options.EffectiveFrameCount);
    }

    [Fact]
    public void HotReloadSmokeRunsMultiFrameSceneStabilityWindow()
    {
        var options = RuntimeSmokeOptions.Parse(new[] { "--smoke-mode", "hot-reload", "--frames", "2" });

        Assert.True(options.Enabled);
        Assert.Equal(RuntimeSmokeMode.HotReload, options.Mode);
        Assert.Equal("hot-reload", options.ModeName);
        Assert.Equal(2u, options.RequestedFrameCount);
        Assert.Equal(4u, options.EffectiveFrameCount);
    }

    [Fact]
    public void UnknownSmokeModeFailsEarly()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            RuntimeSmokeOptions.Parse(new[] { "--smoke-mode", "gpu-only" }));

        Assert.Contains("Unknown smoke mode", exception.Message);
    }
}
