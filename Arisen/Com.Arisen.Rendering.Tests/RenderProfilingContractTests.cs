using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderProfilingContractTests
{
    [Theory]
    [InlineData("StaticMeshPass.DrawCount")]
    [InlineData("StaticMeshPass.MaterialBatchCount")]
    [InlineData("RenderGraph.CulledPassCount")]
    [InlineData("RenderGraph.ResourceTransitionCount")]
    public void SceneRenderingProfilerCountersRemainDeclared(string counterName)
    {
        var source = ReadRepoFile(
            counterName.StartsWith("StaticMeshPass.", StringComparison.Ordinal)
                ? "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/StaticMeshPass.cs"
                : "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderGraph.cs");

        Assert.Contains($"Profiler.PlotValue(\"{counterName}\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderGraphCommandRecordingRemainsVisibleAsWorkerTaskSpans()
    {
        var renderGraphSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderGraph.cs");
        var taskWorkerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.taskgraph/Managed/TaskWorker.cs");

        Assert.Contains("new ActionTask(() =>", renderGraphSource, StringComparison.Ordinal);
        Assert.Contains("}, $\"{node.Name}[{capturedWorkItemIndex}]\");", renderGraphSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.Zone(task.Name)", taskWorkerSource, StringComparison.Ordinal);
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
