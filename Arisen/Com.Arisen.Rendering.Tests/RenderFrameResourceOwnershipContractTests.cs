using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderFrameResourceOwnershipContractTests
{
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

        Assert.Contains("SwapChainFrameIndex = swapChainFrameIndex", managedDeviceSource, StringComparison.Ordinal);
        Assert.Contains("desc.SwapChainFrameIndex = bridgeDesc->swapChainFrameIndex;", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("public uint SwapChainFrameIndex;", generatedBridgeSource, StringComparison.Ordinal);
        Assert.Contains("UInt32 SwapChainFrameIndex", queueContractSource, StringComparison.Ordinal);
        Assert.Contains("descriptor->SwapChainFrameIndex", vulkanQueueSource, StringComparison.Ordinal);
        Assert.Contains("GetImageAvailableSemaphore(swapChainFrameIndex)", vulkanQueueSource, StringComparison.Ordinal);
        Assert.Contains("GetRenderFinishSemaphore(swapChainFrameIndex)", vulkanQueueSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetImageAvailableSemaphore(resourceFrameIndex)", vulkanQueueSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRenderFinishSemaphore(resourceFrameIndex)", vulkanQueueSource, StringComparison.Ordinal);
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
