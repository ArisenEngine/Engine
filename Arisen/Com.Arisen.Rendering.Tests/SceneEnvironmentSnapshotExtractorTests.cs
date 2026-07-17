using System.Numerics;
using ArisenEngine.Core.ECS;
using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class SceneEnvironmentSnapshotExtractorTests
{
    [Fact]
    public void Extract_AcceptsFirstEnabledEnvironmentAndReportsOverflow()
    {
        var disabled = SceneEnvironmentComponent.Default;
        disabled.Enabled = 0;

        var firstEnabled = SceneEnvironmentComponent.Default;
        firstEnabled.EnvironmentTextureGuid = Guid.Parse("12121212-3434-5656-7878-909090909090");
        firstEnabled.SkyColor = new Vector3(0.1f, 0.2f, 0.3f);
        firstEnabled.AmbientColor = new Vector3(0.4f, 0.5f, 0.6f);
        firstEnabled.AmbientIntensity = 0.45f;
        firstEnabled.Exposure = 1.35f;

        var dropped = SceneEnvironmentComponent.Default;
        dropped.SkyColor = Vector3.One;

        SceneEnvironmentComponent[] source = [disabled, firstEnabled, dropped];

        var stats = SceneEnvironmentSnapshotExtractor.Extract(source, out var environment);

        Assert.Equal(3, stats.SourceCount);
        Assert.Equal(2, stats.EnabledCount);
        Assert.Equal(1, stats.AcceptedCount);
        Assert.Equal(1, stats.DroppedCount);
        Assert.Equal(firstEnabled.SkyColor, environment.SkyColor);
        Assert.Equal(firstEnabled.AmbientColor, environment.AmbientColor);
        Assert.Equal(firstEnabled.AmbientIntensity, environment.AmbientIntensity);
        Assert.Equal(firstEnabled.Exposure, environment.Exposure);
        Assert.Equal(firstEnabled.EnvironmentTextureGuid, environment.EnvironmentTextureGuid);
    }

    [Fact]
    public void ExposurePolicy_PreservesBaselineAndBoundsInvalidValues()
    {
        Assert.Equal(1.0f, SceneEnvironmentComponent.Default.Exposure);
        Assert.Equal(1.0f, SceneEnvironment.Default.Exposure);
        Assert.Equal(0.0f, SceneEnvironmentComponent.NormalizeExposure(-2.0f));
        Assert.Equal(2.5f, SceneEnvironmentComponent.NormalizeExposure(2.5f));
        Assert.Equal(64.0f, SceneEnvironmentComponent.NormalizeExposure(100.0f));
        Assert.Equal(1.0f, SceneEnvironmentComponent.NormalizeExposure(float.NaN));
        Assert.Equal(1.0f, SceneEnvironment.NormalizeExposure(float.PositiveInfinity));
    }

    [Fact]
    public void Extract_ReturnsEmptySnapshotWhenAllEnvironmentsAreDisabled()
    {
        var disabled = SceneEnvironmentComponent.Default;
        disabled.Enabled = 0;
        SceneEnvironmentComponent[] source = [disabled];

        var stats = SceneEnvironmentSnapshotExtractor.Extract(source, out var environment);

        Assert.Equal(1, stats.SourceCount);
        Assert.Equal(0, stats.EnabledCount);
        Assert.Equal(0, stats.AcceptedCount);
        Assert.Equal(0, stats.DroppedCount);
        Assert.False(environment.IsValid);
    }
}
