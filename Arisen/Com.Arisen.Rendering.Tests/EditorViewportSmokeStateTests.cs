using ArisenEditor.Core.Validation;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class EditorViewportSmokeStateTests
{
    [Fact]
    public void ObserveCompletesSceneResizeAndGameSequence()
    {
        var state = new EditorViewportSmokeState();
        state.ObserveRenderDocAvailability(false);
        state.NotifyTerrainPaintAvailability(true);

        Assert.Equal(
            EditorViewportSmokeAction.ResizeSceneView,
            state.Observe(CreateObservation(EditorViewportKind.SceneView, 1, 1, 640, 360)));

        var resizeTargets = new (uint Width, uint Height)[]
        {
            (704, 396),
            (800, 450),
            (720, 405),
            (800, 450)
        };
        uint resizeFrameIndex = 2;
        uint resizeGeneration = 2;
        for (int index = 0; index < resizeTargets.Length; index++)
        {
            var target = resizeTargets[index];
            state.NotifySceneResizeRequested(
                target.Width,
                target.Height,
                target.Width,
                target.Height);

            if (index == 0)
            {
                Assert.Equal(
                    EditorViewportSmokeAction.None,
                    state.Observe(CreateObservation(
                        EditorViewportKind.SceneView,
                        resizeFrameIndex++,
                        1,
                        640,
                        360)));
            }

            if (index == resizeTargets.Length - 1)
            {
                Assert.Equal(
                    EditorViewportSmokeAction.None,
                    state.Observe(CreateObservation(
                        EditorViewportKind.SceneView,
                        resizeFrameIndex++,
                        3,
                        target.Width,
                        target.Height)));
            }

            var expectedAction = index == resizeTargets.Length - 1
                ? EditorViewportSmokeAction.ShowGameView
                : EditorViewportSmokeAction.ResizeSceneView;
            Assert.Equal(
                expectedAction,
                state.Observe(CreateObservation(
                    EditorViewportKind.SceneView,
                    resizeFrameIndex++,
                    resizeGeneration++,
                    target.Width,
                    target.Height)));
        }

        state.NotifyGameViewActivated();
        state.NotifyConcurrentViewportLayout(400, 450, 400, 450, 400, 450, 400, 450);
        Assert.Equal(
            EditorViewportSmokeAction.None,
            state.Observe(CreateObservation(EditorViewportKind.GameView, 5, 1, 800, 450)));
        state.NotifyTerrainPaintActivated();

        uint concurrentFrameIndex = 6;
        for (int index = 0;
             index < EditorViewportSmokeState.RequiredConcurrentFramesPerViewport - 1;
             index++)
        {
            Assert.Equal(
                EditorViewportSmokeAction.None,
                state.Observe(CreateObservation(
                    EditorViewportKind.SceneView,
                    concurrentFrameIndex++,
                    4,
                    400,
                    450)));
            Assert.Equal(
                EditorViewportSmokeAction.None,
                state.Observe(CreateObservation(
                    EditorViewportKind.GameView,
                    concurrentFrameIndex++,
                    2,
                    400,
                    450)));
        }
        Assert.Equal(
            EditorViewportSmokeAction.None,
            state.Observe(CreateObservation(
                EditorViewportKind.SceneView,
                concurrentFrameIndex++,
                4,
                400,
                450)));
        Assert.Equal(
            EditorViewportSmokeAction.FinishConcurrentPresentation,
            state.Observe(CreateObservation(
                EditorViewportKind.GameView,
                concurrentFrameIndex,
                2,
                400,
                450)));

        Guid worldGuid = Guid.Parse("93000000-0000-0000-0000-000000000001");
        Guid cellId = Guid.Parse("93000000-0000-0000-0000-000000000002");
        state.ObserveWorldFirstOpen(worldGuid, 2, cellId, 0, 0, 0);
        state.NotifyWorldCellLoadRequested(cellId);
        state.ObserveWorldCellActive(cellId);
        state.NotifyWorldCellUnloadRequested(cellId);
        Assert.True(state.ObserveWorldCellUnloaded(cellId));

        var artifact = state.CreateArtifact("Editor", 30);
        Assert.Equal(6, artifact.SchemaVersion);
        Assert.True(state.Succeeded);
        Assert.True(artifact.Passed);
        Assert.True(artifact.RenderDocAvailabilityObserved);
        Assert.False(artifact.RenderDocAvailableAtStartup);
        Assert.True(artifact.Checks.RenderDocStartupExpectationMet);
        Assert.True(artifact.Checks.RenderDocRestartExpectationMet);
        Assert.False(artifact.RenderDocRestartExpected);
        Assert.False(artifact.RenderDocRestartRequested);
        Assert.False(artifact.RenderDocRestartCompleted);
        Assert.Equal(0, artifact.PostRestartConcurrentSceneFrameCount);
        Assert.Equal(0, artifact.PostRestartConcurrentGameFrameCount);
        Assert.True(artifact.Checks.InteropResourceCachesBounded);
        Assert.Equal(EditorViewportSmokeState.RequiredImportedImagesPerViewport,
            artifact.MaxSceneImportedImageCount);
        Assert.Equal(EditorViewportSmokeState.RequiredImportedSemaphoresPerViewport,
            artifact.MaxSceneImportedSemaphoreCount);
        Assert.Equal(EditorViewportSmokeState.RequiredImportedImagesPerViewport,
            artifact.MaxGameImportedImageCount);
        Assert.Equal(EditorViewportSmokeState.RequiredImportedSemaphoresPerViewport,
            artifact.MaxGameImportedSemaphoreCount);
        Assert.True(artifact.Checks.ScenePresentedBeforeGameViewActivation);
        Assert.True(artifact.Checks.SceneResizeGenerationAdvanced);
        Assert.True(artifact.Checks.SceneResizeStressPassed);
        Assert.True(artifact.Checks.GameOrientationCorrect);
        Assert.True(artifact.Checks.ConcurrentSceneFramesPresented);
        Assert.True(artifact.Checks.ConcurrentGameFramesPresented);
        Assert.True(artifact.Checks.TerrainPaintInteractionPassed);
        Assert.Equal(EditorViewportSmokeState.RequiredConcurrentFramesPerViewport,
            artifact.ConcurrentSceneFrameCount);
        Assert.Equal(EditorViewportSmokeState.RequiredConcurrentFramesPerViewport,
            artifact.ConcurrentGameFrameCount);
        Assert.True(artifact.TerrainPaintAvailable);
        Assert.True(artifact.TerrainPaintActivated);
        Assert.Equal(EditorViewportSmokeState.RequiredSceneResizeTransitions,
            artifact.SceneResizeRequestCount);
        Assert.Equal(EditorViewportSmokeState.RequiredSceneResizeTransitions,
            artifact.SceneResizeTransitionCount);
        Assert.True(artifact.Checks.WorldVisibleOnFirstOpen);
        Assert.True(artifact.Checks.WorldOriginCellSelected);
        Assert.True(artifact.Checks.WorldCellLoadObserved);
        Assert.True(artifact.Checks.WorldCellUnloadObserved);
        Assert.Equal(2, artifact.WorldPartition!.CellCount);
    }

    [Fact]
    public void ObserveRequiresSustainedPresentationAfterRenderDocRestart()
    {
        var state = new EditorViewportSmokeState(expectRenderDocRestart: true);
        state.ObserveRenderDocAvailability(false);
        state.NotifyTerrainPaintAvailability(true);

        uint frameIndex = AdvanceToConcurrentCompletion(
            state,
            EditorViewportSmokeAction.RestartRenderDoc);
        Assert.Equal(
            EditorViewportSmokeAction.None,
            state.Observe(CreateObservation(
                EditorViewportKind.SceneView,
                frameIndex++,
                4,
                400,
                450)));

        state.NotifyRenderDocRestartRequested(previousGeneration: 7);
        state.ObserveRenderDocRestartCompleted(
            succeeded: true,
            previousGeneration: 7,
            currentGeneration: 8,
            renderDocAvailable: true,
            diagnostic: string.Empty);

        for (int index = 0;
             index < EditorViewportSmokeState.RequiredConcurrentFramesPerViewport - 1;
             index++)
        {
            Assert.Equal(
                EditorViewportSmokeAction.None,
                state.Observe(CreateObservation(
                    EditorViewportKind.SceneView,
                    frameIndex++,
                    1,
                    400,
                    450)));
            Assert.Equal(
                EditorViewportSmokeAction.None,
                state.Observe(CreateObservation(
                    EditorViewportKind.GameView,
                    frameIndex++,
                    1,
                    400,
                    450)));
        }
        Assert.Equal(
            EditorViewportSmokeAction.None,
            state.Observe(CreateObservation(
                EditorViewportKind.SceneView,
                frameIndex++,
                1,
                400,
                450)));
        Assert.Equal(
            EditorViewportSmokeAction.FinishConcurrentPresentation,
            state.Observe(CreateObservation(
                EditorViewportKind.GameView,
                frameIndex,
                1,
                400,
                450)));

        Guid worldGuid = Guid.Parse("93000000-0000-0000-0000-000000000011");
        Guid cellId = Guid.Parse("93000000-0000-0000-0000-000000000012");
        state.ObserveWorldFirstOpen(worldGuid, 2, cellId, 0, 0, 0);
        state.NotifyWorldCellLoadRequested(cellId);
        state.ObserveWorldCellActive(cellId);
        state.NotifyWorldCellUnloadRequested(cellId);
        Assert.True(state.ObserveWorldCellUnloaded(cellId));

        EditorViewportSmokeArtifact artifact = state.CreateArtifact("Editor", 120);
        Assert.True(artifact.Passed);
        Assert.True(artifact.RenderDocRestartExpected);
        Assert.True(artifact.RenderDocRestartRequested);
        Assert.True(artifact.RenderDocRestartCompleted);
        Assert.True(artifact.RenderDocAvailableAfterRestart);
        Assert.Equal((ulong)7, artifact.GraphicsGenerationBeforeRestart);
        Assert.Equal((ulong)8, artifact.GraphicsGenerationAfterRestart);
        Assert.Equal(
            EditorViewportSmokeState.RequiredConcurrentFramesPerViewport,
            artifact.PostRestartConcurrentSceneFrameCount);
        Assert.Equal(
            EditorViewportSmokeState.RequiredConcurrentFramesPerViewport,
            artifact.PostRestartConcurrentGameFrameCount);
        Assert.True(artifact.Checks.RenderDocRestartExpectationMet);
        Assert.True(artifact.Checks.PostRestartSceneFramesPresented);
        Assert.True(artifact.Checks.PostRestartGameFramesPresented);
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
    public void ObserveRejectsRenderDocDuringOrdinaryStartup()
    {
        var state = new EditorViewportSmokeState();

        state.ObserveRenderDocAvailability(true);

        Assert.True(state.IsComplete);
        Assert.False(state.Succeeded);
        Assert.Contains("RenderDoc", state.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ObserveAcceptsRenderDocWhenExpectedAtStartup()
    {
        var state = new EditorViewportSmokeState(expectRenderDocAtStartup: true);

        state.ObserveRenderDocAvailability(true);

        Assert.False(state.IsComplete);
        Assert.Null(state.FailureMessage);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 4)]
    [InlineData(3, 5)]
    public void ObserveRejectsInvalidImportedResourceCacheBounds(
        int importedImageCount,
        int importedSemaphoreCount)
    {
        var state = new EditorViewportSmokeState();
        var observation = CreateObservation(
            EditorViewportKind.SceneView,
            1,
            1,
            640,
            360) with
        {
            ImportedImageCount = importedImageCount,
            ImportedSemaphoreCount = importedSemaphoreCount
        };

        var action = state.Observe(observation);

        Assert.Equal(EditorViewportSmokeAction.Failed, action);
        Assert.Contains("cache", state.FailureMessage, StringComparison.OrdinalIgnoreCase);
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
            VisualHeight: height,
            SurfaceOwnershipGeneration: 1,
            SurfaceOwnershipOwnerId: $"test:{kind}",
            ImportedImageCount: 3,
            ImportedSemaphoreCount: 4);
    }

    private static uint AdvanceToConcurrentCompletion(
        EditorViewportSmokeState state,
        EditorViewportSmokeAction expectedFinalAction)
    {
        Assert.Equal(
            EditorViewportSmokeAction.ResizeSceneView,
            state.Observe(CreateObservation(EditorViewportKind.SceneView, 1, 1, 640, 360)));

        var resizeTargets = new (uint Width, uint Height)[]
        {
            (704, 396),
            (800, 450),
            (720, 405),
            (800, 450)
        };
        uint frameIndex = 2;
        uint generation = 2;
        for (int index = 0; index < resizeTargets.Length; index++)
        {
            var target = resizeTargets[index];
            state.NotifySceneResizeRequested(
                target.Width,
                target.Height,
                target.Width,
                target.Height);
            Assert.Equal(
                index == resizeTargets.Length - 1
                    ? EditorViewportSmokeAction.ShowGameView
                    : EditorViewportSmokeAction.ResizeSceneView,
                state.Observe(CreateObservation(
                    EditorViewportKind.SceneView,
                    frameIndex++,
                    generation++,
                    target.Width,
                    target.Height)));
        }

        state.NotifyGameViewActivated();
        state.NotifyConcurrentViewportLayout(400, 450, 400, 450, 400, 450, 400, 450);
        Assert.Equal(
            EditorViewportSmokeAction.None,
            state.Observe(CreateObservation(EditorViewportKind.GameView, frameIndex++, 1, 800, 450)));
        state.NotifyTerrainPaintActivated();

        for (int index = 0;
             index < EditorViewportSmokeState.RequiredConcurrentFramesPerViewport - 1;
             index++)
        {
            Assert.Equal(
                EditorViewportSmokeAction.None,
                state.Observe(CreateObservation(
                    EditorViewportKind.SceneView,
                    frameIndex++,
                    4,
                    400,
                    450)));
            Assert.Equal(
                EditorViewportSmokeAction.None,
                state.Observe(CreateObservation(
                    EditorViewportKind.GameView,
                    frameIndex++,
                    2,
                    400,
                    450)));
        }
        Assert.Equal(
            EditorViewportSmokeAction.None,
            state.Observe(CreateObservation(
                EditorViewportKind.SceneView,
                frameIndex++,
                4,
                400,
                450)));
        Assert.Equal(
            expectedFinalAction,
            state.Observe(CreateObservation(
                EditorViewportKind.GameView,
                frameIndex++,
                2,
                400,
                450)));
        return frameIndex;
    }
}
