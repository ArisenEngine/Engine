using ArisenEditorFramework.Extensions;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class EditorExtensionRegistryTests
{
    [Fact]
    public void ActivationWithoutOptionalExtensionsProducesAnEmptyFrozenSet()
    {
        var registry = new EditorExtensionRegistryCore<TestExtension>();

        var active = registry.BeginEditorActivation();

        Assert.Empty(active);
        Assert.True(registry.IsEditorActive);
        registry.EndEditorActivation();
        Assert.False(registry.IsEditorActive);
    }

    [Fact]
    public void ActivationFreezesExtensionsInDeterministicOrderAndRejectsLateRegistration()
    {
        var registry = new EditorExtensionRegistryCore<TestExtension>();
        var later = new TestExtension("extension.zeta", 20);
        var earlier = new TestExtension("extension.early", 10);
        var sameOrder = new TestExtension("extension.alpha", 20);
        registry.Register(later, later.Id, later.Order);
        registry.Register(earlier, earlier.Id, earlier.Order);
        registry.Register(sameOrder, sameOrder.Id, sameOrder.Order);

        var active = registry.BeginEditorActivation();

        Assert.Equal(
            ["extension.early", "extension.alpha", "extension.zeta"],
            active.Select(extension => extension.Id));
        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new TestExtension("extension.late", 0), "extension.late", 0));
        Assert.Equal(
            "[Editor.Extensions] Cannot register extension 'extension.late' after Editor activation. " +
            "Register extensions during package OnLoad before the Editor host starts.",
            exception.Message);
    }

    [Fact]
    public void DuplicateExtensionIdIsRejectedWithStableDiagnostic()
    {
        var registry = new EditorExtensionRegistryCore<TestExtension>();
        registry.Register(new TestExtension("extension.duplicate", 0), "extension.duplicate", 0);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new TestExtension("extension.duplicate", 1), "extension.duplicate", 1));

        Assert.Equal(
            "[Editor.Extensions] Extension ID 'extension.duplicate' is already registered.",
            exception.Message);
    }

    [Fact]
    public void UnregisterIsIdempotentAfterEditorActivationEnds()
    {
        var registry = new EditorExtensionRegistryCore<TestExtension>();
        var extension = new TestExtension("extension.lifecycle", 0);
        registry.Register(extension, extension.Id, extension.Order);
        registry.BeginEditorActivation();

        var activeException = Assert.Throws<InvalidOperationException>(() =>
            registry.Unregister(extension, extension.Id));
        Assert.Equal(
            "[Editor.Extensions] Cannot unregister extension 'extension.lifecycle' while the Editor is active.",
            activeException.Message);

        registry.EndEditorActivation();

        Assert.True(registry.Unregister(extension, extension.Id));
        Assert.False(registry.Unregister(extension, extension.Id));
        Assert.Equal(0, registry.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" extension.leading")]
    [InlineData("extension.trailing ")]
    public void InvalidExtensionIdIsRejected(string extensionId)
    {
        var registry = new EditorExtensionRegistryCore<TestExtension>();

        var exception = Assert.Throws<ArgumentException>(() =>
            registry.Register(new TestExtension(extensionId, 0), extensionId, 0));

        Assert.StartsWith(
            "[Editor.Extensions] Extension ID must be non-empty",
            exception.Message,
            StringComparison.Ordinal);
    }

    private sealed record TestExtension(string Id, int Order);
}
