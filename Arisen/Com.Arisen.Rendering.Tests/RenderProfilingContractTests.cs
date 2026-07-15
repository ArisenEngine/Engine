using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderProfilingContractTests
{
    [Theory]
    [InlineData("StaticMeshPass.DrawCount")]
    [InlineData("StaticMeshPass.MaterialBatchCount")]
    [InlineData("StaticMeshPass.OpaqueDrawCount")]
    [InlineData("StaticMeshPass.AlphaTestDrawCount")]
    [InlineData("StaticMeshPass.TransparentDrawCount")]
    [InlineData("StaticMeshPass.PointLightCount")]
    [InlineData("StaticMeshPass.SpotLightCount")]
    [InlineData("StaticMeshPass.SceneDataCount")]
    [InlineData("Render.StaticMeshItemCount")]
    [InlineData("Render.VisibleStaticMeshItemCount")]
    [InlineData("Render.CulledStaticMeshItemCount")]
    [InlineData("Render.SceneDrawCommandCount")]
    [InlineData("Render.VisibleDrawCommandCount")]
    [InlineData("Render.MaterialCount")]
    [InlineData("Render.PreparedMaterialCount")]
    [InlineData("Render.LightCount")]
    [InlineData("Render.PointLightCount")]
    [InlineData("Render.SpotLightCount")]
    [InlineData("Render.EnvironmentCount")]
    [InlineData("DirectionalShadowPass.DrawCount")]
    [InlineData("DirectionalShadowPass.ShadowMapSize")]
    [InlineData("DirectionalShadowPass.Enabled")]
    [InlineData("RenderGraph.CulledPassCount")]
    [InlineData("RenderGraph.ResourceTransitionCount")]
    [InlineData("RenderGraph.TransientTextureCount")]
    public void SceneRenderingProfilerCountersRemainDeclared(string counterName)
    {
        var sourcePath = GetCounterSourcePath(counterName);
        var source = ReadRepoFile(sourcePath);

        Assert.Contains($"Profiler.PlotValue(\"{counterName}\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericPipelineSetupLogKeepsVisualSceneCountsVisible()
    {
        var source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");

        Assert.Contains("DirectionalLights:", source, StringComparison.Ordinal);
        Assert.Contains("PointLights:", source, StringComparison.Ordinal);
        Assert.Contains("SpotLights:", source, StringComparison.Ordinal);
        Assert.Contains("Materials:", source, StringComparison.Ordinal);
        Assert.Contains("VisibleDrawCommands:", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectionalShadowSetupHasTimelineZone()
    {
        var source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/DirectionalShadowPass.cs");

        Assert.Contains("Profiler.Zone(\"DirectionalShadowPass.Prepare\")", source, StringComparison.Ordinal);
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

    private static string GetCounterSourcePath(string counterName)
    {
        if (counterName.StartsWith("StaticMeshPass.", StringComparison.Ordinal))
        {
            return "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/StaticMeshPass.cs";
        }

        if (counterName.StartsWith("DirectionalShadowPass.", StringComparison.Ordinal))
        {
            return "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/DirectionalShadowPass.cs";
        }

        if (counterName.StartsWith("Render.", StringComparison.Ordinal))
        {
            return "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs";
        }

        return "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderGraph.cs";
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
