using Arisen.Native.RHI;
using ArisenEngine.Core.RHI;
using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderFrameColorOutputPolicyTests
{
    [Fact]
    public void FirstUseStartsFromUndefinedForEveryOutputKind()
    {
        foreach (RenderOutputKind outputKind in Enum.GetValues<RenderOutputKind>())
        {
            RenderFrameColorRhiState state = RenderFrameColorOutputPolicy.Resolve(
                outputKind,
                targetImageRequiresInitialization: true,
                isSource: true);

            Assert.Equal(EImageLayout.IMAGE_LAYOUT_UNDEFINED, state.Layout);
            Assert.Equal(EAccessFlag.ACCESS_NONE, state.Access);
            Assert.Equal(RHIQueueFamily.Ignored, state.QueueFamily);
            Assert.Equal(EPipelineStageFlagBits.PIPELINE_STAGE_TOP_OF_PIPE_BIT, state.Stage);
        }
    }

    [Fact]
    public void InitializedNativeSwapchainStartsAndFinishesInPresentLayout()
    {
        RenderFrameColorRhiState source = ResolveInitialized(
            RenderOutputKind.NativeSwapchain,
            isSource: true);
        RenderFrameColorRhiState destination = ResolveInitialized(
            RenderOutputKind.NativeSwapchain,
            isSource: false);

        Assert.Equal(EImageLayout.IMAGE_LAYOUT_PRESENT_SRC_KHR, source.Layout);
        Assert.Equal(EImageLayout.IMAGE_LAYOUT_PRESENT_SRC_KHR, destination.Layout);
        Assert.Equal(EAccessFlag.ACCESS_NONE, source.Access);
        Assert.Equal(EAccessFlag.ACCESS_NONE, destination.Access);
        Assert.Equal(RHIQueueFamily.Ignored, source.QueueFamily);
        Assert.Equal(RHIQueueFamily.Ignored, destination.QueueFamily);
        Assert.Equal(EPipelineStageFlagBits.PIPELINE_STAGE_TOP_OF_PIPE_BIT, source.Stage);
        Assert.Equal(EPipelineStageFlagBits.PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT, destination.Stage);
    }

    [Fact]
    public void InitializedEditorSharedTextureTransfersExternalOwnershipInTransferSourceLayout()
    {
        RenderFrameColorRhiState source = ResolveInitialized(
            RenderOutputKind.EditorSharedTexture,
            isSource: true);
        RenderFrameColorRhiState destination = ResolveInitialized(
            RenderOutputKind.EditorSharedTexture,
            isSource: false);

        Assert.Equal(EImageLayout.IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, source.Layout);
        Assert.Equal(EImageLayout.IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, destination.Layout);
        Assert.Equal(EAccessFlag.ACCESS_NONE, source.Access);
        Assert.Equal(EAccessFlag.ACCESS_NONE, destination.Access);
        Assert.Equal(RHIQueueFamily.External, source.QueueFamily);
        Assert.Equal(RHIQueueFamily.External, destination.QueueFamily);
        Assert.Equal(EPipelineStageFlagBits.PIPELINE_STAGE_TOP_OF_PIPE_BIT, source.Stage);
        Assert.Equal(EPipelineStageFlagBits.PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT, destination.Stage);
    }

    [Fact]
    public void InitializedOffscreenOutputRemainsTransferReadableWithoutOwnershipTransfer()
    {
        RenderFrameColorRhiState source = ResolveInitialized(
            RenderOutputKind.Offscreen,
            isSource: true);
        RenderFrameColorRhiState destination = ResolveInitialized(
            RenderOutputKind.Offscreen,
            isSource: false);

        Assert.Equal(EImageLayout.IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, source.Layout);
        Assert.Equal(EImageLayout.IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, destination.Layout);
        Assert.Equal(EAccessFlag.ACCESS_TRANSFER_READ_BIT, source.Access);
        Assert.Equal(EAccessFlag.ACCESS_TRANSFER_READ_BIT, destination.Access);
        Assert.Equal(RHIQueueFamily.Ignored, source.QueueFamily);
        Assert.Equal(RHIQueueFamily.Ignored, destination.QueueFamily);
        Assert.Equal(EPipelineStageFlagBits.PIPELINE_STAGE_TRANSFER_BIT, source.Stage);
        Assert.Equal(EPipelineStageFlagBits.PIPELINE_STAGE_TRANSFER_BIT, destination.Stage);
    }

    private static RenderFrameColorRhiState ResolveInitialized(
        RenderOutputKind outputKind,
        bool isSource)
    {
        return RenderFrameColorOutputPolicy.Resolve(
            outputKind,
            targetImageRequiresInitialization: false,
            isSource);
    }
}
