#include "PipelineExports.h"
#include "../../Core/Core.RHI/RHI/Core/RHIFactory.h"
#include "../../Core/Core.RHI/RHI/Pipeline/RHIPipelineCache.h"
#include "../../Core/Core.RHI/RHI/RenderPass/RHISubPass.h"
#include "../../Core/RHI.Vulkan/Core/RHIVkDevice.h"
#include <unordered_map>
#include <unordered_map>
#include <mutex>
#include "RHINativeBridge.h"

using namespace ArisenEngine;



extern "C" ENGINE_DLL RHI_PipelineManagerHandle RHI_Device_GetPipelineManager(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return nullptr;
    return reinterpret_cast<RHI_PipelineManagerHandle>(dev->GetPipelineCache());
}

extern "C" ENGINE_DLL RHI_PSOHandle RHI_PipelineManager_CreatePSO(RHI_PipelineManagerHandle pm)
{
    auto* mgr = reinterpret_cast<RHI::RHIPipelineCache*>(pm);
    if (mgr == nullptr) return nullptr;
    auto up = mgr->GetPipelineState();
    return reinterpret_cast<RHI_PSOHandle>(up.release());
}

extern "C" ENGINE_DLL void RHI_PSO_Destroy(RHI_PSOHandle pso)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    delete s;
}

extern "C" ENGINE_DLL void RHI_PSO_AddProgram(RHI_PSOHandle pso, RHI_GPUProgramHandle program)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr || program == 0) return;
    auto h = *reinterpret_cast<RHI::RHIShaderProgramHandle*>(&program);
    s->AddProgram(h);
}

extern "C" ENGINE_DLL void RHI_PSO_ClearPrograms(RHI_PSOHandle pso)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->ClearAllPrograms();
}

extern "C" ENGINE_DLL void RHI_PSO_AddVertexBindingDescription(RHI_PSOHandle pso, unsigned int binding, unsigned int stride, RHI::EVertexInputRate inputRate)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->AddVertexBindingDescription(binding, stride, inputRate);
}

extern "C" ENGINE_DLL void RHI_PSO_AddVertexInputAttributeDescription(RHI_PSOHandle pso, unsigned int location, unsigned int binding, RHI::EFormat format, unsigned int offset)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->AddVertexInputAttributeDescription(location, binding, format, offset);
}

extern "C" ENGINE_DLL void RHI_PSO_ClearDescriptorSetLayoutBindings(RHI_PSOHandle pso)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->ClearDescriptorSetLayoutBindings();
}

extern "C" ENGINE_DLL void RHI_PSO_AddDescriptorSetLayoutBinding_Buffers(RHI_PSOHandle pso, unsigned int layoutIndex, unsigned int binding, RHI::EDescriptorType type, unsigned int descriptorCount, unsigned int shaderStageFlags, Containers::Vector<RHI::RHIBufferHandle>* buffers)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr || buffers == nullptr) return;
    s->AddDescriptorSetLayoutBinding(layoutIndex, binding, type, descriptorCount, shaderStageFlags, std::move(*buffers));
}

extern "C" ENGINE_DLL void RHI_PSO_AddDescriptorSetLayoutBinding_Images(RHI_PSOHandle pso, unsigned int layoutIndex, unsigned int binding, RHI::EDescriptorType type, unsigned int descriptorCount, unsigned int shaderStageFlags, Containers::Vector<RHI::RHIDescriptorImageInfo>* images)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr || images == nullptr) return;
    s->AddDescriptorSetLayoutBinding(layoutIndex, binding, type, descriptorCount, shaderStageFlags, std::move(*images));
}

extern "C" ENGINE_DLL void RHI_PSO_UpdateDescriptorSet_Buffers(RHI_PSOHandle pso, unsigned int layoutIndex, unsigned int binding, Containers::Vector<RHI::RHIBufferHandle>* buffers)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr || buffers == nullptr) return;
    s->UpdateDescriptorSet(layoutIndex, binding, std::move(*buffers));
}

extern "C" ENGINE_DLL void RHI_PSO_UpdateDescriptorSet_Images(RHI_PSOHandle pso, unsigned int layoutIndex, unsigned int binding, Containers::Vector<RHI::RHIDescriptorImageInfo>* images)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr || images == nullptr) return;
    s->UpdateDescriptorSet(layoutIndex, binding, std::move(*images));
}

extern "C" ENGINE_DLL void RHI_PSO_BuildDescriptorSetLayout(RHI_PSOHandle pso)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->BuildDescriptorSetLayout();
}

extern "C" ENGINE_DLL void RHI_PSO_AddDynamicState(RHI_PSOHandle pso, RHI::EDynamicPipelineState state)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->AddDynamicPipelineState(state);
}

extern "C" ENGINE_DLL void RHI_PSO_SetPrimitiveState(RHI_PSOHandle pso, RHI::EPrimitiveTopology topology, bool primitiveRestart)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->SetPrimitiveState(topology, primitiveRestart);
}

extern "C" ENGINE_DLL void RHI_PSO_SetDepthClampEnable(RHI_PSOHandle pso, bool enable)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->SetDepthClampEnable(enable);
}

extern "C" ENGINE_DLL void RHI_PSO_SetRasterizerDiscardEnable(RHI_PSOHandle pso, bool enable)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->SetRasterizerDiscardEnable(enable);
}

extern "C" ENGINE_DLL void RHI_PSO_SetPolygonMode(RHI_PSOHandle pso, RHI::EPolygonMode mode)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->SetPolygonMode(mode);
}

extern "C" ENGINE_DLL void RHI_PSO_SetLineWidth(RHI_PSOHandle pso, float lineWidth)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->SetLineWidth(lineWidth);
}

extern "C" ENGINE_DLL void RHI_PSO_SetCullMode(RHI_PSOHandle pso, RHI::ECullModeFlagBits cull)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->SetCullMode(cull);
}

extern "C" ENGINE_DLL void RHI_PSO_SetFrontFace(RHI_PSOHandle pso, RHI::EFrontFace face)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->SetFrontFace(face);
}

extern "C" ENGINE_DLL void RHI_PSO_SetDepthBiasEnable(RHI_PSOHandle pso, bool enable)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->SetDepthBiasEnable(enable);
}

extern "C" ENGINE_DLL void RHI_PSO_SetSampleShading(RHI_PSOHandle pso, bool enable)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->SetSampleShading(enable);
}

extern "C" ENGINE_DLL void RHI_PSO_SetSampleCount(RHI_PSOHandle pso, RHI::ESampleCountFlagBits sample)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->SetSampleCount(sample);
}

extern "C" ENGINE_DLL void RHI_PSO_AddBlendAttachmentState_Simple(RHI_PSOHandle pso, bool enable, unsigned int writeMask)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->AddBlendAttachmentState(enable, writeMask);
}

extern "C" ENGINE_DLL void RHI_PSO_SetLogicOp(RHI_PSOHandle pso, bool enable, RHI::ELogicOp op)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->SetLogicOp(enable, op);
}

extern "C" ENGINE_DLL void RHI_PSO_SetBlendConstants(RHI_PSOHandle pso, float r, float g, float b, float a)
{
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (s == nullptr) return;
    s->SetBlendConstants(r, g, b, a);
}

extern "C" ENGINE_DLL void RHI_PSO_SetRenderingFormats(RHI_PSOHandle pso, ArisenEngine::Containers::Vector<ArisenEngine::RHI::EFormat>* colorFormats, ArisenEngine::RHI::EFormat depthFormat, ArisenEngine::RHI::EFormat stencilFormat)
{
    auto* pipelineState = reinterpret_cast<ArisenEngine::RHI::RHIPipelineState*>(pso);
    if (pipelineState == nullptr) return;
    if (colorFormats)
    {
        pipelineState->SetRenderingFormats(*colorFormats, depthFormat, stencilFormat);
    }
    else
    {
         pipelineState->SetRenderingFormats({}, depthFormat, stencilFormat);
    }
}

extern "C" ENGINE_DLL RHI_PipelineHandle RHI_PipelineManager_GetGraphicsPipeline(RHI_PipelineManagerHandle pm, RHI_PSOHandle pso)
{
    auto* mgr = reinterpret_cast<RHI::RHIPipelineCache*>(pm);
    auto* s = reinterpret_cast<RHI::RHIPipelineState*>(pso);
    if (mgr == nullptr || s == nullptr) return 0;
    auto handle = mgr->GetGraphicsPipeline(s);
    return *reinterpret_cast<unsigned long long*>(&handle);
}

// Moved to HandlesExports: CreateRenderPass (was GetRenderPass)

extern "C" ENGINE_DLL void RHI_RenderPass_Free(RHI_DeviceHandle device, RHI_RenderPassHandle rp, unsigned int frameIndex)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || rp == 0) return;
    auto h = *reinterpret_cast<RHI::RHIRenderPassHandle*>(&rp);
    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (vkDev) {
        auto* r = RHI::RHINativeBridge::GetRenderPassItem(vkDev, h);
        if (r) {
            // Logic to free or deallocate
            // dev->GetFactory()->ReleaseRenderPass(h);
        }
    }
    (void)frameIndex;
}

extern "C" ENGINE_DLL void RHI_RenderPass_AddAttachmentAction(RHI_DeviceHandle device, RHI_RenderPassHandle rp, RHI::EFormat format, RHI::ESampleCountFlagBits samples, RHI::EAttachmentLoadOp colorLoad, RHI::EAttachmentStoreOp colorStore, RHI::EAttachmentLoadOp stencilLoad, RHI::EAttachmentStoreOp stencilStore, RHI::EImageLayout initialLayout, RHI::EImageLayout finalLayout)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || rp == 0) return;
    auto h = *reinterpret_cast<RHI::RHIRenderPassHandle*>(&rp);
    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (!vkDev) return;

    auto* r = RHI::RHINativeBridge::GetRenderPassItem(vkDev, h);
    if (r && r->renderPassObj) {
        auto* rpObj = static_cast<RHI::RHIRenderPass*>(r->renderPassObj);
        rpObj->AddAttachmentAction(format, samples, colorLoad, colorStore, stencilLoad, stencilStore, initialLayout, finalLayout);
    }
}

extern "C" ENGINE_DLL RHI_SubpassHandle RHI_RenderPass_AddSubPass(RHI_DeviceHandle device, RHI_RenderPassHandle rp)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (!dev) return nullptr;
    auto h = *reinterpret_cast<RHI::RHIRenderPassHandle*>(&rp);
    
    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (vkDev) {
        auto* item = RHI::RHINativeBridge::GetRenderPassItem(vkDev, h);
        if (item && item->renderPassObj) {
            auto* r = static_cast<RHI::RHIRenderPass*>(item->renderPassObj);
            return reinterpret_cast<RHI_SubpassHandle>(r->AddSubPass());
        }
    }
    return nullptr;
}

extern "C" ENGINE_DLL void RHI_Subpass_SetDependency(RHI_SubpassHandle sp, unsigned int prevIndex, unsigned int prevStage, unsigned int prevAccessMask, unsigned int currStage, unsigned int currAccessMask, unsigned int syncFlag)
{
    auto* s = reinterpret_cast<RHI::RHISubPass*>(sp);
    if (s == nullptr) return;
    s->SetDependency(prevIndex, prevStage, prevAccessMask, currStage, currAccessMask, syncFlag);
}

extern "C" ENGINE_DLL void RHI_Subpass_SetBindPoint(RHI_SubpassHandle sp, RHI::EPipelineBindPoint bindPoint)
{
    auto* s = reinterpret_cast<RHI::RHISubPass*>(sp);
    if (s == nullptr) return;
    s->SetBindPoint(bindPoint);
}

extern "C" ENGINE_DLL void RHI_Subpass_AddColorReference(RHI_SubpassHandle sp, unsigned int index, RHI::EImageLayout layout)
{
    auto* s = reinterpret_cast<RHI::RHISubPass*>(sp);
    if (s == nullptr) return;
    s->AddColorReference(index, layout);
}

extern "C" ENGINE_DLL void RHI_Subpass_SetDescriptionFlag(RHI_SubpassHandle sp, unsigned int flag)
{
    auto* s = reinterpret_cast<RHI::RHISubPass*>(sp);
    if (s == nullptr) return;
    s->SetSubPassDescriptionFlag(flag);
}

extern "C" ENGINE_DLL void RHI_RenderPass_Alloc(RHI_DeviceHandle device, RHI_RenderPassHandle rp, unsigned int frameIndex)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (!dev) return;
    auto h = *reinterpret_cast<RHI::RHIRenderPassHandle*>(&rp);

    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (vkDev) {
        auto* item = RHI::RHINativeBridge::GetRenderPassItem(vkDev, h);
        if (item && item->renderPassObj) {
            auto* r = static_cast<RHI::RHIRenderPass*>(item->renderPassObj);
            r->AllocRenderPass(frameIndex);
            
            // Sync the cached VkRenderPass handle in the pool item
            item->renderPass = static_cast<VkRenderPass>(r->GetHandle(frameIndex));
        }
    }
}

extern "C" ENGINE_DLL void RHI_Pipeline_AllocGraphics(RHI_DeviceHandle device, RHI_PipelineHandle pipeline, unsigned int frameIndex, RHI_SubpassHandle subpass)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (!dev) return;
    auto h = *reinterpret_cast<RHI::RHIPipelineHandle*>(&pipeline);

    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (vkDev) {
        auto* item = RHI::RHINativeBridge::GetPipelineItem(vkDev, h);
        if (item && item->pipeline) {
             auto* sub = reinterpret_cast<RHI::RHISubPass*>(subpass);
             item->pipeline->AllocGraphicPipeline(frameIndex, sub);
        }
    }
}

// Moved to HandlesExports: ReleaseRenderPass



