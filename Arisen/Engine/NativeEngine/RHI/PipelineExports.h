#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.RHI/RHI/RenderPass/RHIRenderPass.h"
#include "../../Core/Core.RHI/RHI/Pipeline/RHIPipeline.h"
#include "../../Core/Core.RHI/RHI/Pipeline/RHIPipelineState.h"

#include "RHIHandleExports.h"

extern "C" ENGINE_DLL RHI_PipelineManagerHandle RHI_Device_GetPipelineManager(RHI_DeviceHandle device);
extern "C" ENGINE_DLL RHI_PSOHandle RHI_PipelineManager_CreatePSO(RHI_PipelineManagerHandle pm);
extern "C" ENGINE_DLL void RHI_PSO_Destroy(RHI_PSOHandle pso);
extern "C" ENGINE_DLL void RHI_PSO_AddProgram(RHI_PSOHandle pso, RHI_GPUProgramHandle program);
extern "C" ENGINE_DLL void RHI_PSO_ClearPrograms(RHI_PSOHandle pso);
extern "C" ENGINE_DLL void RHI_PSO_AddVertexBindingDescription(RHI_PSOHandle pso, unsigned int binding, unsigned int stride, ArisenEngine::RHI::EVertexInputRate inputRate);
extern "C" ENGINE_DLL void RHI_PSO_AddVertexInputAttributeDescription(RHI_PSOHandle pso, unsigned int location, unsigned int binding, ArisenEngine::RHI::EFormat format, unsigned int offset);
extern "C" ENGINE_DLL void RHI_PSO_ClearDescriptorSetLayoutBindings(RHI_PSOHandle pso);
extern "C" ENGINE_DLL void RHI_PSO_AddDescriptorSetLayoutBinding_Buffers(RHI_PSOHandle pso, unsigned int layoutIndex, unsigned int binding, ArisenEngine::RHI::EDescriptorType type, unsigned int descriptorCount, unsigned int shaderStageFlags, ArisenEngine::Containers::Vector<ArisenEngine::RHI::RHIBufferHandle>* buffers);
extern "C" ENGINE_DLL void RHI_PSO_AddDescriptorSetLayoutBinding_Images(RHI_PSOHandle pso, unsigned int layoutIndex, unsigned int binding, ArisenEngine::RHI::EDescriptorType type, unsigned int descriptorCount, unsigned int shaderStageFlags, ArisenEngine::Containers::Vector<ArisenEngine::RHI::RHIDescriptorImageInfo>* images);
extern "C" ENGINE_DLL void RHI_PSO_UpdateDescriptorSet_Buffers(RHI_PSOHandle pso, unsigned int layoutIndex, unsigned int binding, ArisenEngine::Containers::Vector<ArisenEngine::RHI::RHIBufferHandle>* buffers);
extern "C" ENGINE_DLL void RHI_PSO_UpdateDescriptorSet_Images(RHI_PSOHandle pso, unsigned int layoutIndex, unsigned int binding, ArisenEngine::Containers::Vector<ArisenEngine::RHI::RHIDescriptorImageInfo>* images);
extern "C" ENGINE_DLL void RHI_PSO_BuildDescriptorSetLayout(RHI_PSOHandle pso);
extern "C" ENGINE_DLL void RHI_PSO_SetBindPoint(RHI_PSOHandle pso, ArisenEngine::RHI::EPipelineBindPoint bindPoint);
extern "C" ENGINE_DLL void RHI_PSO_AddDynamicState(RHI_PSOHandle pso, ArisenEngine::RHI::EDynamicPipelineState state);
extern "C" ENGINE_DLL void RHI_PSO_SetPrimitiveState(RHI_PSOHandle pso, ArisenEngine::RHI::EPrimitiveTopology topology, bool primitiveRestart);
extern "C" ENGINE_DLL void RHI_PSO_SetDepthClampEnable(RHI_PSOHandle pso, bool enable);
extern "C" ENGINE_DLL void RHI_PSO_SetRasterizerDiscardEnable(RHI_PSOHandle pso, bool enable);
extern "C" ENGINE_DLL void RHI_PSO_SetPolygonMode(RHI_PSOHandle pso, ArisenEngine::RHI::EPolygonMode mode);
extern "C" ENGINE_DLL void RHI_PSO_SetLineWidth(RHI_PSOHandle pso, float lineWidth);
extern "C" ENGINE_DLL void RHI_PSO_SetCullMode(RHI_PSOHandle pso, ArisenEngine::RHI::ECullModeFlagBits cull);
extern "C" ENGINE_DLL void RHI_PSO_SetFrontFace(RHI_PSOHandle pso, ArisenEngine::RHI::EFrontFace face);
extern "C" ENGINE_DLL void RHI_PSO_SetDepthBiasEnable(RHI_PSOHandle pso, bool enable);
extern "C" ENGINE_DLL void RHI_PSO_SetSampleShading(RHI_PSOHandle pso, bool enable);
extern "C" ENGINE_DLL void RHI_PSO_SetSampleCount(RHI_PSOHandle pso, ArisenEngine::RHI::ESampleCountFlagBits sample);
extern "C" ENGINE_DLL void RHI_PSO_AddBlendAttachmentState_Simple(RHI_PSOHandle pso, bool enable, unsigned int writeMask);
extern "C" ENGINE_DLL void RHI_PSO_SetLogicOp(RHI_PSOHandle pso, bool enable, ArisenEngine::RHI::ELogicOp op);
extern "C" ENGINE_DLL void RHI_PSO_SetBlendConstants(RHI_PSOHandle pso, float r, float g, float b, float a);
extern "C" ENGINE_DLL void RHI_PSO_SetDepthStencilState(RHI_PSOHandle pso, const ArisenEngine::RHI::RHIDepthStencilState* state);
extern "C" ENGINE_DLL void RHI_PSO_SetRenderingFormats(RHI_PSOHandle pso, ArisenEngine::Containers::Vector<ArisenEngine::RHI::EFormat>* colorFormats, ArisenEngine::RHI::EFormat depthFormat, ArisenEngine::RHI::EFormat stencilFormat);
extern "C" ENGINE_DLL RHI_PipelineHandle RHI_PipelineManager_GetGraphicsPipeline(RHI_PipelineManagerHandle pm, RHI_PSOHandle pso);
extern "C" ENGINE_DLL void RHI_Pipeline_AllocGraphics(RHI_DeviceHandle device, RHI_PipelineHandle pipeline, unsigned int frameIndex, RHI_SubpassHandle subpass);
extern "C" ENGINE_DLL void RHI_Pipeline_AllocCompute(RHI_DeviceHandle device, RHI_PipelineHandle pipeline, unsigned int frameIndex);

// Moved to HandlesExports: CreateRenderPass, ReleaseRenderPass
extern "C" ENGINE_DLL void RHI_RenderPass_Free(RHI_DeviceHandle device, RHI_RenderPassHandle rp, unsigned int frameIndex);
extern "C" ENGINE_DLL void RHI_RenderPass_AddAttachmentAction(RHI_DeviceHandle device, RHI_RenderPassHandle rp, ArisenEngine::RHI::EFormat format, ArisenEngine::RHI::ESampleCountFlagBits samples, ArisenEngine::RHI::EAttachmentLoadOp colorLoad, ArisenEngine::RHI::EAttachmentStoreOp colorStore, ArisenEngine::RHI::EAttachmentLoadOp stencilLoad, ArisenEngine::RHI::EAttachmentStoreOp stencilStore, ArisenEngine::RHI::EImageLayout initialLayout, ArisenEngine::RHI::EImageLayout finalLayout);
extern "C" ENGINE_DLL RHI_SubpassHandle RHI_RenderPass_AddSubPass(RHI_DeviceHandle device, RHI_RenderPassHandle rp);
extern "C" ENGINE_DLL void RHI_Subpass_SetDependency(RHI_SubpassHandle sp, unsigned int prevIndex, unsigned int prevStage, unsigned int prevAccessMask, unsigned int currStage, unsigned int currAccessMask, unsigned int syncFlag);
extern "C" ENGINE_DLL void RHI_Subpass_SetBindPoint(RHI_SubpassHandle sp, ArisenEngine::RHI::EPipelineBindPoint bindPoint);
extern "C" ENGINE_DLL void RHI_Subpass_AddColorReference(RHI_SubpassHandle sp, unsigned int index, ArisenEngine::RHI::EImageLayout layout);
extern "C" ENGINE_DLL void RHI_Subpass_SetResolveReference(RHI_SubpassHandle sp, unsigned int index, ArisenEngine::RHI::EImageLayout layout);
extern "C" ENGINE_DLL void RHI_Subpass_SetDepthStencilReference(RHI_SubpassHandle sp, unsigned int index, ArisenEngine::RHI::EImageLayout layout);
extern "C" ENGINE_DLL void RHI_Subpass_SetDescriptionFlag(RHI_SubpassHandle sp, unsigned int flag);
extern "C" ENGINE_DLL void RHI_RenderPass_Alloc(RHI_DeviceHandle device, RHI_RenderPassHandle rp, unsigned int frameIndex);

// Moved to SurfaceExports: RHI_FrameBuffer_SetAttachment



