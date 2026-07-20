using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderOutputDepthContractTests
{
    [Fact]
    public void VisualSummaryPublishesAndDeclaresFrameDepthTransferRead()
    {
        var pipeline = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderPipeline.cs");
        var genericPipeline = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");

        Assert.Contains("PublishFrameDepth(frameDepthTexture);", genericPipeline, StringComparison.Ordinal);
        Assert.Contains("if (IsVisualSummaryEnabled)", genericPipeline, StringComparison.Ordinal);
        Assert.Contains("IMAGE_USAGE_TRANSFER_SRC_BIT", genericPipeline, StringComparison.Ordinal);
        Assert.Contains(".ReadTransfer(capturedFrameDepth.Resource)", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedCopyContractCarriesColorAndDepthAspectsToNativeBridge()
    {
        var readback = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderOutputReadbackPass.cs");
        var commandList = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderCommandList.cs");
        var managedRhi = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core/RHI/RHICommandBuffer.cs");
        var nativeBridge = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core.native/Source/Core.RHI/Bridges/RHICommandBufferBridge.cpp");

        Assert.Contains("EImageAspectFlagBits.IMAGE_ASPECT_COLOR_BIT", readback, StringComparison.Ordinal);
        Assert.Contains("EImageAspectFlagBits.IMAGE_ASPECT_DEPTH_BIT", readback, StringComparison.Ordinal);
        Assert.Contains("EImageAspectFlagBits sourceAspect", commandList, StringComparison.Ordinal);
        Assert.Contains("EImageAspectFlagBits srcImageAspect", managedRhi, StringComparison.Ordinal);
        Assert.Contains("uint32_t srcImageAspect", nativeBridge, StringComparison.Ordinal);
        Assert.Contains(
            "static_cast<EImageAspectFlagBits>(srcImageAspect)",
            nativeBridge,
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

        throw new DirectoryNotFoundException("Could not locate the Arisen repository root.");
    }
}
