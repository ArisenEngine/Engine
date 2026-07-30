using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderResourceDisposalContractTests
{
    [Fact]
    public void DeferredDisposalUsesNonBlockingCompletedTicketSweepDuringFrames()
    {
        var queueSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/DeferredRenderResourceDisposalQueue.cs");
        var pipelineSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");
        var deviceBridgeSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core.native/Source/Core.RHI/Bridges/RHIDeviceBridge.cpp");

        Assert.Contains("public void ReleaseCompleted(RHIDevice device, ulong deviceGeneration)", queueSource, StringComparison.Ordinal);
        Assert.Contains("BindDevice(device, deviceGeneration);", queueSource, StringComparison.Ordinal);
        Assert.Contains("device.GetCompletedTicket()", queueSource, StringComparison.Ordinal);
        Assert.Contains("queue->Update();", deviceBridgeSource, StringComparison.Ordinal);
        Assert.Contains("return queue->GetCompletedTicket();", deviceBridgeSource, StringComparison.Ordinal);
        Assert.Contains("m_DisposalQueue.ReleaseCompleted(", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("context.DeviceGeneration", pipelineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("m_DisposalQueue.Drain(context.Device);", pipelineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DeferredDisposalKeepsBlockingDrainForPipelineTeardown()
    {
        var queueSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/DeferredRenderResourceDisposalQueue.cs");
        var pipelineSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");

        Assert.Contains("public void Drain(", queueSource, StringComparison.Ordinal);
        Assert.Contains("ulong submittedThroughTicket", queueSource, StringComparison.Ordinal);
        Assert.Contains("ValidateDrainBoundary", queueSource, StringComparison.Ordinal);
        Assert.Contains("device.WaitQueueTicket(maximumPendingTicket);", queueSource, StringComparison.Ordinal);
        Assert.Contains("m_DisposalQueue.Drain(", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("m_DeviceGeneration", pipelineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BackendRestartDrainsResidencyInvalidationsBeforeReleasingGeneration()
    {
        var providerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipelineProvider.cs");
        var preparedProviderSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericPreparedAssetProvider.cs");
        var queueSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/DeferredRenderResourceDisposalQueue.cs");

        int invalidation = providerSource.IndexOf(
            "m_ResidencyService.InvalidatePreparedProvider(",
            StringComparison.Ordinal);
        int release = providerSource.IndexOf(
            "m_PreparedAssetProvider.ReleaseAllDeviceResources();",
            StringComparison.Ordinal);

        Assert.True(invalidation >= 0);
        Assert.True(release > invalidation);
        Assert.Contains("m_Device.WaitQueueTicket(m_LastSubmittedTicket);", preparedProviderSource, StringComparison.Ordinal);
        Assert.Contains("m_DisposalQueue.ReleaseDevice(", preparedProviderSource, StringComparison.Ordinal);
        Assert.Contains("m_State.Unbind", queueSource, StringComparison.Ordinal);
    }

    [Fact]
    public void EditorViewportResizeRejectsPartialSwapchainsAndCoalescesAtRenderBoundary()
    {
        var commandQueueSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RHICommandQueue.cs");
        var viewportSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.editor/Managed/Core/Views/ArisenViewportControl.cs");
        var swapChainSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Presentation/RHIVkSwapChain.cpp");
        var surfaceBridgeSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core.native/Source/Core.RHI/Bridges/RHISurfaceBridge.cpp");
        var renderSurfaceSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderSurface.cs");
        var deviceSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Core/RHIVkDevice.cpp");

        Assert.Contains("Dictionary<IntPtr, ResizeSurfaceCommand>? pendingResizes", commandQueueSource, StringComparison.Ordinal);
        Assert.Contains("pendingResizes[resize.Host] = resize;", commandQueueSource, StringComparison.Ordinal);
        Assert.Contains("ExecutePendingResizes", commandQueueSource, StringComparison.Ordinal);

        Assert.DoesNotContain("SurfaceResizeStabilizationInterval", viewportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_surfaceResizeTimer", viewportSource, StringComparison.Ordinal);
        Assert.Contains("ProcessSurfaceResizesAsync", viewportSource, StringComparison.Ordinal);
        Assert.Contains("await _activePresentationTask;", viewportSource, StringComparison.Ordinal);
        Assert.Contains("await resizeTask;", viewportSource, StringComparison.Ordinal);
        Assert.Contains("await ClearImportedResourceCacheAsync();", viewportSource, StringComparison.Ordinal);
        Assert.Contains("await renderSubsystem.ResizeSurfaceAsync", viewportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_resourceReleaseFailed = false", viewportSource, StringComparison.Ordinal);
        Assert.Contains("resize.AbsorbCompletions", commandQueueSource, StringComparison.Ordinal);

        Assert.Contains("if (!imageHandle.IsValid())", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("if (!viewHandle.IsValid())", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("if (sharedHandle == nullptr)", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("if (!m_LastCreationSucceeded)", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("TrySetResolution", surfaceBridgeSource, StringComparison.Ordinal);
        Assert.Contains("RHISurface_TrySetResolution", renderSurfaceSource, StringComparison.Ordinal);
        Assert.Contains("the previous native swapchain remains active", renderSurfaceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("for (auto h : m_SharedHandles)", swapChainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("handles = std::move(oldSharedHandles)", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("AcknowledgeExternalConsumerRelease", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("GetLatestTicket() : 0", swapChainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetLatestTicket() +", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("if (!image) return;", deviceSource, StringComparison.Ordinal);
    }

    [Fact]
    public void EditorViewportSemaphoreHandshakeWaitsForExternalConsumption()
    {
        var surfaceSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderSurface.cs");
        var viewportSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.editor/Managed/Core/Views/ArisenViewportControl.cs");
        var swapChainSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Presentation/RHIVkSwapChain.cpp");
        var queueSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Queues/RHIVkQueue.cpp");

        Assert.Contains("EditorSharedTextureMaxOutstandingFrames = 3", surfaceSource, StringComparison.Ordinal);
        Assert.Contains("Queue<PendingRenderOutput> m_PendingOutputs", surfaceSource, StringComparison.Ordinal);
        Assert.Contains("Dictionary<IntPtr, RHISwapChain> m_ConsumedSemaphoreOwners", surfaceSource, StringComparison.Ordinal);
        Assert.Contains("m_PendingOutputs.Count >= EditorSharedTextureMaxOutstandingFrames", surfaceSource, StringComparison.Ordinal);
        Assert.Contains("var pending = m_PendingOutputs.Peek();", surfaceSource, StringComparison.Ordinal);
        Assert.Contains("m_PendingOutputs.Dequeue();", surfaceSource, StringComparison.Ordinal);
        Assert.Contains("swapChain.GetRenderFinishedSemaphoreWin32Handle(frameIndex)", surfaceSource, StringComparison.Ordinal);
        Assert.Contains("await surface.UpdateWithSemaphoresAsync", viewportSource, StringComparison.Ordinal);
        Assert.Contains("await semaphore.ImportCompleted;", viewportSource, StringComparison.Ordinal);
        Assert.Contains("_semaphoreCache", viewportSource, StringComparison.Ordinal);
        Assert.Contains("GetOrImportSemaphore", viewportSource, StringComparison.Ordinal);
        Assert.Contains("await ClearImportedResourceCacheAsync();", viewportSource, StringComparison.Ordinal);
        Assert.Contains("await DisposeImportedSemaphoreAsync(semaphore);", viewportSource, StringComparison.Ordinal);
        Assert.Contains("CompleteConsumedSemaphore(info.SignalSemaphoreHandle);", viewportSource, StringComparison.Ordinal);
        Assert.Contains("ReleaseConsumedSemaphore(info.SignalSemaphoreHandle);", viewportSource, StringComparison.Ordinal);

        Assert.DoesNotContain("auto nextProducer = factory->CreateSemaphore();", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("m_ImageAvailableSemaphoreSharedHandles[currentFrame]", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("m_RenderFinishSemaphoreSharedHandles[currentFrame]", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("synchronization.consumerUpdateQueued", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("synchronization.preparedForReuse", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("replacementConsumerSemaphores", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("replacementProducerSemaphores", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("CompleteConsumedSemaphoreWin32Handle", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("GetExternalConsumerWaitSemaphore", queueSource, StringComparison.Ordinal);
        Assert.Contains("VK_PIPELINE_STAGE_ALL_COMMANDS_BIT", queueSource, StringComparison.Ordinal);
        Assert.Contains("NotifyFrameSubmitted(swapChainFrameIndex, submitTicket)", queueSource, StringComparison.Ordinal);
    }

    [Fact]
    public void EditorViewportContextLossCannotStartTimedRecreationLoop()
    {
        var viewportSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.editor/Managed/Core/Views/ArisenViewportControl.cs");
        var smokeSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.editor/Managed/Core/Validation/EditorViewportSmokeSession.cs");
        var smokeStateSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.editor/Managed/Core/Validation/EditorViewportSmokeState.cs");

        Assert.DoesNotContain("_lastRecoveryTime", viewportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TotalMilliseconds", viewportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RecoverContext", viewportSource, StringComparison.Ordinal);
        Assert.Contains("entering a fail-stop state", viewportSource, StringComparison.Ordinal);
        Assert.Contains("brush.IsChecked = false;", smokeSource, StringComparison.Ordinal);
        Assert.Contains("paint.IsChecked = true;", smokeSource, StringComparison.Ordinal);
        Assert.Contains("RequiredConcurrentFramesPerViewport = 320", smokeStateSource, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Arisen")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
