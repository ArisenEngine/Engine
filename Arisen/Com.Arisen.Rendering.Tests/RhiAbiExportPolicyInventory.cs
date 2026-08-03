namespace Com.Arisen.Rendering.Tests;

[Flags]
internal enum RhiAbiInputPolicy
{
    None = 0,
    Handle = 1 << 0,
    EnumOrFlags = 1 << 1,
    RangeOrIdentity = 1 << 2
}

internal sealed record RhiAbiExportPolicy(
    string FileName,
    string ExportName,
    RhiAbiInputPolicy InputPolicy,
    IReadOnlySet<string> OptionalPointers);

internal static class RhiAbiExportPolicyInventory
{
    public static IReadOnlyDictionary<string, RhiAbiExportPolicy> All { get; } = Parse(
        """
        [RHICommandBufferBridge.cpp]
        RHICommandBuffer_Begin|-
        RHICommandBuffer_End|-
        RHICommandBuffer_BeginRenderPass|HE
        RHICommandBuffer_EndRenderPass|-
        RHICommandBuffer_BindPipeline|H
        RHICommandBuffer_SetViewport|R
        RHICommandBuffer_SetScissor|R
        RHICommandBuffer_BindVertexBuffers|HR
        RHICommandBuffer_BindIndexBuffer|HER
        RHICommandBuffer_Draw|-
        RHICommandBuffer_DrawIndexed|-
        RHICommandBuffer_PipelineBarrier|HER
        RHICommandBuffer_TransitionImageLayout|HE
        RHICommandBuffer_TransitionImageLayoutExplicit|HE
        RHICommandBuffer_TransitionImageLayoutWithQueueFamily|HE
        RHICommandBuffer_BindDescriptorSets|HER
        RHICommandBuffer_PushConstants|ER
        RHICommandBuffer_CopyBuffer|HR
        RHICommandBuffer_CopyBufferToImage2DSubresource|HER
        RHICommandBuffer_CopyBufferToImage2D|HER
        RHICommandBuffer_CopyImageToBuffer2D|HER
        RHICommandBuffer_BeginDebugLabel|R
        RHICommandBuffer_EndDebugLabel|-
        RHICommandBuffer_InsertDebugMarker|R
        RHICommandBuffer_Dispatch|-
        RHICommandBuffer_BindDescriptorSet|HER
        RHICommandBuffer_BeginRendering|HER
        RHICommandBuffer_BeginRenderingWithDepth|HER
        RHICommandBuffer_BeginRenderingDepthOnly|HER
        RHICommandBuffer_EndRendering|-

        [RHICommandBufferPoolBridge.cpp]
        RHICommandBufferPool_GetCommandBuffer|E
        RHICommandBufferPool_ReleaseCommandBuffer|H

        [RHIDescriptorPoolBridge.cpp]
        RHIDescriptorPool_AddPool|ER
        RHIDescriptorPool_ResetPool|R
        RHIDescriptorPool_AllocDescriptorSet|R
        RHIDescriptorPool_UpdateDescriptorSet|R

        [RHIDeviceBridge.cpp]
        RHIDevice_DeviceWaitIdle|-
        RHIDevice_GraphicQueueWaitIdle|-
        RHIDevice_GetMaxFramesInFlight|-
        RHIDevice_GetFactory|-
        RHIDevice_GetInstance|-
        RHIDevice_SetResolution|R
        RHIDevice_SetObjectName|ER
        RHIDevice_GetCapabilities|-
        RHIDevice_GetCommandBuffer|H
        RHIDevice_GetCommandBufferPool|H
        RHIDevice_GetCompletedSubmitTicket|-
        RHIDevice_WaitQueueTicket|R
        RHIDevice_Submit|H|bridgeDesc
        RHIDevice_GetQueue|E
        RHIDevice_GetPipelineCache|-
        RHIDevice_GetSurface|-
        RHIDevice_GetDescriptorPool|-
        RHIDevice_GetDescriptorPoolHandle|-
        RHIDevice_GetSharedWin32Handle|H

        [RHIFactoryBindlessBridge.cpp]
        RHIFactory_RegisterBindlessResourceSampler|H
        RHIFactory_UnregisterBindlessResourceImage|R
        RHIFactory_UnregisterBindlessResourceBuffer|R
        RHIFactory_UnregisterBindlessResourceSampler|R

        [RHIFactoryBridge.cpp]
        RHIFactory_CreateBuffer|ER|name
        RHIFactory_ReleaseBuffer|H
        RHIFactory_BufferMemoryCopy|HR
        RHIFactory_MapBuffer|H
        RHIFactory_UnmapBuffer|H
        RHIFactory_GetBufferSize|H
        RHIFactory_GetBufferDeviceAddress|H
        RHIFactory_CreateImage|ER|name
        RHIFactory_ReleaseImage|H
        RHIFactory_CreateImageView|HER
        RHIFactory_ReleaseImageView|H
        RHIFactory_CreateSampler|ER
        RHIFactory_ReleaseSampler|H
        RHIFactory_CreateSemaphore|-
        RHIFactory_ReleaseSemaphore|H
        RHIFactory_CreateRenderPass|-
        RHIFactory_ReleaseRenderPass|H
        RHIFactory_CreateFrameBuffer|-
        RHIFactory_ReleaseFrameBuffer|H
        RHIFactory_CreateCommandBufferPool|E
        RHIFactory_ReleaseCommandBufferPool|H
        RHIFactory_GetImageViewFormat|H
        RHIFactory_GetImageViewWidth|H
        RHIFactory_GetImageViewHeight|H
        RHIFactory_CreateGPUProgram|-
        RHIFactory_ReleaseGPUProgram|H
        RHIFactory_AttachProgramByteCode|HER|entryPoint
        RHIFactory_RegisterBindlessResourceImage|H
        RHIFactory_RegisterBindlessResourceBuffer|H
        RHIFactory_BufferMemoryCopyAsync|HR
        RHIFactory_FlushTransfers|-
        RHIFactory_UpdateTransfers|-

        [RHIInstanceBridge.cpp]
        RHIInstance_PickPhysicalDevice|-
        RHIInstance_InitLogicDevices|-
        RHIInstance_CreateSurface|R
        RHIInstance_DestroySurface|-
        RHIInstance_SetResolution|R
        RHIInstance_GetLogicalDevice|R
        RHIInstance_GetSurface|-
        RHIInstance_CreateLogicDevice|-
        RHIInstance_IsPhysicalDeviceAvailable|-
        RHIInstance_IsSurfacesAvailable|-
        RHIInstance_GetMaxFramesInFlight|-
        RHIInstance_IsEnableValidation|-
        RHIInstance_GetExternalIndex|-
        RHIInstance_IsSupportLinearColorSpace|-
        RHIInstance_PresentModeSupported|E
        RHIInstance_SetCurrentPresentMode|E
        RHIInstance_GetSuitableSwapChainFormat|-
        RHIInstance_GetSuitablePresentMode|-
        RHIInstance_GetAdapterName|-
        RHIInstance_GetAdapterTypeName|-
        RHIInstance_GetAdapterDriverInfo|-
        RHIInstance_GetEnabledInstanceExtensions|-
        RHIInstance_GetEnabledDeviceExtensions|-
        RHIInstance_GetMissingDeviceExtensions|-

        [RHILoaderBridge.cpp]
        RHILoader_SetCurrentGraphicsAPI|E
        RHILoader_CreateInstance|R
        RHILoader_Dispose|-

        [RHILoaderDiagnosticsBridge.cpp]
        RHILoader_GetLastErrorMessage|-

        [RHIPipelineBridge.cpp]
        RHIPipelineCache_GetGraphicsPipeline|R
        RHIPipelineCache_GetComputePipeline|R
        RHIPipelineCache_ReleasePipeline|H
        RHIPipelineCache_GetPipelineState|-
        RHIPipelineState_AddProgram|H
        RHIPipelineState_SetBindPoint|E
        RHIPipelineState_SetInputAssemblyState|E
        RHIPipelineState_AddVertexBindingDescription|E
        RHIPipelineState_AddVertexInputAttributeDescription|E
        RHIPipelineState_ClearVertexInputDescriptions|-
        RHIPipelineState_SetRasterizationState|E
        RHIPipelineState_SetRasterizationStateWithDepthBias|ER
        RHIPipelineState_SetColorBlendState|E
        RHIPipelineState_SetDepthStencilState|E
        RHIPipelineState_SetDynamicStateMask|E
        RHIPipelineState_SetRenderingFormats|E
        RHIPipelineState_UpdateDescriptorSetBuffer|HR
        RHIPipelineState_BuildDescriptorSetLayout|-
        RHIPipelineState_Delete|-

        [RHIQueueBridge.cpp]
        RHIQueue_Submit|HE|descriptor
        RHIQueue_Update|-
        RHIQueue_GetCompletedTicket|-
        RHIQueue_GetLatestTicket|-
        RHIQueue_WaitForTicket|R
        RHIQueue_GetType|-

        [RHISurfaceBridge.cpp]
        RHISurface_InitSwapChain|-
        RHISurface_GetSwapChain|-
        RHISurface_SetResolution|R
        RHISurface_TrySetResolution|R

        [RHISwapChainBridge.cpp]
        RHISwapChain_BeginFrame|-
        RHISwapChain_EndFrame|-
        RHISwapChain_RetireFrame|-
        RHISwapChain_GetImageView|-
        RHISwapChain_GetSharedWin32Handle|-
        RHISwapChain_GetSharedMemorySize|-
        RHISwapChain_GetRenderFinishedSemaphoreWin32Handle|-
        RHISwapChain_CreateConsumedSemaphoreWin32Handle|-
        RHISwapChain_CompleteConsumedSemaphoreWin32Handle|-
        RHISwapChain_ReleaseConsumedSemaphoreWin32Handle|-
        RHISwapChain_AcknowledgeExternalConsumerRelease|-
        RHISwapChain_SetResolution|R
        """);

    private static IReadOnlyDictionary<string, RhiAbiExportPolicy> Parse(string source)
    {
        var policies = new Dictionary<string, RhiAbiExportPolicy>(StringComparer.Ordinal);
        string? currentFile = null;

        foreach (string rawLine in source.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("[", StringComparison.Ordinal) &&
                line.EndsWith("]", StringComparison.Ordinal))
            {
                currentFile = line[1..^1];
                continue;
            }

            if (currentFile == null)
            {
                throw new InvalidDataException($"RHI ABI policy '{line}' has no file section.");
            }

            string[] fields = line.Split('|');
            if (fields.Length is < 2 or > 3)
            {
                throw new InvalidDataException($"Malformed RHI ABI policy line '{line}'.");
            }

            RhiAbiInputPolicy inputPolicy = ParseInputPolicy(fields[1]);
            IReadOnlySet<string> optionalPointers = fields.Length == 3
                ? fields[2]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            var policy = new RhiAbiExportPolicy(
                currentFile,
                fields[0],
                inputPolicy,
                optionalPointers);
            if (!policies.TryAdd(policy.ExportName, policy))
            {
                throw new InvalidDataException(
                    $"Duplicate RHI ABI export policy for '{policy.ExportName}'.");
            }
        }

        return policies;
    }

    private static RhiAbiInputPolicy ParseInputPolicy(string value)
    {
        if (value == "-")
        {
            return RhiAbiInputPolicy.None;
        }

        RhiAbiInputPolicy policy = RhiAbiInputPolicy.None;
        foreach (char marker in value)
        {
            policy |= marker switch
            {
                'H' => RhiAbiInputPolicy.Handle,
                'E' => RhiAbiInputPolicy.EnumOrFlags,
                'R' => RhiAbiInputPolicy.RangeOrIdentity,
                _ => throw new InvalidDataException(
                    $"Unknown RHI ABI input-policy marker '{marker}' in '{value}'.")
            };
        }

        return policy;
    }
}
