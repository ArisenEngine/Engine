using System.Text.Json;
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
    [InlineData("StaticMeshPass.SkippedAlphaDrawCount")]
    [InlineData("TransparentStaticMeshPass.DrawCount")]
    [InlineData("TransparentStaticMeshPass.TransparentDrawCount")]
    [InlineData("TransparentStaticMeshPass.SkippedAlphaDrawCount")]
    [InlineData("StaticMeshPass.PointLightCount")]
    [InlineData("StaticMeshPass.SpotLightCount")]
    [InlineData("StaticMeshPass.SceneDataCount")]
    [InlineData("Render.StaticMeshItemCount")]
    [InlineData("Render.VisibleStaticMeshItemCount")]
    [InlineData("Render.CulledStaticMeshItemCount")]
    [InlineData("Render.SceneDrawCommandCount")]
    [InlineData("Render.VisibleDrawCommandCount")]
    [InlineData("Render.OpaqueDrawCommandCount")]
    [InlineData("Render.AlphaTestDrawCommandCount")]
    [InlineData("Render.TransparentDrawCommandCount")]
    [InlineData("Render.SkippedAlphaDrawCommandCount")]
    [InlineData("Render.ShadowReceiverBoundedItemCount")]
    [InlineData("Render.ShadowCasterSourceItemCount")]
    [InlineData("Render.ShadowCasterItemCount")]
    [InlineData("Render.CulledShadowCasterItemCount")]
    [InlineData("Render.ShadowCasterDrawCommandCount")]
    [InlineData("Render.ShadowCascadeCount")]
    [InlineData("Render.DroppedShadowDrawCommandCount")]
    [InlineData("Render.MaterialCount")]
    [InlineData("Render.PreparedMaterialCount")]
    [InlineData("Render.LightCount")]
    [InlineData("Render.PointLightCount")]
    [InlineData("Render.SpotLightCount")]
    [InlineData("Render.EnvironmentCount")]
    [InlineData("DirectionalShadowPass.DrawCount")]
    [InlineData("DirectionalShadowPass.ShadowMapSize")]
    [InlineData("DirectionalShadowPass.Enabled")]
    [InlineData("DirectionalShadowPass.SceneFitted")]
    [InlineData("DirectionalShadowPass.CascadeCount")]
    [InlineData("DirectionalShadowPass.DroppedDrawCount")]
    [InlineData("RenderGraph.CulledPassCount")]
    [InlineData("RenderGraph.ResourceTransitionCount")]
    [InlineData("RenderGraph.TransientTextureCount")]
    [InlineData("RenderGraph.TransientTextureLifetimeCount")]
    [InlineData("RenderGraph.TransientTexturePeakLiveCount")]
    [InlineData("Render.FrameDepth.Width")]
    [InlineData("Render.FrameDepth.Height")]
    [InlineData("Render.FrameDepth.Format")]
    [InlineData("Terrain.ExtractedTileCount")]
    [InlineData("Terrain.PreparedDrawCount")]
    [InlineData("Terrain.SubmittedDrawCount")]
    [InlineData("Terrain.DroppedShadowDrawCount")]
    [InlineData("Terrain.VisibleTileCount")]
    [InlineData("Terrain.SeamViolationCount")]
    [InlineData("Terrain.Residency.CpuBytes")]
    [InlineData("Terrain.Residency.PreparedBytes")]
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
        Assert.Contains("ShadowDrawCommands:", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TerrainSubmissionDiagnosticsRemainBoundedAndRuntimeValidated()
    {
        var featureSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.terrain.generic-renderpipeline/Managed/TerrainGenericRenderPipelineFeature.cs");
        var passSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.terrain.generic-renderpipeline/Managed/TerrainRenderPass.cs");
        var runtimeValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_runtime.bat");
        var relocatedValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_relocated_production.ps1");

        const string marker = "[Terrain.GenericRP] Submitted terrain draw commands";
        Assert.Contains("m_SubmittedDrawReported", featureSource, StringComparison.Ordinal);
        Assert.Contains(marker, featureSource, StringComparison.Ordinal);
        Assert.Contains("LastRecordedDrawCount", passSource, StringComparison.Ordinal);
        Assert.Contains("Volatile.Write", passSource, StringComparison.Ordinal);
        Assert.Contains(marker, runtimeValidation, StringComparison.Ordinal);
        Assert.Contains(marker, relocatedValidation, StringComparison.Ordinal);
    }

    [Fact]
    public void TerrainProfilingKeepsCoarsePipelineAndResidencyAttribution()
    {
        var featureSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.terrain.generic-renderpipeline/Managed/TerrainGenericRenderPipelineFeature.cs");
        var providerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.terrain.generic-renderpipeline/Managed/TerrainPreparedAssetProvider.cs");
        var cookerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.terrain/Managed/Assets/TerrainRuntimeAssetCooker.cs");

        Assert.Contains("Profiler.Zone(\"Terrain.LodPlan\")", featureSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.Zone(\"Terrain.ExpandPatches\")", featureSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.Zone(\"Terrain.PrepareCascades\")", featureSource, StringComparison.Ordinal);
        Assert.Contains("Terrain.LodLevel0Patches", featureSource, StringComparison.Ordinal);
        Assert.Contains("Terrain.LodLevel12Patches", featureSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.Zone(\"Terrain.ReleasePreparedResource\")", providerSource, StringComparison.Ordinal);
        Assert.Contains("\"Terrain.SetupRoot\"", providerSource, StringComparison.Ordinal);
        Assert.Contains("\"Terrain.SetupTile\"", providerSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.Zone(\"Terrain.CookRoot\")", cookerSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.Zone(\"Terrain.CookTile\")", cookerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RelocatedProductionValidationAuditsTerrainRuntimeClosure()
    {
        var validation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_relocated_production.ps1");

        Assert.Contains("terrainClosureComplete = $false", validation, StringComparison.Ordinal);
        Assert.Contains("$checks.terrainClosureComplete = $true", validation, StringComparison.Ordinal);
        Assert.Contains("schemaVersion = 4", validation, StringComparison.Ordinal);
        Assert.Contains("--smoke-mode terrain-streaming", validation, StringComparison.Ordinal);
        Assert.Contains("validate_terrain_streaming_summary.ps1", validation, StringComparison.Ordinal);
        Assert.Contains("Assert-SourceIndependentRun", validation, StringComparison.Ordinal);
        Assert.Contains("Assert-TerrainShutdown", validation, StringComparison.Ordinal);
        Assert.Contains(
            "$checks.terrainStreamingSourceIndependent = $true",
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            "relocated-production-terrain-streaming.json",
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            "Runtime catalog contains stale or missing generated terrain tile rows.",
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            "Runtime catalog contains an unreferenced generated terrain tile artifact.",
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            "Runtime catalog must contain exactly three cooked terrain shader stages.",
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            "26dd7ce8-e574-4ab2-8ab7-5e6e26f361b5",
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            "Render-pipeline closure does not reach terrain shader artifact",
            validation,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DirectionalShadowSetupHasTimelineZone()
    {
        var source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/DirectionalShadowPass.cs");

        Assert.Contains("Profiler.Zone(\"DirectionalShadowPass.Prepare\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentPassLoadsOpaqueDepthWithoutWritingIt()
    {
        var pipelineSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");
        var passSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/StaticMeshPass.cs");

        Assert.Contains("StaticMeshPassConfiguration.Transparent", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("m_TransparentStaticMeshPass", pipelineSource, StringComparison.Ordinal);
        Assert.Contains(".ReadDepthAttachment(", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("RenderAttachmentIntent.ReadOnlyLoadStore", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("DepthWriteEnabled: false", passSource, StringComparison.Ordinal);
        Assert.Contains("ClearDepthOnFirstWorkItem: false", passSource, StringComparison.Ordinal);
        Assert.Contains("PreservePreparedDrawOrder: true", passSource, StringComparison.Ordinal);
        Assert.Contains("key.DepthWriteEnabled", passSource, StringComparison.Ordinal);
    }

    [Fact]
    public void FrameDepthAllocationAndTransitionsAreOwnedByRenderGraph()
    {
        var pipelineSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");
        var passSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/StaticMeshPass.cs");
        var renderGraphSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderGraph.cs");
        var textureSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderGraphTexture.cs");

        Assert.Contains("RenderGraphTextureDescriptor.DepthAttachment2D(", pipelineSource, StringComparison.Ordinal);
        Assert.Contains(".ReadWriteDepthAttachment(", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("RenderAttachmentIntent.ClearThenLoadStore", pipelineSource, StringComparison.Ordinal);
        Assert.Contains(".ReadDepthAttachment(", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("RenderAttachmentIntent.ReadOnlyLoadStore", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("pass.SetDepthTarget(", pipelineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SetExternalDepthTarget", pipelineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDepthTargetBinding", pipelineSource, StringComparison.Ordinal);

        Assert.DoesNotContain("CreateImage(", passSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReleaseImage(", passSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TransitionImageLayout(", passSource, StringComparison.Ordinal);
        Assert.Contains("IMAGE_LAYOUT_DEPTH_STENCIL_READ_ONLY_OPTIMAL", passSource, StringComparison.Ordinal);
        Assert.Contains("RecordFallbackOrDepthClear", passSource, StringComparison.Ordinal);
        Assert.Contains("RequiresDepthClearWorkItem()", passSource, StringComparison.Ordinal);

        Assert.Contains("PreparePassWorkItemCounts(context, layout);", renderGraphSource, StringComparison.Ordinal);
        Assert.Contains("BuildResourceTransitionPlan(m_ActivePassNodeIds)", renderGraphSource, StringComparison.Ordinal);
        Assert.Contains("disposalQueue.Enqueue(allocation, lastSubmittedTicket);", textureSource, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticMeshHostWriteBarrierPrecedesDynamicRendering()
    {
        var passSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/StaticMeshPass.cs");

        var fallbackStart = passSource.IndexOf("private void RecordFallbackMesh", StringComparison.Ordinal);
        var fallbackEnd = passSource.IndexOf("private void RecordDrawRange", fallbackStart, StringComparison.Ordinal);
        var fallbackBarrier = passSource.IndexOf("RecordObjectDataBarrier(commandList);", fallbackStart, StringComparison.Ordinal);
        var fallbackRendering = passSource.IndexOf("BeginStaticMeshRendering(", fallbackStart, StringComparison.Ordinal);

        Assert.InRange(fallbackBarrier, fallbackStart, fallbackEnd - 1);
        Assert.InRange(fallbackRendering, fallbackStart, fallbackEnd - 1);
        Assert.True(fallbackBarrier < fallbackRendering);

        var batchStart = passSource.IndexOf("private void RecordBatchWorkItem", StringComparison.Ordinal);
        var batchEnd = passSource.IndexOf("private void BeginStaticMeshRendering", batchStart, StringComparison.Ordinal);
        var batchBarrier = passSource.IndexOf("RecordObjectDataBarrier(commandList);", batchStart, StringComparison.Ordinal);
        var batchRendering = passSource.IndexOf("BeginStaticMeshRendering(", batchStart, StringComparison.Ordinal);

        Assert.InRange(batchBarrier, batchStart, batchEnd - 1);
        Assert.InRange(batchRendering, batchStart, batchEnd - 1);
        Assert.True(batchBarrier < batchRendering);
    }

    [Fact]
    public void SharedImageOwnershipUsesExternalQueueFamilyInsteadOfForeignFamily()
    {
        var managedSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core/RHI/RHICommandBufferInterop.cs");
        var nativeSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core.native/Source/Core.RHI/RHI/Sync/RHIImageMemoryBarrier.h");

        Assert.Contains("public const uint External = 0xFFFFFFFEu;", managedSource, StringComparison.Ordinal);
        Assert.Contains("RHI_QUEUE_FAMILY_EXTERNAL = 0xFFFFFFFEu;", nativeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("External = 0xFFFFFFFDu;", managedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RHI_QUEUE_FAMILY_EXTERNAL = 0xFFFFFFFDu;", nativeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectionalShadowAllocationAndTransitionsAreOwnedByRenderGraph()
    {
        var pipelineSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");
        var passSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/DirectionalShadowPass.cs");
        var targetPath = Path.Combine(
            FindRepoRoot(),
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/DirectionalShadowTarget.cs");

        Assert.Contains("RenderGraphTextureDescriptor.DepthAttachmentSampled2DArray(", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("var shadowMapResource = directionalShadowTexture.Resource;", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("m_DirectionalShadowPass.SetDepthTargets(", pipelineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateTransientResource(\"DirectionalShadowMap\"", pipelineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectionalShadowTarget", pipelineSource, StringComparison.Ordinal);

        Assert.Contains("BeginRenderingDepthOnly(", passSource, StringComparison.Ordinal);
        Assert.Contains("texture.GetLayerImageView(", passSource, StringComparison.Ordinal);
        Assert.DoesNotContain("m_PreparedDrawCount <= 0", passSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TransitionImageLayout(", passSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateImage(", passSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReleaseImage(", passSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectionalShadowTarget", passSource, StringComparison.Ordinal);
        Assert.False(File.Exists(targetPath));
    }

    [Fact]
    public void RenderGraphAttachmentDeclarationsKeepLoadStoreIntentVisible()
    {
        var pipelineSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");
        var graphSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderGraph.cs");
        var plannerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderGraphResourcePlanner.cs");

        Assert.Contains("RenderAttachmentIntent.ClearStore", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("RenderAttachmentIntent.LoadStore", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("RenderAttachmentIntent.ClearThenLoadStore", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("RenderAttachmentIntent.ReadOnlyLoadStore", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("builder.Append(\",load=\");", graphSource, StringComparison.Ordinal);
        Assert.Contains("builder.Append(\",store=\");", graphSource, StringComparison.Ordinal);
        Assert.Contains("RequiresExistingAttachmentContent", plannerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderGraphLifetimePlanningUsesCulledAndActivePassOrder()
    {
        var renderGraphSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderGraph.cs");
        var plannerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderGraphResourceLifetimePlanner.cs");

        var preflightIndex = renderGraphSource.IndexOf(
            "PreparePassWorkItemCounts(context, layout);",
            StringComparison.Ordinal);
        var lifetimeIndex = renderGraphSource.IndexOf(
            "RenderGraphResourceLifetimePlanner.BuildLifetimePlan(",
            StringComparison.Ordinal);

        Assert.True(preflightIndex >= 0);
        Assert.True(lifetimeIndex > preflightIndex);
        Assert.Contains("layout.SortedNodeIds", renderGraphSource, StringComparison.Ordinal);
        Assert.Contains("m_ActivePassNodeIds", renderGraphSource, StringComparison.Ordinal);
        Assert.Contains("LogTransientTextureLifetimeDiagnostics", renderGraphSource, StringComparison.Ordinal);
        Assert.Contains("resource.IsImported || resource.Type != RenderResourceType.Texture", plannerSource, StringComparison.Ordinal);
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

    [Fact]
    public void ModelImportAndSceneLoadKeepCoarseTimelineMarkers()
    {
        var reimportSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/Resources/ModelSourceReimporter.cs");
        var plannerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/Resources/GltfModelImportPlanner.cs");
        var emitterSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/Resources/GltfModelImportEmitter.cs");
        var runtimeSceneSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.resources/Serialization/RuntimeSceneService.cs");
        var sceneLoaderSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.resources/Serialization/SceneAssetLoader.cs");

        Assert.Contains("Profiler.Zone(\"ModelSourceReimporter.Reimport\")", reimportSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.Zone(\"ModelSourceReimporter.InvalidateCookedOutputs\")", reimportSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.PlotValue(\"ModelImport.InvalidatedAssetCount\"", reimportSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.Zone(\"GltfModelImportPlanner.CreatePlan\")", plannerSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.PlotValue(\"ModelImport.PlannedChildCount\"", plannerSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.Zone(\"GltfModelImportEmitter.Emit\")", emitterSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.PlotValue(\"ModelImport.EmittedTextureCount\"", emitterSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.Zone(\"RuntimeSceneService.LoadScene\")", runtimeSceneSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.Zone(\"SceneAssetLoader.LoadSceneSource\")", sceneLoaderSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.PlotValue(\"SceneLoad.EntityCount\"", sceneLoaderSource, StringComparison.Ordinal);
        Assert.Contains("Profiler.PlotValue(\"SceneLoad.MeshRendererCount\"", sceneLoaderSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelSceneProfilerWorkflowUsesBundledViewerAndEnabledDevelopmentProfile()
    {
        var launcherSource = ReadRepoFile("Arisen/Scripts/Windows/open_tracy_profiler.bat");
        var profilingGuide = ReadRepoFile("Arisen/Docs/Architecture/Profiling.md");
        var manifestSource = ReadRepoFile("Arisen/Development/PackageGame/manifest.json");

        Assert.Contains(
            @"Development\PackageGame\Local\com.arisen.core.native\3rdparty\tracy",
            launcherSource,
            StringComparison.Ordinal);
        Assert.Contains("--target tracy-profiler", launcherSource, StringComparison.Ordinal);
        Assert.Contains("start \"\" \"%TRACY_PROFILER_EXE%\"", launcherSource, StringComparison.Ordinal);
        Assert.Contains(
            @"open_tracy_profiler.bat --config Release --no-pause",
            profilingGuide,
            StringComparison.Ordinal);
        Assert.Contains(
            @"build_workspace.bat --config Debug --profile Development",
            profilingGuide,
            StringComparison.Ordinal);
        Assert.Contains(
            @"PackageGame.exe --workspace Arisen\Development\PackageGame --profile Development",
            profilingGuide,
            StringComparison.Ordinal);

        using var manifest = JsonDocument.Parse(
            manifestSource,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
        var profiles = manifest.RootElement.GetProperty("Profiles");
        Assert.True(profiles.GetProperty("Development").GetProperty("EnableProfiler").GetBoolean());
        Assert.False(profiles.GetProperty("Production").GetProperty("EnableProfiler").GetBoolean());
    }

    private static string ReadRepoFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));
    }

    private static string GetCounterSourcePath(string counterName)
    {
        if (counterName.StartsWith("StaticMeshPass.", StringComparison.Ordinal) ||
            counterName.StartsWith("TransparentStaticMeshPass.", StringComparison.Ordinal))
        {
            return "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/StaticMeshPass.cs";
        }

        if (counterName.StartsWith("DirectionalShadowPass.", StringComparison.Ordinal))
        {
            return "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/DirectionalShadowPass.cs";
        }

        if (counterName.StartsWith("Terrain.", StringComparison.Ordinal))
        {
            return counterName.StartsWith("Terrain.Residency.", StringComparison.Ordinal)
                ? "Arisen/Development/PackageGame/Local/com.arisen.terrain.generic-renderpipeline/Managed/TerrainPreparedAssetProvider.cs"
                : "Arisen/Development/PackageGame/Local/com.arisen.terrain.generic-renderpipeline/Managed/TerrainGenericRenderPipelineFeature.cs";
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
