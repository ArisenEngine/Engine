using ArisenEditor.Core.Validation;
using ArisenEditor.Views;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class EditorViewportSurfaceOwnershipTests
{
    [Fact]
    public async Task ReplacementWaitsUntilPriorViewportReleasesOwnership()
    {
        var ownership = new EditorViewportSurfaceOwnership();
        EditorViewportSurfaceLease first = await ownership.AcquireAsync(
            EditorViewportKind.SceneView,
            "scene:first");

        Task<EditorViewportSurfaceLease> replacementTask = ownership.AcquireAsync(
            EditorViewportKind.SceneView,
            "scene:replacement").AsTask();

        Assert.False(replacementTask.IsCompleted);
        EditorViewportSurfaceOwnershipSnapshot held = ownership.GetSnapshot(
            EditorViewportKind.SceneView);
        Assert.True(held.IsOwned);
        Assert.Equal("scene:first", held.OwnerId);

        first.Dispose();
        using EditorViewportSurfaceLease replacement = await replacementTask;

        Assert.True(replacement.Generation > first.Generation);
        EditorViewportSurfaceOwnershipSnapshot transferred = ownership.GetSnapshot(
            EditorViewportKind.SceneView);
        Assert.True(transferred.IsOwned);
        Assert.Equal(replacement.Generation, transferred.Generation);
        Assert.Equal("scene:replacement", transferred.OwnerId);
    }

    [Fact]
    public async Task SceneAndGameViewportOwnershipRemainIndependent()
    {
        var ownership = new EditorViewportSurfaceOwnership();
        using EditorViewportSurfaceLease scene = await ownership.AcquireAsync(
            EditorViewportKind.SceneView,
            "scene");
        using EditorViewportSurfaceLease game = await ownership.AcquireAsync(
            EditorViewportKind.GameView,
            "game");

        Assert.True(ownership.GetSnapshot(EditorViewportKind.SceneView).IsOwned);
        Assert.True(ownership.GetSnapshot(EditorViewportKind.GameView).IsOwned);
        Assert.Equal(EditorViewportKind.SceneView, scene.ViewportKind);
        Assert.Equal(EditorViewportKind.GameView, game.ViewportKind);
    }

    [Fact]
    public async Task CancelledReplacementDoesNotConsumeOwnership()
    {
        var ownership = new EditorViewportSurfaceOwnership();
        EditorViewportSurfaceLease first = await ownership.AcquireAsync(
            EditorViewportKind.SceneView,
            "scene:first");
        using var cancellation = new CancellationTokenSource();
        Task<EditorViewportSurfaceLease> cancelledTask = ownership.AcquireAsync(
            EditorViewportKind.SceneView,
            "scene:cancelled",
            cancellation.Token).AsTask();

        Assert.False(cancelledTask.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledTask);

        EditorViewportSurfaceOwnershipSnapshot held = ownership.GetSnapshot(
            EditorViewportKind.SceneView);
        Assert.Equal("scene:first", held.OwnerId);
        Assert.Equal(first.Generation, held.Generation);

        first.Dispose();
        using EditorViewportSurfaceLease next = await ownership.AcquireAsync(
            EditorViewportKind.SceneView,
            "scene:next");
        Assert.Equal("scene:next", ownership.GetSnapshot(
            EditorViewportKind.SceneView).OwnerId);
    }

    [Fact]
    public async Task StaleOrRepeatedReleaseCannotUnlockNewerOwner()
    {
        var ownership = new EditorViewportSurfaceOwnership();
        EditorViewportSurfaceLease first = await ownership.AcquireAsync(
            EditorViewportKind.SceneView,
            "scene:first");
        first.Dispose();

        EditorViewportSurfaceLease second = await ownership.AcquireAsync(
            EditorViewportKind.SceneView,
            "scene:second");
        first.Dispose();
        Task<EditorViewportSurfaceLease> thirdTask = ownership.AcquireAsync(
            EditorViewportKind.SceneView,
            "scene:third").AsTask();

        Assert.False(thirdTask.IsCompleted);
        Assert.Equal(second.Generation, ownership.GetSnapshot(
            EditorViewportKind.SceneView).Generation);

        second.Dispose();
        using EditorViewportSurfaceLease third = await thirdTask;
        Assert.True(third.Generation > second.Generation);
        Assert.Equal("scene:third", ownership.GetSnapshot(
            EditorViewportKind.SceneView).OwnerId);
    }
}
