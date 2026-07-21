using System.Numerics;
using ArisenEngine.Core.ECS;
using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class DirectionalLightSnapshotExtractorTests
{
    [Fact]
    public void Extract_AcceptsFirstEnabledLightAndReportsLimitOverflow()
    {
        var disabled = DirectionalLightComponent.Default;
        disabled.Enabled = 0;

        var firstEnabled = DirectionalLightComponent.Default;
        firstEnabled.Direction = new Vector3(0.0f, 2.0f, 0.0f);
        firstEnabled.Color = new Vector3(1.0f, 0.5f, 0.25f);
        firstEnabled.Intensity = 2.0f;

        var dropped = DirectionalLightComponent.Default;
        dropped.Direction = Vector3.UnitX;

        DirectionalLightComponent[] source = [disabled, firstEnabled, dropped];
        Span<DirectionalLight> destination = stackalloc DirectionalLight[
            DirectionalLightSnapshotExtractor.MaxDirectionalLightsPerFrame];

        var stats = DirectionalLightSnapshotExtractor.Extract(source, destination);

        Assert.Equal(3, stats.SourceCount);
        Assert.Equal(2, stats.EnabledCount);
        Assert.Equal(1, stats.AcceptedCount);
        Assert.Equal(0, stats.InvalidInputCount);
        Assert.Equal(1, stats.DroppedCount);
        Assert.Equal(Vector3.UnitY, destination[0].Direction);
        Assert.Equal(firstEnabled.Color, destination[0].Color);
        Assert.Equal(firstEnabled.Intensity, destination[0].Intensity);
    }

    [Fact]
    public void Extract_ReportsEnabledLightAsDroppedWhenDestinationIsEmpty()
    {
        DirectionalLightComponent[] source = [DirectionalLightComponent.Default];

        var stats = DirectionalLightSnapshotExtractor.Extract(
            source,
            Span<DirectionalLight>.Empty);

        Assert.Equal(1, stats.SourceCount);
        Assert.Equal(1, stats.EnabledCount);
        Assert.Equal(0, stats.AcceptedCount);
        Assert.Equal(0, stats.InvalidInputCount);
        Assert.Equal(1, stats.DroppedCount);
    }

    [Fact]
    public void ExtractRejectsNonFiniteLightInput()
    {
        DirectionalLightComponent invalid = DirectionalLightComponent.Default;
        invalid.Direction = new Vector3(float.PositiveInfinity, 0, 0);
        Span<DirectionalLight> destination = stackalloc DirectionalLight[1];

        DirectionalLightExtractionStats stats =
            DirectionalLightSnapshotExtractor.Extract([invalid], destination);

        Assert.Equal(0, stats.AcceptedCount);
        Assert.Equal(1, stats.InvalidInputCount);
        Assert.Equal(1, stats.DroppedCount);
    }
}
