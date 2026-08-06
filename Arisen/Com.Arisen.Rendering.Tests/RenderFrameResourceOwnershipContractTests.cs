using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderFrameResourceOwnershipContractTests
{
    [Fact]
    public void DeviceFrameSlotIsReservedBeforeSwapChainAcquire()
    {
        string subsystemSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderSubsystem.cs");

        int reservationIndex = subsystemSource.IndexOf(
            "RenderFrameResourceReservation frameResource = AcquireFrameResourceSlot(",
            StringComparison.Ordinal);
        int acquireIndex = subsystemSource.IndexOf(
            "if (!submission.Begin(",
            StringComparison.Ordinal);
        int failedAcquireCancellationIndex = subsystemSource.IndexOf(
            "m_FrameResourceSlots.Cancel(frameResource);",
            acquireIndex,
            StringComparison.Ordinal);

        Assert.True(reservationIndex >= 0, "RenderSubsystem must reserve a device frame slot.");
        Assert.True(acquireIndex > reservationIndex,
            "The previous device-frame ticket must complete before native swapchain acquire.");
        Assert.True(failedAcquireCancellationIndex > acquireIndex,
            "A native acquire skip must cancel its unused device-frame reservation.");
    }

    [Fact]
    public void MultiSurfaceRenderingSeparatesOutputAndDeviceFrameIndices()
    {
        string subsystemSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderSubsystem.cs");
        string graphSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderGraph.cs");
        string staticMeshSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/StaticMeshPass.cs");
        string shadowSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/DirectionalShadowFrameData.cs");
        string environmentSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/EnvironmentFrameData.cs");

        Assert.Contains("AcquireFrameResourceSlot(", subsystemSource, StringComparison.Ordinal);
        Assert.Contains("frameResource.SlotIndex", subsystemSource, StringComparison.Ordinal);
        Assert.Contains("device.WaitQueueTicket(reservation.PreviousTicket);", subsystemSource, StringComparison.Ordinal);
        Assert.Contains("GetCommandBuffer(context.FrameResourceIndex)", graphSource, StringComparison.Ordinal);
        Assert.Contains("context.FrameResourceIndex % (uint)m_ObjectDataRingSize", staticMeshSource, StringComparison.Ordinal);
        Assert.Contains("context.FrameResourceIndex % (uint)ringSize", shadowSource, StringComparison.Ordinal);
        Assert.Contains("context.FrameResourceIndex % (uint)ringSize", environmentSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SubmissionUsesSurfaceLocalIndexOnlyForSwapChainSynchronization()
    {
        string managedDeviceSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core/RHI/RHIDevice.cs");
        string bridgeSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core.native/Source/Core.RHI/Bridges/RHIDeviceBridge.cpp");
        string generatedBridgeSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core.native/Managed/Generated/RHI/RHISubmitDescriptor_Bridge.cs");
        string queueContractSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core.native/Source/Core.RHI/RHI/Queues/RHIQueue.h");
        string vulkanQueueSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Queues/RHIVkQueue.cpp");
        string vulkanSwapChainSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan/Presentation/RHIVkSwapChain.cpp");

        Assert.Contains("SwapChainFrameIndex = swapChainFrameIndex", managedDeviceSource, StringComparison.Ordinal);
        Assert.Contains("desc.SwapChainFrameIndex = bridgeDesc->swapChainFrameIndex;", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("public uint SwapChainFrameIndex;", generatedBridgeSource, StringComparison.Ordinal);
        Assert.Contains("UInt32 SwapChainFrameIndex", queueContractSource, StringComparison.Ordinal);
        Assert.Contains("descriptor->SwapChainFrameIndex", vulkanQueueSource, StringComparison.Ordinal);
        Assert.Contains("PrepareFrameSubmission(swapChainFrameIndex", vulkanQueueSource, StringComparison.Ordinal);
        Assert.Contains("CommitFrameSubmission(swapChainFrameIndex", vulkanQueueSource, StringComparison.Ordinal);
        Assert.Contains("m_ImageAvailableSemaphores[currentFrame]", vulkanSwapChainSource, StringComparison.Ordinal);
        Assert.Contains("m_ImageAvailableSemaphoreTickets[currentFrame]", vulkanSwapChainSource, StringComparison.Ordinal);
        Assert.Contains("m_RealPresentWaitSemaphores[imageIndex]", vulkanSwapChainSource, StringComparison.Ordinal);
        Assert.Contains("ResolveRenderFinishSemaphoreLocked(frameIndex)", vulkanSwapChainSource, StringComparison.Ordinal);
        Assert.Contains(
            "plan.signalSemaphore = ResolveRenderFinishSemaphoreLocked(frameIndex);",
            vulkanSwapChainSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "auto hSem = ResolveRenderFinishSemaphoreLocked(frameIndex);",
            vulkanSwapChainSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "plan.signalSemaphore = m_RenderFinishSemaphores[currentFrame]",
            vulkanSwapChainSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GetImageAvailableSemaphore(resourceFrameIndex)", vulkanQueueSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRenderFinishSemaphore(resourceFrameIndex)", vulkanQueueSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderGraphReturnsRecordedCommandBuffersToTheirOwningPools()
    {
        string graphSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderGraph.cs");

        Assert.Contains("private readonly struct RecordedCommandBufferLease", graphSource, StringComparison.Ordinal);
        Assert.Contains(
            "Pool.ReleaseCommandBuffer(FrameResourceIndex, CommandBuffer.RHIHandle);",
            graphSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "nodeCommandBuffers[capturedWorkItemIndex] = new RecordedCommandBufferLease(",
            graphSource,
            StringComparison.Ordinal);
        Assert.Contains("var releaseFailures = ReleaseRecordedCommandBuffers();", graphSource, StringComparison.Ordinal);
        Assert.Contains(
            "RenderGraph execution and command-buffer release both failed.",
            graphSource,
            StringComparison.Ordinal);
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
