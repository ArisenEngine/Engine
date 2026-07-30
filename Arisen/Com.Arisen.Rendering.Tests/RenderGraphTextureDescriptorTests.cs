using Arisen.Native.RHI;
using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderGraphTextureDescriptorTests
{
    [Fact]
    public void DepthAttachment2D_UsesDepthOnlyAllocationWithoutSamplingDescriptors()
    {
        var descriptor = RenderGraphTextureDescriptor.DepthAttachment2D(
            "FrameDepth",
            1920,
            1080,
            EFormat.FORMAT_D32_SFLOAT);

        Assert.Equal("FrameDepth", descriptor.DebugName);
        Assert.Equal(1920u, descriptor.Width);
        Assert.Equal(1080u, descriptor.Height);
        Assert.Equal(EFormat.FORMAT_D32_SFLOAT, descriptor.Format);
        Assert.Equal(
            (uint)EImageUsageFlagBits.IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT,
            descriptor.Usage);
        Assert.Equal(EImageAspectFlagBits.IMAGE_ASPECT_DEPTH_BIT, descriptor.AspectMask);
        Assert.False(descriptor.RegisterBindlessSampled);
        Assert.Equal(1u, descriptor.ArrayLayers);
    }

    [Fact]
    public void ColorAttachmentSampled2D_RetainsAttachmentAndSamplingCapabilities()
    {
        var descriptor = RenderGraphTextureDescriptor.ColorAttachmentSampled2D(
            "SceneColor",
            1280,
            720,
            EFormat.FORMAT_R16G16B16A16_SFLOAT);

        var expectedUsage =
            (uint)EImageUsageFlagBits.IMAGE_USAGE_COLOR_ATTACHMENT_BIT |
            (uint)EImageUsageFlagBits.IMAGE_USAGE_SAMPLED_BIT;
        Assert.Equal(expectedUsage, descriptor.Usage);
        Assert.Equal(EImageAspectFlagBits.IMAGE_ASPECT_COLOR_BIT, descriptor.AspectMask);
        Assert.True(descriptor.RegisterBindlessSampled);
        Assert.Equal(1u, descriptor.ArrayLayers);
    }

    [Fact]
    public void DepthAttachmentSampled2D_CombinesDepthAndSamplingCapabilities()
    {
        var descriptor = RenderGraphTextureDescriptor.DepthAttachmentSampled2D(
            "DirectionalShadowMap",
            2048,
            2048,
            EFormat.FORMAT_D32_SFLOAT);

        var expectedUsage =
            (uint)EImageUsageFlagBits.IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT |
            (uint)EImageUsageFlagBits.IMAGE_USAGE_SAMPLED_BIT;
        Assert.Equal(expectedUsage, descriptor.Usage);
        Assert.Equal(EImageAspectFlagBits.IMAGE_ASPECT_DEPTH_BIT, descriptor.AspectMask);
        Assert.True(descriptor.RegisterBindlessSampled);
        Assert.Equal(1u, descriptor.ArrayLayers);
    }

    [Fact]
    public void DepthAttachmentSampled2DArray_OwnsSharedDepthLayers()
    {
        var descriptor = RenderGraphTextureDescriptor.DepthAttachmentSampled2DArray(
            "DirectionalShadowCascades",
            2048,
            2048,
            EFormat.FORMAT_D32_SFLOAT,
            4);

        var expectedUsage =
            (uint)EImageUsageFlagBits.IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT |
            (uint)EImageUsageFlagBits.IMAGE_USAGE_SAMPLED_BIT;
        Assert.Equal(expectedUsage, descriptor.Usage);
        Assert.Equal(EImageAspectFlagBits.IMAGE_ASPECT_DEPTH_BIT, descriptor.AspectMask);
        Assert.True(descriptor.RegisterBindlessSampled);
        Assert.Equal(4u, descriptor.ArrayLayers);
    }

    [Fact]
    public void WithAdditionalUsage_AddsTransferSourceWithoutChangingDepthOwnership()
    {
        var descriptor = RenderGraphTextureDescriptor.DepthAttachment2D(
                "FrameDepth",
                1280,
                720,
                EFormat.FORMAT_D32_SFLOAT)
            .WithAdditionalUsage(EImageUsageFlagBits.IMAGE_USAGE_TRANSFER_SRC_BIT);

        var expectedUsage =
            (uint)EImageUsageFlagBits.IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT |
            (uint)EImageUsageFlagBits.IMAGE_USAGE_TRANSFER_SRC_BIT;
        Assert.Equal(expectedUsage, descriptor.Usage);
        Assert.Equal(EImageAspectFlagBits.IMAGE_ASPECT_DEPTH_BIT, descriptor.AspectMask);
        Assert.False(descriptor.RegisterBindlessSampled);
    }
}
