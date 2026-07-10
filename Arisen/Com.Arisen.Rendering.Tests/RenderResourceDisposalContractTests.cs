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

        Assert.Contains("public void ReleaseCompleted(RHIDevice device)", queueSource, StringComparison.Ordinal);
        Assert.Contains("device.GetCompletedTicket()", queueSource, StringComparison.Ordinal);
        Assert.Contains("m_DisposalQueue.ReleaseCompleted(context.Device);", pipelineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("m_DisposalQueue.Drain(context.Device);", pipelineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DeferredDisposalKeepsBlockingDrainForPipelineTeardown()
    {
        var queueSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/DeferredRenderResourceDisposalQueue.cs");
        var pipelineSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");

        Assert.Contains("public void Drain(RHIDevice device)", queueSource, StringComparison.Ordinal);
        Assert.Contains("device.WaitQueueTicket", queueSource, StringComparison.Ordinal);
        Assert.Contains("m_DisposalQueue.Drain(m_LastDevice);", pipelineSource, StringComparison.Ordinal);
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
