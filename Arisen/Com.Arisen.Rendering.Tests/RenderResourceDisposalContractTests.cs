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
        var resourcePoolsSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Handles/RHIVkResourcePools.h");

        Assert.Contains("Dictionary<RenderSurfaceRegistration, ResizeSurfaceCommand>? pendingResizes", commandQueueSource, StringComparison.Ordinal);
        Assert.Contains("pendingResizes[resize.Registration] = resize;", commandQueueSource, StringComparison.Ordinal);
        Assert.Contains("pendingResizes.TryAdd(resize.Registration", commandQueueSource, StringComparison.Ordinal);
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
        Assert.Contains("const RHIGpuTicket retirementTicket = m_LastOwnedGraphicsTicket;", swapChainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetLatestTicket()", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("void* sharedHandle{nullptr};", resourcePoolsSource, StringComparison.Ordinal);
        Assert.Contains("RHIVkImageState::~RHIVkImageState()", deviceSource, StringComparison.Ordinal);
        Assert.Contains("if (sharedHandle != nullptr)", deviceSource, StringComparison.Ordinal);
        Assert.Contains("::CloseHandle(static_cast<HANDLE>(sharedHandle))", deviceSource, StringComparison.Ordinal);
        Assert.Contains("image->state->sharedHandle = win32Handle;", deviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CloseSharedWin32Handle", deviceSource, StringComparison.Ordinal);
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
        Assert.Contains("CompleteConsumedSemaphore(registration, info.SignalSemaphoreHandle);", viewportSource, StringComparison.Ordinal);
        Assert.Contains("ReleaseConsumedSemaphore(registration, info.SignalSemaphoreHandle);", viewportSource, StringComparison.Ordinal);

        Assert.DoesNotContain("auto nextProducer = factory->CreateSemaphore();", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("m_ImageAvailableSemaphoreSharedHandles[currentFrame]", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("m_RenderFinishSemaphoreSharedHandles[currentFrame]", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("synchronization.consumerUpdateQueued", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("synchronization.preparedForReuse", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("replacementConsumerSemaphores", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("replacementProducerSemaphores", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("CompleteConsumedSemaphoreWin32Handle", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("PrepareFrameSubmission", queueSource, StringComparison.Ordinal);
        Assert.Contains("appendSwapChainPlan", queueSource, StringComparison.Ordinal);
        Assert.Contains("plan.waitSemaphore = m_ImageAvailableSemaphores[currentFrame]", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("VK_PIPELINE_STAGE_ALL_COMMANDS_BIT", queueSource, StringComparison.Ordinal);
        Assert.Contains("CommitFrameSubmission(swapChainFrameIndex, submitTicket)", queueSource, StringComparison.Ordinal);
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

    [Fact]
    public void SurfaceReleaseCommitsNativeOwnershipBeforeManagedCacheRemoval()
    {
        var rhiSystemSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core/RHI/RHISystem.cs");
        var renderSurfaceSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderSurface.cs");
        var instanceSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Core/RHIVkInstance.cpp");
        var surfaceSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Presentation/RHIVkSurface.cpp");
        var swapChainSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Presentation/RHIVkSwapChain.cpp");

        int nativeDestroy = rhiSystemSource.IndexOf(
            "m_Instance.Value.DestroySurface(windowId);",
            StringComparison.Ordinal);
        int deviceRemoval = rhiSystemSource.IndexOf(
            "m_DeviceWrappers.TryRemove(windowId, out _);",
            nativeDestroy,
            StringComparison.Ordinal);
        int surfaceRemoval = rhiSystemSource.IndexOf(
            "m_SurfaceWrappers.TryRemove(windowId, out _);",
            nativeDestroy,
            StringComparison.Ordinal);

        Assert.True(nativeDestroy >= 0);
        Assert.True(deviceRemoval > nativeDestroy);
        Assert.True(surfaceRemoval > nativeDestroy);
        Assert.DoesNotContain("DeviceWaitIdle failed before removing surface", rhiSystemSource, StringComparison.Ordinal);

        Assert.Contains("bool releaseCommitted = false;", renderSurfaceSource, StringComparison.Ordinal);
        Assert.Contains("m_IsDisposed = releaseCommitted;", renderSurfaceSource, StringComparison.Ordinal);
        Assert.Contains("m_DisposeStarted = false;", renderSurfaceSource, StringComparison.Ordinal);

        int prepareForRelease = instanceSource.IndexOf("PrepareForRelease()", StringComparison.Ordinal);
        int nativeSurfaceReset = instanceSource.IndexOf("it->second.reset();", prepareForRelease, StringComparison.Ordinal);
        int nativeSurfaceErase = instanceSource.IndexOf("m_Surfaces.erase(it);", nativeSurfaceReset, StringComparison.Ordinal);
        Assert.True(prepareForRelease >= 0);
        Assert.True(nativeSurfaceReset > prepareForRelease);
        Assert.True(nativeSurfaceErase > nativeSurfaceReset);
        Assert.Contains(
            "Surface release did not commit; active frame, external-consumer lease, or GPU generation ownership remains",
            instanceSource,
            StringComparison.Ordinal);
        Assert.Contains("m_SwapChain->PrepareForSurfaceRelease()", surfaceSource, StringComparison.Ordinal);
        Assert.Contains("HasActiveFrameOwnershipLocked()", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("Refusing release while a frame is active", swapChainSource, StringComparison.Ordinal);
        Assert.Contains("Physical generation release remains incomplete", swapChainSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VulkanTerminalTeardownProvesCompletionBeforeDestroyingParents()
    {
        var instanceSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Core/RHIVkInstance.cpp");
        var deviceSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Core/RHIVkDevice.cpp");
        var swapChainSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Presentation/RHIVkSwapChain.cpp");

        int terminalDeviceProof = instanceSource.IndexOf(
            "EnsureTerminalCompletion()",
            StringComparison.Ordinal);
        int terminalSurfacePrepare = instanceSource.IndexOf(
            "PrepareForReleaseAfterTerminalCompletion()",
            terminalDeviceProof,
            StringComparison.Ordinal);
        int surfaceClear = instanceSource.IndexOf(
            "m_Surfaces.clear();",
            terminalSurfacePrepare,
            StringComparison.Ordinal);
        int deviceClear = instanceSource.IndexOf(
            "m_LogicalDevices.clear();",
            surfaceClear,
            StringComparison.Ordinal);
        Assert.True(terminalDeviceProof >= 0);
        Assert.True(terminalSurfacePrepare > terminalDeviceProof);
        Assert.True(surfaceClear > terminalSurfacePrepare);
        Assert.True(deviceClear > surfaceClear);
        Assert.Contains("std::terminate();", instanceSource, StringComparison.Ordinal);

        int deviceCompletionGate = deviceSource.IndexOf(
            "if (!HasTerminalCompletion() && !EnsureTerminalCompletion())",
            StringComparison.Ordinal);
        int registryShutdown = deviceSource.IndexOf(
            "m_ResourceRegistry->Shutdown();",
            deviceCompletionGate,
            StringComparison.Ordinal);
        int deferredFlush = deviceSource.IndexOf(
            "m_DeferredDeletion->Flush(RHIQueueType::Graphics, kAll);",
            registryShutdown,
            StringComparison.Ordinal);
        Assert.True(deviceCompletionGate >= 0);
        Assert.True(registryShutdown > deviceCompletionGate);
        Assert.True(deferredFlush > registryShutdown);

        int destructorStart = swapChainSource.IndexOf(
            "RHIVkSwapChain::~RHIVkSwapChain() noexcept",
            StringComparison.Ordinal);
        int destructorEnd = swapChainSource.IndexOf(
            "void ArisenEngine::RHI::RHIVkSwapChain::CreateSwapChainWithDesc",
            destructorStart,
            StringComparison.Ordinal);
        Assert.True(destructorStart >= 0);
        Assert.True(destructorEnd > destructorStart);
        string destructor = swapChainSource[destructorStart..destructorEnd];

        int physicalGuard = destructor.IndexOf("if (isPhysical)", StringComparison.Ordinal);
        int ownershipCheck = destructor.IndexOf(
            "HasPhysicalGenerationOwnershipLocked()",
            physicalGuard,
            StringComparison.Ordinal);
        int failStop = destructor.IndexOf("std::terminate();", ownershipCheck, StringComparison.Ordinal);
        int virtualCleanupGuard = destructor.IndexOf("if (!isPhysical)", failStop, StringComparison.Ordinal);
        int cleanup = destructor.IndexOf("Cleanup();", virtualCleanupGuard, StringComparison.Ordinal);
        Assert.True(physicalGuard >= 0);
        Assert.True(ownershipCheck > physicalGuard);
        Assert.True(failStop > ownershipCheck);
        Assert.True(virtualCleanupGuard > failStop);
        Assert.True(cleanup > virtualCleanupGuard);
        Assert.DoesNotContain("WaitIdleNoThrow()", destructor, StringComparison.Ordinal);
    }

    [Fact]
    public void VulkanQueueAndSwapchainSynchronizationUsesOneLockOrder()
    {
        var deviceSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Core/RHIVkDevice.cpp");
        var queueHeader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Queues/RHIVkQueue.h");
        var queueSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Queues/RHIVkQueue.cpp");
        var swapChainSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Presentation/RHIVkSwapChain.cpp");

        static string Method(string source, string start, string next)
        {
            int methodStart = source.IndexOf(start, StringComparison.Ordinal);
            int methodEnd = source.IndexOf(next, methodStart, StringComparison.Ordinal);
            Assert.True(methodStart >= 0);
            Assert.True(methodEnd > methodStart);
            return source[methodStart..methodEnd];
        }

        static void AssertQueueBeforeSwapchain(string method)
        {
            int queueLock = method.IndexOf(
                "queueLock(graphicsQueue->m_SubmitMutex)",
                StringComparison.Ordinal);
            int swapChainLock = method.IndexOf(
                "lock(m_Mutex)",
                queueLock,
                StringComparison.Ordinal);
            Assert.True(queueLock >= 0);
            Assert.True(swapChainLock > queueLock);
        }

        string acquire = Method(
            swapChainSource,
            "RHIVkSwapChain::AcquireCurrentImage(UInt32 frameIndex)",
            "RHIVkSwapChain::AcquireCurrentImageLocked");
        string resize = Method(
            swapChainSource,
            "RHIVkSwapChain::TrySetResolution(UInt32 width, UInt32 height)",
            "RHIVkSwapChain::TrySetResolutionLocked");
        string surfaceRelease = Method(
            swapChainSource,
            "RHIVkSwapChain::PrepareForSurfaceRelease()",
            "RHIVkSwapChain::PrepareForSurfaceReleaseAfterTerminalCompletion");
        AssertQueueBeforeSwapchain(acquire);
        AssertQueueBeforeSwapchain(resize);
        AssertQueueBeforeSwapchain(surfaceRelease);

        string publicWait = Method(
            queueSource,
            "RHIVkQueue::WaitForTicket(RHIGpuTicket ticket)",
            "RHIVkQueue::WaitForTicketUnderSubmitLock");
        Assert.Contains("Update();", publicWait, StringComparison.Ordinal);
        Assert.Contains("vkWaitSemaphores", publicWait, StringComparison.Ordinal);
        Assert.Contains("ticket > latestTicket", publicWait, StringComparison.Ordinal);
        Assert.DoesNotContain("m_SubmitMutex", publicWait, StringComparison.Ordinal);

        Assert.Contains(
            "std::shared_ptr<std::mutex> m_RawQueueMutex",
            queueHeader,
            StringComparison.Ordinal);
        Assert.Contains("rawQueueMutexes[queue]", deviceSource, StringComparison.Ordinal);
        Assert.Contains("GetQueueForVkHandle", deviceSource, StringComparison.Ordinal);
        Assert.Contains("rawQueueLock(*m_RawQueueMutex)", queueSource, StringComparison.Ordinal);
        Assert.Contains("PresentNoThrow(presentInfo)", swapChainSource, StringComparison.Ordinal);
        Assert.Equal(
            3,
            System.Text.RegularExpressions.Regex.Matches(
                swapChainSource,
                @"GetQueueForVkHandle\(\s*m_VkPresentQueue\)").Count);
        Assert.DoesNotContain(
            "GetQueue(RHIQueueType::Present)",
            swapChainSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NativeSubmitFaultFixtureIsSingleArmOneShotAndRecoversTickets()
    {
        var queueHeader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Queues/RHIVkQueue.h");
        var queueSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Queues/RHIVkQueue.cpp");
        var nativeTestSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native.test/RHI/Unit/RHIAbiContractTest.h");

        Assert.Contains("InjectNextSubmitResultForTesting", queueHeader, StringComparison.Ordinal);
        Assert.Contains("compare_exchange_strong", queueHeader, StringComparison.Ordinal);
        Assert.Contains("A submit failure is already pending", queueHeader, StringComparison.Ordinal);
        Assert.Contains(
            "m_InjectedSubmitResult.exchange(VK_SUCCESS, std::memory_order_acq_rel)",
            queueSource,
            StringComparison.Ordinal);
        Assert.Contains("duplicateSubmitFaultRejected", nativeTestSource, StringComparison.Ordinal);
        Assert.Contains("recoveredSubmitTicket", nativeTestSource, StringComparison.Ordinal);
        Assert.Contains(
            "queue->GetLatestTicket() != latestBeforeSubmit",
            nativeTestSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "queue->GetCommandBufferSubmitTicketForTesting(commandBuffer) != 0",
            nativeTestSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NativeCommandPoolRetirementInheritsAcceptedChildTickets()
    {
        var poolHeader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Commands/RHIVkCommandBufferPool.h");
        var poolSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Commands/RHIVkCommandBufferPool.cpp");
        var queueSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Queues/RHIVkQueue.cpp");
        var deviceSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Core/RHIVkDevice.cpp");
        var nativeTestSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native.test/RHI/Unit/RHIAbiContractTest.h");

        Assert.Contains(
            "std::atomic<RHIGpuTicket> m_AcceptedSubmitTickets[QUEUE_TYPE_COUNT]",
            poolHeader,
            StringComparison.Ordinal);
        Assert.Contains("compare_exchange_weak", poolSource, StringComparison.Ordinal);
        Assert.Contains("std::memory_order_release", poolSource, StringComparison.Ordinal);
        Assert.Contains("std::memory_order_acquire", poolHeader, StringComparison.Ordinal);

        int checkedQueueSubmit = queueSource.IndexOf(
            "vkQueueSubmit(m_Queue, 1, &submitInfo, VK_NULL_HANDLE)",
            StringComparison.Ordinal);
        int acceptedSubmit = queueSource.IndexOf(
            "ownerPool->RecordAcceptedSubmission(m_Type, submitTicket);",
            StringComparison.Ordinal);
        int latestTicketPublication = acceptedSubmit >= 0
            ? queueSource.IndexOf(
                "m_LatestTicket.store(submitTicket, std::memory_order_release);",
                acceptedSubmit,
                StringComparison.Ordinal)
            : -1;
        int frameSubmissionPublication = acceptedSubmit >= 0
            ? queueSource.IndexOf(
                "CommitFrameSubmission(swapChainFrameIndex, submitTicket)",
                acceptedSubmit,
                StringComparison.Ordinal)
            : -1;
        Assert.True(checkedQueueSubmit >= 0);
        Assert.True(acceptedSubmit > checkedQueueSubmit);
        Assert.True(latestTicketPublication > acceptedSubmit);
        Assert.True(frameSubmissionPublication > acceptedSubmit);

        int poolRelease = deviceSource.IndexOf(
            "RHIVkDevice::ReleaseCommandBufferPool",
            StringComparison.Ordinal);
        int registryTicketPublication = poolRelease >= 0
            ? deviceSource.IndexOf(
                "m_ResourceRegistry->UpdateTicket(",
                poolRelease,
                StringComparison.Ordinal)
            : -1;
        int registryOwnershipRelease = registryTicketPublication >= 0
            ? deviceSource.IndexOf(
                "ReleaseRegistryOwnership(*m_ResourceRegistry, item->registryHandle,",
                registryTicketPublication,
                StringComparison.Ordinal)
            : -1;
        Assert.True(poolRelease >= 0);
        Assert.True(registryTicketPublication > poolRelease);
        Assert.True(registryOwnershipRelease > registryTicketPublication);

        Assert.Contains("VerifyPendingCommandPoolRetirement()", nativeTestSource, StringComparison.Ordinal);
        Assert.Contains("registry->Retain(poolRegistryHandle)", nativeTestSource, StringComparison.Ordinal);
        Assert.Contains(
            "firstTicket = queue->Submit(firstCommand, &blockedSubmit);",
            nativeTestSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "secondTicket = queue->Submit(secondCommand);",
            nativeTestSource,
            StringComparison.Ordinal);
        Assert.Contains("queue->GetCompletedTicket() != baselineTicket", nativeTestSource, StringComparison.Ordinal);
        Assert.Contains("SignalSemaphoreValue(blockingSemaphore, 1)", nativeTestSource, StringComparison.Ordinal);
    }

    [Fact]
    public void FatalPhysicalPresentFailureEntersGenerationFailStopUntilTeardown()
    {
        var swapChainHeader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Presentation/RHIVkSwapChain.h");
        var swapChainSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Presentation/RHIVkSwapChain.cpp");
        var nativeTestSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native.test/RHI/Rendering/RHIBasicRenderingTest.h");

        Assert.Contains("m_InjectedPresentResult", swapChainHeader, StringComparison.Ordinal);
        Assert.Contains("m_TerminalPresentResult", swapChainHeader, StringComparison.Ordinal);
        Assert.Contains(
            "const VkResult injectedResult = std::exchange(m_InjectedPresentResult, VK_SUCCESS);",
            swapChainSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "Swapchain generation cannot acquire after terminal presentation failure",
            swapChainSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "Refusing generation reuse after terminal presentation failure",
            swapChainSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "InjectNextPresentResultForTesting(VK_ERROR_SURFACE_LOST_KHR)",
            nativeTestSource,
            StringComparison.Ordinal);
        Assert.Contains("duplicatePresentFaultRejected", nativeTestSource, StringComparison.Ordinal);
        Assert.Contains(
            "RHI::RHISwapChainFrameState::Retired",
            nativeTestSource,
            StringComparison.Ordinal);
        Assert.Contains("terminalAcquireRejected", nativeTestSource, StringComparison.Ordinal);
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
