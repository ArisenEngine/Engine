#include "RHI/Pipeline/RHIPipeline.h"
#include "RHI/Pipeline/RHIPipelineCache.h"
#include "RHI/Pipeline/RHIPipelineState.h"
#include "Base/BindingMacros.h"

using namespace ArisenEngine::RHI;

ARISEN_BIND_BEGIN_BRIDGE("RHIPipeline", "Core.RHI.dll", "Arisen.Native.RHI")

extern "C" {
RHI_DLL void* RHIPipelineCache_GetGraphicsPipeline(RHIPipelineCache* cache, RHIPipelineState* pso)
{
    RHIPipelineHandle handle = cache->GetGraphicsPipeline(pso);
    uint64_t result = 0;
    std::memcpy(&result, &handle, sizeof(handle));
    return (void*)result;
}

RHI_DLL void* RHIPipelineCache_GetComputePipeline(RHIPipelineCache* cache, RHIPipelineState* pso)
{
    RHIPipelineHandle handle = cache->GetComputePipeline(pso);
    uint64_t result = 0;
    std::memcpy(&result, &handle, sizeof(handle));
    return (void*)result;
}

RHI_DLL void* RHIPipelineCache_GetPipelineState(RHIPipelineCache* cache)
{
    return cache->GetPipelineState().release();
}

RHI_DLL void RHIPipelineState_AddProgram(RHIPipelineState* pso, uint32_t index, uint32_t generation)
{
    RHIShaderProgramHandle handle;
    handle.index = index;
    handle.generation = generation;
    pso->AddProgram(handle);
}

RHI_DLL void RHIPipelineState_SetBindPoint(RHIPipelineState* pso, int bindPoint)
{
    pso->SetBindPoint(static_cast<EPipelineBindPoint>(bindPoint));
}

RHI_DLL void RHIPipelineState_SetInputAssemblyState(RHIPipelineState* pso, int topology, int primitiveRestart)
{
    RHIInputAssemblyState state{};
    state.topology = static_cast<EPrimitiveTopology>(topology);
    state.primitiveRestartEnable = primitiveRestart != 0;
    pso->SetInputAssemblyState(state);
}

RHI_DLL void RHIPipelineState_SetRasterizationState(RHIPipelineState* pso, int polygonMode, int cullMode, int frontFace)
{
    RHIRasterizationState state{};
    state.polygonMode = static_cast<EPolygonMode>(polygonMode);
    state.cullMode = static_cast<ECullModeFlagBits>(cullMode);
    state.frontFace = static_cast<EFrontFace>(frontFace);
    state.depthClampEnable = false;
    state.rasterizerDiscardEnable = false;
    state.depthBiasEnable = false;
    state.lineWidth = 1.0f;
    pso->SetRasterizationState(state);
}

RHI_DLL void RHIPipelineState_SetColorBlendState(RHIPipelineState* pso, int blendEnable, int srcColor, int dstColor,
                                                 int colorOp)
{
    RHIColorBlendState state{};
    RHIColorBlendAttachmentState attachment{};
    attachment.blendEnable = blendEnable != 0;
    attachment.srcColorBlendFactor = static_cast<EBlendFactor>(srcColor);
    attachment.dstColorBlendFactor = static_cast<EBlendFactor>(dstColor);
    attachment.colorBlendOp = static_cast<EBlendOp>(colorOp);
    attachment.srcAlphaBlendFactor = static_cast<EBlendFactor>(srcColor);
    attachment.dstAlphaBlendFactor = static_cast<EBlendFactor>(dstColor);
    attachment.alphaBlendOp = static_cast<EBlendOp>(colorOp);
    attachment.colorWriteMask = 0xF; // RGBA
    state.attachments.push_back(attachment);
    pso->SetColorBlendState(state);
}

RHI_DLL void RHIPipelineState_SetDynamicStateMask(RHIPipelineState* pso, uint64_t mask)
{
    pso->SetDynamicStateMask(mask);
}

RHI_DLL void RHIPipelineState_SetRenderingFormats(RHIPipelineState* pso, const int* colorFormats, uint32_t colorCount,
                                                  int depthFormat)
{
    ArisenEngine::Containers::Vector<EFormat> formats;
    for (uint32_t i = 0; i < colorCount; ++i) formats.push_back(static_cast<EFormat>(colorFormats[i]));
    pso->SetRenderingFormats(formats, static_cast<EFormat>(depthFormat), EFormat::FORMAT_UNDEFINED);
}

RHI_DLL void RHIPipelineState_UpdateDescriptorSetBuffer(RHIPipelineState* pso, uint32_t layoutIndex, uint32_t binding,
                                                        const uint32_t* indices, const uint32_t* generations,
                                                        uint32_t count)
{
    ArisenEngine::Containers::Vector<RHIBufferHandle> buffers;
    for (uint32_t i = 0; i < count; ++i)
    {
        RHIBufferHandle h;
        h.index = indices[i];
        h.generation = generations[i];
        buffers.push_back(h);
    }
    pso->UpdateDescriptorSet(layoutIndex, binding, std::move(buffers));
}

RHI_DLL void RHIPipelineState_BuildDescriptorSetLayout(RHIPipelineState* pso)
{
    pso->BuildDescriptorSetLayout();
}

RHI_DLL void RHIPipelineState_Delete(RHIPipelineState* pso)
{
    delete pso;
}
} // extern "C"

ARISEN_BIND_END_BRIDGE()
