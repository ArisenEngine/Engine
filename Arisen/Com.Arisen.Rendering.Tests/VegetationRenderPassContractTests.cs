using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationRenderPassContractTests
{
    [Fact]
    public void OpaquePassDeclaresShadowColorAndDepthGraphAccess()
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationOpaquePass.cs");

        Assert.Contains(".ReadShader(directionalShadow)", source, StringComparison.Ordinal);
        Assert.Contains(".ReadWriteColorAttachment(", source, StringComparison.Ordinal);
        Assert.Contains("sceneColor", source, StringComparison.Ordinal);
        Assert.Contains(".ReadWriteDepthAttachment(", source, StringComparison.Ordinal);
        Assert.Contains("frameDepth", source, StringComparison.Ordinal);
        Assert.Contains("RenderAttachmentIntent.LoadStore", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OpaqueDrawPushConstantsFitThePortableRange()
    {
        string pass = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationOpaquePass.cs");
        string shader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" +
            "Vegetation.hlsl");
        int blockStart = shader.IndexOf("[[vk::push_constant]]", StringComparison.Ordinal);
        int blockEnd = shader.IndexOf("} DrawConstants;", blockStart, StringComparison.Ordinal);

        Assert.True(blockStart >= 0 && blockEnd > blockStart);
        string pushBlock = shader[blockStart..blockEnd];
        int vectorCount = pushBlock
            .Split('\n')
            .Count(line => line.TrimStart().StartsWith("float4 ", StringComparison.Ordinal));

        Assert.Equal(4, vectorCount);
        Assert.Equal(64, vectorCount * 16);
        Assert.Contains("public const int ByteSize = 64;", pass, StringComparison.Ordinal);
        Assert.Contains("public const int ByteSize = 112;", pass, StringComparison.Ordinal);
        Assert.Contains("PrepareFrame(", pass, StringComparison.Ordinal);
        Assert.Contains("ACCESS_HOST_WRITE_BIT", pass, StringComparison.Ordinal);
        Assert.Contains("ACCESS_SHADER_READ_BIT", pass, StringComparison.Ordinal);
        Assert.Contains("LoadFrameVector(0)", shader, StringComparison.Ordinal);
        Assert.Contains("LoadFrameVector(6)", shader, StringComparison.Ordinal);
        Assert.Contains(
            "new Vector4(light.Direction, light.Intensity)",
            pass,
            StringComparison.Ordinal);
        Assert.Contains(
            "new Vector4(light.Color, ambient)",
            pass,
            StringComparison.Ordinal);
        Assert.Contains(
            "DrawConstants.baseColorFactor.rgb",
            shader,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OpaqueShadowReceptionIsResolvedFromTheClusterBeforeRecording()
    {
        string feature = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationGenericRenderPipelineFeature.cs");
        string pass = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationOpaquePass.cs");
        string shader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" +
            "Vegetation.hlsl");

        Assert.Contains(
            "(component.Flags & VegetationClusterFlags.ReceiveShadows) != 0",
            feature,
            StringComparison.Ordinal);
        Assert.Contains(
            "receiveShadows && directionalShadow.Enabled",
            pass,
            StringComparison.Ordinal);
        Assert.Contains(
            "DrawConstants.frameShadowParameters.y",
            shader,
            StringComparison.Ordinal);
        Assert.Contains(
            "shadowBufferIndex == INVALID_BINDLESS_INDEX",
            shader,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OpaqueFrameBufferRegistrationRetainsRetryableCleanupOwnership()
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationOpaquePass.cs");
        string setup = SliceBetween(
            source,
            "private void EnsureFrameBufferSlot(",
            "private void ReleaseFrameBuffers()");

        AssertInOrder(
            setup,
            "slot = new VegetationOpaqueFrameBufferSlot(buffer, InvalidBindlessIndex);",
            "try",
            "m_Factory.RegisterBindlessResourceBuffer(buffer);",
            "catch (Exception registrationFailure)",
            "ReleaseFrameBufferSlot(ref slot);",
            "catch (Exception releaseFailure)",
            "throw new AggregateException(");
        Assert.Contains(
            "if (slot.Buffer.IsValid)",
            setup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OpaquePassJournalsEverySuccessfulPipelineCleanupLegImmediately()
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationOpaquePass.cs");
        string cleanup = SliceBetween(
            source,
            "private void ReleasePipelineResources()",
            "private void ReleasePipeline()");

        Assert.Contains("m_PipelineCleanup.BeginOwnership();", source, StringComparison.Ordinal);
        AssertInOrder(
            cleanup,
            "m_PipelineCleanup.Release(PipelineCleanupLeg, ReleasePipeline);",
            "m_PipelineCleanup.Release(PipelineStateCleanupLeg, ReleasePipelineState);",
            "m_PipelineCleanup.Release(VertexProgramCleanupLeg, ReleaseVertexProgram);",
            "m_PipelineCleanup.Release(FragmentProgramCleanupLeg, ReleaseFragmentProgram);",
            "m_PipelineCleanup.Release(VertexShaderAssetCleanupLeg, ReleaseVertexShaderAsset);",
            "m_PipelineCleanup.Release(FragmentShaderAssetCleanupLeg, ReleaseFragmentShaderAsset);");
        AssertInOrder(
            SliceBetween(source, "private void ReleasePipeline()", "private void ReleasePipelineState()"),
            "pipelineCache.ReleasePipeline(m_Pipeline);",
            "m_Pipeline = RHIPipelineHandle.Invalid;");
        AssertInOrder(
            SliceBetween(source, "private void ReleasePipelineState()", "private void ReleaseVertexProgram()"),
            "m_PipelineState.Release();",
            "m_PipelineState = default;");
        AssertInOrder(
            SliceBetween(source, "private void ReleaseVertexProgram()", "private void ReleaseFragmentProgram()"),
            "m_Factory.ReleaseGPUProgram(m_VertexProgram);",
            "m_VertexProgram = RHIShaderProgramHandle.Invalid;");
        AssertInOrder(
            SliceBetween(source, "private void ReleaseFragmentProgram()", "private void ReleaseVertexShaderAsset()"),
            "m_Factory.ReleaseGPUProgram(m_FragmentProgram);",
            "m_FragmentProgram = RHIShaderProgramHandle.Invalid;");
        AssertInOrder(
            SliceBetween(source, "private void ReleaseVertexShaderAsset()", "private void ReleaseFragmentShaderAsset()"),
            "m_AssetDatabase.Release(m_VertexShaderAsset);",
            "m_VertexShaderAsset = CookedAssetHandle.Invalid;");
        AssertInOrder(
            SliceBetween(source, "private void ReleaseFragmentShaderAsset()", "private RHIShaderProgramHandle CompileProgram("),
            "m_AssetDatabase.Release(m_FragmentShaderAsset);",
            "m_FragmentShaderAsset = CookedAssetHandle.Invalid;");
    }

    [Fact]
    public void ShadowPassJournalsEverySuccessfulPipelineCleanupLegImmediately()
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationShadowPass.cs");
        string cleanup = SliceBetween(
            source,
            "public void ReleaseDeviceResources()",
            "private void ReleasePipeline()");

        Assert.Contains("m_PipelineCleanup.BeginOwnership();", source, StringComparison.Ordinal);
        AssertInOrder(
            cleanup,
            "m_PipelineCleanup.Release(PipelineCleanupLeg, ReleasePipeline);",
            "m_PipelineCleanup.Release(PipelineStateCleanupLeg, ReleasePipelineState);",
            "m_PipelineCleanup.Release(VertexProgramCleanupLeg, ReleaseVertexProgram);",
            "m_PipelineCleanup.Release(VertexShaderAssetCleanupLeg, ReleaseVertexShaderAsset);");
        AssertInOrder(
            SliceBetween(source, "private void ReleasePipeline()", "private void ReleasePipelineState()"),
            "pipelineCache.ReleasePipeline(m_Pipeline);",
            "m_Pipeline = RHIPipelineHandle.Invalid;");
        AssertInOrder(
            SliceBetween(source, "private void ReleasePipelineState()", "private void ReleaseVertexProgram()"),
            "m_PipelineState.Release();",
            "m_PipelineState = default;");
        AssertInOrder(
            SliceBetween(source, "private void ReleaseVertexProgram()", "private void ReleaseVertexShaderAsset()"),
            "m_Factory.ReleaseGPUProgram(m_VertexProgram);",
            "m_VertexProgram = RHIShaderProgramHandle.Invalid;");
        AssertInOrder(
            SliceBetween(source, "private void ReleaseVertexShaderAsset()", "private RHIShaderProgramHandle CompileProgram("),
            "m_AssetDatabase.Release(m_VertexShaderAsset);",
            "m_VertexShaderAsset = CookedAssetHandle.Invalid;");
    }

    [Fact]
    public void SubmissionMarkerWaitsForWorkAndScopesReportsPerSurfaceGeneration()
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationGenericRenderPipelineFeature.cs");
        string condition = SliceBetween(
            source,
            "if (opaqueBatches > 0",
            "LogSubmittedDrawValidation(");

        Assert.Contains("opaqueInstances > 0", condition, StringComparison.Ordinal);
        Assert.Contains("shadowBatches > 0", condition, StringComparison.Ordinal);
        Assert.Contains("shadowInstances > 0", condition, StringComparison.Ordinal);
        Assert.Contains("context.SubmittedTicket > 0", condition, StringComparison.Ordinal);
        Assert.Contains("TryMarkSurfaceReported(", condition, StringComparison.Ordinal);
        Assert.Contains("context.Frame.RenderContext.SurfaceId", condition, StringComparison.Ordinal);
        Assert.Contains("context.Frame.RenderContext.DeviceGeneration", condition, StringComparison.Ordinal);
        Assert.Contains("[Vegetation.GenericRP.Validation]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("m_SubmittedDrawReported", source, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualValidationModesSuppressOnlyPreparedVegetationPasses()
    {
        string policy = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationRenderValidationMode.cs");
        string package = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationGenericRenderPipelinePackage.cs");
        string feature = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationGenericRenderPipelineFeature.cs");

        Assert.Contains(
            "ARISEN_VEGETATION_RENDER_VALIDATION_MODE",
            policy,
            StringComparison.Ordinal);
        Assert.Contains("VegetationRenderValidationMode.Full", policy, StringComparison.Ordinal);
        Assert.Contains("\"opaque-only\"", policy, StringComparison.Ordinal);
        Assert.Contains("VegetationRenderValidationMode.Disabled", policy, StringComparison.Ordinal);
        Assert.Contains("ResolveFromEnvironment()", package, StringComparison.Ordinal);
        Assert.Contains(
            "[Vegetation.GenericRP.VisualValidation] Mode={0}",
            package,
            StringComparison.Ordinal);

        string preparation = SliceBetween(
            feature,
            "public void PrepareResources(",
            "private void PlotPreparedState()");
        AssertInOrder(
            preparation,
            "m_PreparedAssets.UpdateFrameContext(",
            "m_PreparedAssets.InvalidateStaleDependencies()",
            "m_ValidationMode == VegetationRenderValidationMode.Disabled",
            "ClearPreparedFrameState(",
            "m_OpaquePass.Prepare(context.RenderContext);",
            "PrepareClustersAndOpaqueDraws(context, opaqueFrameBufferIndex);",
            "m_ValidationMode == VegetationRenderValidationMode.Full",
            "PrepareShadowDraws(context);");
    }

    [Fact]
    public void StaleDependenciesClearBothPassesBeforeFramePreparationReturns()
    {
        string feature = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationGenericRenderPipelineFeature.cs");
        string staleBranch = SliceBetween(
            feature,
            "if (m_PreparedAssets.InvalidateStaleDependencies())",
            "m_OpaquePass.Prepare(context.RenderContext);");
        AssertInOrder(
            staleBranch,
            "ClearPreparedFrameState(",
            "PlotPreparedState();",
            "return;");

        string clear = SliceBetween(
            feature,
            "private void ClearPreparedFrameState(",
            "private static void EnsureCapacity<T>");
        Assert.Contains("m_PreparedClusterCount = 0;", clear, StringComparison.Ordinal);
        Assert.Contains("m_OpaqueDrawCount = 0;", clear, StringComparison.Ordinal);
        Assert.Contains("m_ShadowDrawCount = 0;", clear, StringComparison.Ordinal);
        Assert.Contains("m_OpaquePass.SetPreparedDraws", clear, StringComparison.Ordinal);
        Assert.Contains("CreateEmptyShadowRanges(cascadeCount)", clear, StringComparison.Ordinal);
        Assert.Contains("m_ShadowPass.SetPreparedDraws", clear, StringComparison.Ordinal);

        string emptyRanges = SliceBetween(
            feature,
            "private static DirectionalShadowCascadeDrawRangeSet CreateEmptyShadowRanges(",
            "private void ClearPreparedFrameState(");
        Assert.Contains("cascadeCount == 0", emptyRanges, StringComparison.Ordinal);
        Assert.Contains("? default", emptyRanges, StringComparison.Ordinal);
    }

    [Fact]
    public void PipelinesDeclareOnlyShaderConsumedVertexLocations()
    {
        string opaque = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/VegetationOpaquePass.cs");
        string shadow = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/VegetationShadowPass.cs");
        string opaqueLayout = opaque[opaque.LastIndexOf(
            "private static void AddStaticMeshVertexLayout",
            StringComparison.Ordinal)..];
        string shadowLayout = shadow[shadow.LastIndexOf(
            "private static void AddStaticMeshVertexLayout",
            StringComparison.Ordinal)..];

        Assert.DoesNotContain("\n            2,\n", opaqueLayout, StringComparison.Ordinal);
        Assert.Contains("\n            0,\n", shadowLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("\n            1,\n", shadowLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("\n            2,\n", shadowLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("\n            3,\n", shadowLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("\n            4,\n", shadowLayout, StringComparison.Ordinal);
    }

    [Fact]
    public void ShadowPassDeclaresCascadeDepthLoadStoreAccess()
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationShadowPass.cs");

        Assert.Contains("builder.ReadWriteDepthAttachment(", source, StringComparison.Ordinal);
        Assert.Contains("directionalShadow", source, StringComparison.Ordinal);
        Assert.Contains("RenderAttachmentIntent.LoadStore", source, StringComparison.Ordinal);
        Assert.Contains("BeginRenderingDepthOnly(", source, StringComparison.Ordinal);
        Assert.Contains("ATTACHMENT_LOAD_OP_LOAD", source, StringComparison.Ordinal);
        Assert.Contains("ATTACHMENT_STORE_OP_STORE", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("VegetationOpaquePass.cs")]
    [InlineData("VegetationShadowPass.cs")]
    public void PassesSubmitPositiveInstancedIndexedRanges(string fileName)
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" + fileName);

        Assert.Contains("InstanceCount > 0", source, StringComparison.Ordinal);
        Assert.Contains("commandList.DrawIndexed(", source, StringComparison.Ordinal);
        Assert.Contains("instanceCount: draw.InstanceCount", source, StringComparison.Ordinal);
        Assert.Contains("firstIndex: draw.FirstIndex", source, StringComparison.Ordinal);
        Assert.Contains("vertexOffset: draw.VertexOffset", source, StringComparison.Ordinal);
        Assert.Contains("firstInstance: draw.FirstInstance", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordingLoopsUsePreparedArraysWithoutLookupsOrAllocation()
    {
        AssertRecordingLoopIsPreparedOnly("VegetationOpaquePass.cs");
        AssertRecordingLoopIsPreparedOnly("VegetationShadowPass.cs");
    }

    [Theory]
    [InlineData("Vegetation.hlsl")]
    [InlineData("VegetationShadow.hlsl")]
    public void ShadersAddressFortyEightByteInstancesByBaseInstance(
        string fileName)
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" + fileName);

        Assert.Contains("VEGETATION_INSTANCE_STRIDE = 48", source, StringComparison.Ordinal);
        Assert.Contains("InstanceIndex : SV_InstanceID", source, StringComparison.Ordinal);
        Assert.Contains(
            "LoadVegetationInstance(input.InstanceIndex)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "instanceIndex * VEGETATION_INSTANCE_STRIDE",
            source,
            StringComparison.Ordinal);
        Assert.Contains("byteOffset + 16", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ItemFiveShaderDoesNotApplyFutureWindOrColorVariation()
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" +
            "Vegetation.hlsl");

        Assert.DoesNotContain("wind", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("variation", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShaderRegistryMatchesImportedShaderSourceGuids()
    {
        string registry = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationGenericRenderPipelineShaderAssets.cs");
        string opaqueMeta = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" +
            "Vegetation.hlsl.meta");
        string shadowMeta = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" +
            "VegetationShadow.hlsl.meta");

        Assert.Contains(
            "2a536b1f-81cf-4d91-a84f-39bc6f7e15a2",
            registry,
            StringComparison.Ordinal);
        Assert.Contains(
            "2a536b1f-81cf-4d91-a84f-39bc6f7e15a2",
            opaqueMeta,
            StringComparison.Ordinal);
        Assert.Contains(
            "9d7a4c3e-f2b6-46a1-8c59-5e1087b34d20",
            registry,
            StringComparison.Ordinal);
        Assert.Contains(
            "9d7a4c3e-f2b6-46a1-8c59-5e1087b34d20",
            shadowMeta,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("VegetationOpaquePass.cs")]
    [InlineData("VegetationShadowPass.cs")]
    public void PipelinesUseTheCanonicalOpaqueTwoSidedRenderState(string fileName)
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" + fileName);

        Assert.Contains("ECullModeFlagBits.CULL_MODE_NONE", source, StringComparison.Ordinal);
        Assert.Contains(
            "EFrontFace.FRONT_FACE_COUNTER_CLOCKWISE",
            source,
            StringComparison.Ordinal);
        Assert.Contains("SetColorBlendState(false)", source, StringComparison.Ordinal);
        Assert.Contains("SetDepthStencilState(", source, StringComparison.Ordinal);
    }

    private static void AssertRecordingLoopIsPreparedOnly(string fileName)
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" + fileName);
        int loopStart = source.IndexOf(
            "ref readonly Vegetation",
            StringComparison.Ordinal);
        int loopEnd = source.IndexOf(
            "commandList.EndRendering();",
            loopStart,
            StringComparison.Ordinal);
        Assert.True(loopStart >= 0 && loopEnd > loopStart);
        string loop = source[loopStart..loopEnd];

        Assert.DoesNotContain("GetService", loop, StringComparison.Ordinal);
        Assert.DoesNotContain("Array.Resize", loop, StringComparison.Ordinal);
        Assert.DoesNotContain("new List", loop, StringComparison.Ordinal);
        Assert.DoesNotContain("lock (", loop, StringComparison.Ordinal);
    }

    private static void AssertInOrder(string source, params string[] values)
    {
        int previous = -1;
        foreach (string value in values)
        {
            int current = source.IndexOf(value, previous + 1, StringComparison.Ordinal);
            Assert.True(
                current > previous,
                $"Expected source contract '{value}' after offset {previous}.");
            previous = current;
        }
    }

    private static string SliceBetween(string source, string startText, string endText)
    {
        int start = source.IndexOf(startText, StringComparison.Ordinal);
        int end = source.IndexOf(endText, start + startText.Length, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find source marker '{startText}'.");
        Assert.True(end > start, $"Could not find source marker '{endText}' after '{startText}'.");
        return source[start..end];
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

        throw new InvalidOperationException(
            "Could not locate repository root from test output directory.");
    }
}
