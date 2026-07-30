using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class GenericRenderPipelineFeatureRegistryTests
{
    [Fact]
    public void ActivationWithoutOptionalFeaturesProducesAnEmptyFrozenSet()
    {
        var registry = new GenericRenderPipelineFeatureRegistryCore<TestFeature>();

        var active = registry.BeginPipelineActivation();

        Assert.Empty(active);
        Assert.True(registry.IsPipelineActive);
        registry.EndPipelineActivation();
        Assert.False(registry.IsPipelineActive);
    }

    [Fact]
    public void ActivationFreezesFeaturesInDeterministicOrderAndRejectsLateRegistration()
    {
        var registry = new GenericRenderPipelineFeatureRegistryCore<TestFeature>();
        var later = new TestFeature("feature.zeta", 20);
        var earlier = new TestFeature("feature.early", 10);
        var sameOrder = new TestFeature("feature.alpha", 20);
        registry.Register(later, later.Id, later.Order);
        registry.Register(earlier, earlier.Id, earlier.Order);
        registry.Register(sameOrder, sameOrder.Id, sameOrder.Order);

        var active = registry.BeginPipelineActivation();

        Assert.True(registry.IsPipelineActive);
        Assert.Equal(
            ["feature.early", "feature.alpha", "feature.zeta"],
            active.Select(feature => feature.Id));
        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new TestFeature("feature.late", 0), "feature.late", 0));
        Assert.Equal(
            "[GenericRP.Features] Cannot register feature 'feature.late' after pipeline activation. " +
            "Register features during package OnLoad before RenderSubsystem initialization.",
            exception.Message);
    }

    [Fact]
    public void DuplicateFeatureIdIsRejectedWithStableDiagnostic()
    {
        var registry = new GenericRenderPipelineFeatureRegistryCore<TestFeature>();
        registry.Register(new TestFeature("feature.duplicate", 0), "feature.duplicate", 0);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new TestFeature("feature.duplicate", 1), "feature.duplicate", 1));

        Assert.Equal(
            "[GenericRP.Features] Feature ID 'feature.duplicate' is already registered.",
            exception.Message);
    }

    [Fact]
    public void UnregisterIsIdempotentAfterActivationEnds()
    {
        var registry = new GenericRenderPipelineFeatureRegistryCore<TestFeature>();
        var feature = new TestFeature("feature.lifecycle", 0);
        registry.Register(feature, feature.Id, feature.Order);
        registry.BeginPipelineActivation();

        var activeException = Assert.Throws<InvalidOperationException>(() =>
            registry.Unregister(feature, feature.Id));
        Assert.Equal(
            "[GenericRP.Features] Cannot unregister feature 'feature.lifecycle' while the pipeline is active.",
            activeException.Message);

        registry.EndPipelineActivation();

        Assert.False(registry.IsPipelineActive);
        Assert.True(registry.Unregister(feature, feature.Id));
        Assert.False(registry.Unregister(feature, feature.Id));
        Assert.Equal(0, registry.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" feature.leading")]
    [InlineData("feature.trailing ")]
    public void InvalidFeatureIdIsRejected(string featureId)
    {
        var registry = new GenericRenderPipelineFeatureRegistryCore<TestFeature>();

        var exception = Assert.Throws<ArgumentException>(() =>
            registry.Register(new TestFeature(featureId, 0), featureId, 0));

        Assert.StartsWith(
            "[GenericRP.Features] Feature ID must be non-empty",
            exception.Message,
            StringComparison.Ordinal);
    }

    private sealed record TestFeature(string Id, int Order);
}
