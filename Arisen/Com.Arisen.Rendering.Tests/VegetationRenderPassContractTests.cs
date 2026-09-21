using ArisenEngine.Vegetation;
using System.Text.RegularExpressions;
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

        Assert.Equal(7, vectorCount);
        Assert.Equal(112, vectorCount * 16);
        Assert.True(
            vectorCount * 16 <= 128,
            "The opaque push-constant block must fit the 128-byte portable minimum.");
        Assert.Contains("public const int ByteSize = 112;", pass, StringComparison.Ordinal);
        Assert.Contains("public const int ByteSize = 144;", pass, StringComparison.Ordinal);
        Assert.Contains("PrepareFrame(", pass, StringComparison.Ordinal);
        Assert.Contains("ACCESS_HOST_WRITE_BIT", pass, StringComparison.Ordinal);
        Assert.Contains("ACCESS_SHADER_READ_BIT", pass, StringComparison.Ordinal);
        Assert.Contains("LoadFrameVector(0)", shader, StringComparison.Ordinal);
        Assert.Contains("LoadFrameVector(6)", shader, StringComparison.Ordinal);
        Assert.Contains(
            "VEGETATION_WIND_DIRECTION_STRENGTH_VECTOR = 7u",
            shader,
            StringComparison.Ordinal);
        Assert.Contains(
            "VEGETATION_WIND_GUST_PARAMETERS_VECTOR = 8u",
            shader,
            StringComparison.Ordinal);
        Assert.Contains(
            "new Vector4(cameraPositionInFrame, 1.0f)",
            pass,
            StringComparison.Ordinal);
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
            "(cluster.Component.Flags & VegetationClusterFlags.ReceiveShadows) != 0",
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
    public void ValidationRecordReportsCanonicalBoundedPerClusterInstanceCounts()
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationGenericRenderPipelineFeature.cs");

        Assert.Contains(
            "private const int MaximumLoggedClusters = 8;",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"Species={6:D} Clusters={7} ClustersOverflow={8} OpaqueBatches={9} \" +",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "m_LoggedClusterGuids[insertion].CompareTo(candidateCluster) <= 0",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (insertion >= MaximumLoggedClusters)",
            source,
            StringComparison.Ordinal);
        Assert.Contains("overflow++;", source, StringComparison.Ordinal);
        Assert.Contains("clustersOverflow = overflow;", source, StringComparison.Ordinal);
        Assert.Contains(
            ".Append(m_LoggedClusterGuids[index].ToString(\"N\"))",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            ".Append(m_LoggedSpeciesGuids[index].ToString(\"N\"))",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            ".Append(m_LoggedClusterInstances[index]);",
            source,
            StringComparison.Ordinal);
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
        preparation = preparation.Replace("\r\n", "\n", StringComparison.Ordinal);
        AssertInOrder(
            preparation,
            "m_PreparedAssets.UpdateFrameContext(",
            "m_PreparedAssets.InvalidateStaleDependencies()",
            "m_ValidationMode == VegetationRenderValidationMode.Disabled",
            "ClearPreparedFrameState(",
            "m_OpaquePass.Prepare(context.RenderContext);",
            "PrepareClustersAndOpaqueDraws(\n            context,\n            opaqueFrameBufferIndex,\n            cameraWorldPosition);",
            "m_ValidationMode == VegetationRenderValidationMode.Full",
            "PrepareShadowDraws(\n                context,\n                opaqueFrameBufferIndex,\n                cameraWorldPosition);");
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

    [Theory]
    [InlineData("VegetationOpaquePass.cs", "Vegetation.hlsl")]
    [InlineData("VegetationShadowPass.cs", "VegetationShadow.hlsl")]
    public void PipelinesDeclareOnlyShaderConsumedVertexLocations(
        string passFileName,
        string shaderFileName)
    {
        // DXC numbers HLSL vertex input locations by declaration order inside the
        // entry-point signature, so the pipeline layout must mirror that order and
        // must not declare a location the shader never reads.
        string pass = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" + passFileName);
        string shader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" + shaderFileName);
        string layout = pass[pass.LastIndexOf(
            "private static void AddStaticMeshVertexLayout",
            StringComparison.Ordinal)..];

        string[] declarations = ReadVertexInputDeclarations(shader);
        Assert.NotEmpty(declarations);

        var expected = new List<string>();
        for (int location = 0; location < declarations.Length; location++)
        {
            (string format, string offset) = StaticMeshAttribute(declarations[location]);
            expected.Add(
                $"{location},\n            0,\n            {format},\n            {offset});");
        }

        AssertInOrder(layout, expected.ToArray());
        Assert.Equal(
            expected.Count,
            CountOccurrences(layout, "AddVertexInputAttributeDescription("));
        Assert.Contains(
            $"AddVertexBindingDescription(\n            0,\n            " +
            "MeshAssetCooker.StaticMeshVertexStride,",
            layout,
            StringComparison.Ordinal);
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
    public void OpaqueShaderAppliesDeterministicTintVariationAndBoundedWind()
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" +
            "Vegetation.hlsl");

        Assert.Contains(
            "VEGETATION_FLAG_TINT_VARIATION",
            source,
            StringComparison.Ordinal);
        Assert.Contains("input.ColorVariation", source, StringComparison.Ordinal);
        Assert.Contains(
            "DrawConstants.normalParameters.z",
            source,
            StringComparison.Ordinal);
        Assert.Contains("byteOffset + 32", source, StringComparison.Ordinal);
        Assert.Contains(
            "ComputeVegetationWindLean(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "saturate(\n        windGustParameters.w *",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "DrawConstants.windParameters.x *",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "windParameters.y",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ShadowShaderAppliesTheSameAlphaCoverageContractAsTheOpaqueShader()
    {
        string shader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" +
            "VegetationShadow.hlsl");
        string pass = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationShadowPass.cs");
        int blockStart = shader.IndexOf(
            "[[vk::push_constant]]",
            StringComparison.Ordinal);
        int blockEnd = shader.IndexOf(
            "} DrawConstants;",
            blockStart,
            StringComparison.Ordinal);

        Assert.True(blockStart >= 0 && blockEnd > blockStart);
        string pushBlock = shader[blockStart..blockEnd];
        int vectorCount = pushBlock
            .Split('\n')
            .Count(line => line.TrimStart().StartsWith("float4 ", StringComparison.Ordinal));

        Assert.Equal(7, vectorCount);
        Assert.Equal(112, vectorCount * 16);
        Assert.Contains(
            "// @arisen.material.texture2d BaseColor",
            shader,
            StringComparison.Ordinal);
        Assert.Contains(
            "// @arisen.material.texture2dvariant BaseColor r8g8b8a8unorm.srgb.mips",
            shader,
            StringComparison.Ordinal);
        Assert.Contains("float2 UV : TEXCOORD0", shader, StringComparison.Ordinal);
        Assert.Contains("VEGETATION_FLAG_ALPHA_TEST", shader, StringComparison.Ordinal);
        Assert.Contains(
            "DrawConstants.materialFlags.x",
            shader,
            StringComparison.Ordinal);
        Assert.Contains(
            "DrawConstants.materialParameters.x",
            shader,
            StringComparison.Ordinal);
        Assert.Contains("clip(", shader, StringComparison.Ordinal);
        Assert.Contains(
            "EShaderStage.SHADER_STAGE_FRAGMENT_BIT",
            pass,
            StringComparison.Ordinal);
        Assert.Contains("public const int ByteSize = 112;", pass, StringComparison.Ordinal);
        Assert.Contains("material.AlphaCutoff", pass, StringComparison.Ordinal);
        Assert.Contains("material.BaseColorFactor.W", pass, StringComparison.Ordinal);
        Assert.Contains(
            "BitConverter.UInt32BitsToSingle(material.Flags)",
            pass,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Vegetation.hlsl")]
    [InlineData("VegetationShadow.hlsl")]
    public void WindDisplacementIsSharedAndBoundedByTheVertexHeight(string fileName)
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" + fileName);

        Assert.Contains("float height = max(localHeight, 0.0);", source, StringComparison.Ordinal);
        Assert.Contains("windGustParameters.w *", source, StringComparison.Ordinal);
        Assert.Contains("saturate(", source, StringComparison.Ordinal);
        Assert.Contains("(lean * height) -", source, StringComparison.Ordinal);
        Assert.Contains(
            "float3(0.0, height * (1.0 - sqrt(max(1.0 - lean * lean, 0.0))), 0.0)",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "VEGETATION_GUST_SPATIAL_FREQUENCY = 0.35",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "VEGETATION_GUST_PRIMARY_WEIGHT = 0.6",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "VEGETATION_GUST_SECONDARY_WEIGHT = 0.4",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "VEGETATION_GUST_SECONDARY_PHASE_RATIO = 1.618",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "VEGETATION_GUST_SPATIAL_SECONDARY_SCALE = 0.5",
            source,
            StringComparison.Ordinal);
        Assert.Contains("VEGETATION_FADE_FRACTION = 0.15", source, StringComparison.Ordinal);
        Assert.Contains(
            "float fadeStart = fadeDistance * (1.0 - VEGETATION_FADE_FRACTION);",
            source,
            StringComparison.Ordinal);
        Assert.Contains("clip(input.FadeState.x - input.FadeState.y);", source, StringComparison.Ordinal);
        Assert.Contains(
            "ComputeVegetationWindGust(worldPosition, windPhase)",
            source,
            StringComparison.Ordinal);
        Assert.Contains("nointerpolation float2 FadeState", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// The gust spatial phase is split between the shaders and the CPU: the shaders multiply the
    /// origin-relative position by their spatial literals, and the CPU folds the render origin's
    /// half of the same dot product into the wrapped frame angles so an origin rebase cannot move
    /// the wave. Both halves have to read the same numbers for that split to reconstruct a single
    /// world-anchored wave.
    /// </summary>
    [Theory]
    [InlineData("Vegetation.hlsl")]
    [InlineData("VegetationShadow.hlsl")]
    public void GustSpatialConstantsMatchTheCpuWindContract(string fileName)
    {
        string shader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" + fileName);

        Assert.Equal(
            VegetationWindSettings.GustSpatialFrequency,
            ReadShaderConstant(shader, "VEGETATION_GUST_SPATIAL_FREQUENCY"),
            6);
        Assert.Equal(
            VegetationWindSettings.SecondaryGustSpatialScale,
            ReadShaderConstant(shader, "VEGETATION_GUST_SPATIAL_SECONDARY_SCALE"),
            6);
        Assert.Equal(
            VegetationWindSettings.SecondaryGustFrequencyRatio,
            ReadShaderConstant(shader, "VEGETATION_GUST_SECONDARY_PHASE_RATIO"),
            6);
    }

    [Fact]
    public void FadeCoverageIsResolvedAtTheUndisplacedInstanceOrigin()
    {
        string opaque = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" +
            "Vegetation.hlsl");
        string shadow = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" +
            "VegetationShadow.hlsl");

        Assert.Contains(
            "float3 instanceOrigin = DrawConstants.clusterOriginInstanceBuffer.xyz +",
            opaque,
            StringComparison.Ordinal);
        Assert.Contains(
            "ComputeVegetationWindOffset(\n        instanceOrigin,",
            opaque,
            StringComparison.Ordinal);
        Assert.Contains(
            "output.FadeState = ComputeVegetationFadeState(\n        instanceOrigin,",
            opaque,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "output.FadeState = ComputeVegetationFadeState(\n        worldPosition,",
            opaque,
            StringComparison.Ordinal);

        Assert.Contains(
            "float3 instanceOrigin = DrawConstants.clusterOriginInstanceBuffer.xyz +\n" +
            "        instance.Position;",
            shadow,
            StringComparison.Ordinal);
        Assert.Contains(
            "output.FadeState = ComputeVegetationFadeState(\n        instanceOrigin,",
            shadow,
            StringComparison.Ordinal);
        Assert.DoesNotContain("shadowCameraPosition", shadow, StringComparison.Ordinal);
    }

    [Fact]
    public void ShadowShaderConsumesViewFramePositionsWithoutTheWorldOrigin()
    {
        string shader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Assets/Shaders/" +
            "VegetationShadow.hlsl");
        string pass = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationShadowPass.cs");
        string feature = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationGenericRenderPipelineFeature.cs");
        string prepared = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationPreparedRendering.cs");

        Assert.DoesNotContain(
            "VEGETATION_WIND_SHADOW_CAMERA_VECTOR",
            shader,
            StringComparison.Ordinal);
        Assert.DoesNotContain("renderOrigin", shader, StringComparison.Ordinal);
        Assert.Contains("VEGETATION_WIND_CAMERA_VECTOR = 4u", shader, StringComparison.Ordinal);
        Assert.Contains(
            "dot(shadowPosition, DrawConstants.viewProjectionColumn0)",
            shader,
            StringComparison.Ordinal);
        Assert.Contains("Vector3 viewRelativeClusterOrigin", pass, StringComparison.Ordinal);
        Assert.DoesNotContain("ToRelativeFloat(", pass, StringComparison.Ordinal);
        Assert.Contains(
            "VegetationViewFrame.ToViewRelativePosition(",
            feature,
            StringComparison.Ordinal);
        Assert.Contains(
            "internal static class VegetationViewFrame",
            prepared,
            StringComparison.Ordinal);
        Assert.Contains(
            "public static Vector3 ToViewRelativePosition(",
            prepared,
            StringComparison.Ordinal);
        Assert.Contains(
            "view-frame float range",
            prepared,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ShadowPassPublishesTheFrameDataDependencyItReads()
    {
        string pass = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationShadowPass.cs");
        string opaquePass = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationOpaquePass.cs");
        string feature = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationGenericRenderPipelineFeature.cs");

        Assert.Contains(
            "public void SetFrameDataBuffer(RHIBufferHandle buffer)",
            pass,
            StringComparison.Ordinal);
        Assert.Contains("EAccessFlag.ACCESS_HOST_WRITE_BIT", pass, StringComparison.Ordinal);
        Assert.Contains(
            "EPipelineStageFlagBits.PIPELINE_STAGE_HOST_BIT",
            pass,
            StringComparison.Ordinal);
        Assert.Contains("m_FrameDataBuffer = RHIBufferHandle.Invalid;", pass, StringComparison.Ordinal);
        AssertInOrder(
            pass,
            "RecordFrameDataBarrier(commandList);",
            "commandList.BeginRenderingDepthOnly(");

        Assert.Contains(
            "public RHIBufferHandle FrameConstantsBuffer => m_FrameConstantsBuffer;",
            opaquePass,
            StringComparison.Ordinal);
        Assert.Contains(
            "m_ShadowPass.SetFrameDataBuffer(m_OpaquePass.FrameConstantsBuffer);",
            feature,
            StringComparison.Ordinal);
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

    private static string[] ReadVertexInputDeclarations(string shaderSource)
    {
        string block = SliceBetween(shaderSource, "struct VSInput", "};");
        var declarations = new List<string>();
        foreach (string rawLine in block.Split('\n'))
        {
            string line = rawLine.Trim();
            int separator = line.IndexOf(" : ", StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            string semantic = line[(separator + 3)..].Trim().TrimEnd(';');
            if (semantic.StartsWith("SV_", StringComparison.Ordinal))
            {
                continue;
            }

            int indexStart = semantic.Length;
            while (indexStart > 0 && char.IsDigit(semantic[indexStart - 1]))
            {
                indexStart--;
            }

            declarations.Add(semantic[..indexStart]);
        }

        return declarations.ToArray();
    }

    private static (string Format, string Offset) StaticMeshAttribute(string semantic)
    {
        return semantic switch
        {
            "POSITION" => ("EFormat.FORMAT_R32G32B32_SFLOAT", "0"),
            "NORMAL" => ("EFormat.FORMAT_R32G32B32_SFLOAT", "12"),
            "TANGENT" => ("EFormat.FORMAT_R32G32B32A32_SFLOAT", "24"),
            "TEXCOORD" => ("EFormat.FORMAT_R32G32_SFLOAT", "40"),
            "COLOR" => ("EFormat.FORMAT_R32G32B32_SFLOAT", "48"),
            _ => throw new InvalidOperationException(
                $"Unexpected static mesh vertex semantic '{semantic}'."),
        };
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = source.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = source.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
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

    private static float ReadShaderConstant(string shader, string name)
    {
        Match match = Regex.Match(
            shader,
            $@"static const float {name} = (?<value>[0-9]+(?:\.[0-9]+)?);",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"The vegetation shader is missing {name}.");
        return float.Parse(
            match.Groups["value"].Value,
            System.Globalization.CultureInfo.InvariantCulture);
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
