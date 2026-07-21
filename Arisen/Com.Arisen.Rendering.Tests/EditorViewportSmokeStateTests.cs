using ArisenEditor.Core.Validation;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class EditorViewportSmokeStateTests
{
    [Fact]
    public void ObserveCompletesSceneResizeAndGameSequence()
    {
        var state = new EditorViewportSmokeState();

        Assert.Equal(
            EditorViewportSmokeAction.ResizeSceneView,
            state.Observe(CreateObservation(EditorViewportKind.SceneView, 1, 1, 640, 360)));
        Assert.Equal(
            EditorViewportSmokeAction.None,
            state.Observe(CreateObservation(EditorViewportKind.SceneView, 2, 1, 640, 360)));
        Assert.Equal(
            EditorViewportSmokeAction.ShowGameView,
            state.Observe(CreateObservation(EditorViewportKind.SceneView, 3, 2, 800, 450)));

        state.NotifyGameViewActivated();
        Assert.Equal(
            EditorViewportSmokeAction.None,
            state.Observe(CreateObservation(EditorViewportKind.GameView, 4, 1, 800, 450)));

        Guid worldGuid = Guid.Parse("93000000-0000-0000-0000-000000000001");
        Guid cellId = Guid.Parse("93000000-0000-0000-0000-000000000002");
        state.ObserveWorldFirstOpen(worldGuid, 2, cellId);
        state.NotifyWorldCellLoadRequested(cellId);
        state.ObserveWorldCellActive(cellId);
        state.NotifyWorldCellUnloadRequested(cellId);
        Assert.True(state.ObserveWorldCellUnloaded(cellId));

        var artifact = state.CreateArtifact("Editor", 30);
        Assert.True(state.Succeeded);
        Assert.True(artifact.Passed);
        Assert.True(artifact.Checks.ScenePresentedBeforeGameViewActivation);
        Assert.True(artifact.Checks.SceneResizeGenerationAdvanced);
        Assert.True(artifact.Checks.GameOrientationCorrect);
        Assert.True(artifact.Checks.WorldVisibleOnFirstOpen);
        Assert.True(artifact.Checks.WorldCellLoadObserved);
        Assert.True(artifact.Checks.WorldCellUnloadObserved);
        Assert.Equal(2, artifact.WorldPartition!.CellCount);
    }

    [Fact]
    public void ObserveRejectsGameViewBeforeSceneView()
    {
        var state = new EditorViewportSmokeState();

        var action = state.Observe(CreateObservation(EditorViewportKind.GameView, 1, 1, 640, 360));

        Assert.Equal(EditorViewportSmokeAction.Failed, action);
        Assert.Contains("before", state.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ObserveRejectsMissingConsumptionReport()
    {
        var state = new EditorViewportSmokeState();
        var observation = CreateObservation(EditorViewportKind.SceneView, 1, 1, 640, 360) with
        {
            ConsumptionReported = false
        };

        var action = state.Observe(observation);

        Assert.Equal(EditorViewportSmokeAction.Failed, action);
        Assert.Contains("consumed", state.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ObserveRejectsIncorrectVulkanCompositorFlip()
    {
        var state = new EditorViewportSmokeState();
        var observation = CreateObservation(EditorViewportKind.SceneView, 1, 1, 640, 360) with
        {
            PresentationScaleY = 1.0f
        };

        var action = state.Observe(observation);

        Assert.Equal(EditorViewportSmokeAction.Failed, action);
        Assert.Contains("transform", state.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static EditorViewportPresentationObservation CreateObservation(
        EditorViewportKind kind,
        uint frameIndex,
        uint resizeGeneration,
        uint width,
        uint height)
    {
        return new EditorViewportPresentationObservation(
            kind,
            Ticket: frameIndex + 100,
            FrameIndex: frameIndex,
            ResizeGeneration: resizeGeneration,
            Width: width,
            Height: height,
            LastConsumedFrameIndex: frameIndex,
            ConsumptionReported: true,
            RequiresVerticalFlip: true,
            PresentationScaleX: 1.0f,
            PresentationScaleY: -1.0f,
            PresentationCenterX: width * 0.5f,
            PresentationCenterY: height * 0.5f,
            VisualWidth: width,
            VisualHeight: height);
    }
}
