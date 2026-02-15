#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.RHI/RHI/Commands/RHICommandBuffer.h"

#include "RHITypesExports.h"

/** @ownership Owned - Caller must release via RHI_Device_ReleaseCommandBufferPool */
extern "C" ENGINE_DLL [[deprecated("Use RHIDevice::GetFactory()->CreateCommandBufferPool instead")]] RHI_CommandBufferPoolHandle RHI_Device_CreateCommandBufferPool(RHI_DeviceHandle device);
extern "C" ENGINE_DLL [[deprecated("Use RHIDevice::GetFactory()->CreateCommandBufferPool instead")]] RHI_CommandBufferPoolHandle RHI_Device_CreateCommandBufferPool_Type(RHI_DeviceHandle device, unsigned int queueType);
extern "C" ENGINE_DLL [[deprecated("Use RHIDevice::GetFactory()->ReleaseCommandBufferPool instead")]] void RHI_Device_ReleaseCommandBufferPool(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool);

/** @ownership Borrowed - Buffer managed by pool; do NOT release manually */
extern "C" ENGINE_DLL [[deprecated("Use RHIDevice::GetCommandBufferPool()->GetCommandBuffer instead")]] RHI_CommandBufferHandle RHI_Device_GetCommandBuffer(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool, unsigned int currentFrameIndex);
extern "C" ENGINE_DLL [[deprecated("Use RHIDevice::GetCommandBufferPool()->GetCommandBuffer instead")]] RHI_CommandBufferHandle RHI_Device_GetSecondaryCommandBuffer(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool, unsigned int currentFrameIndex);
extern "C" ENGINE_DLL [[deprecated("Use RHIDevice::GetCommandBufferPool()->ReleaseCommandBuffer instead")]] void RHI_Device_ReleaseCommandBuffer(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool, unsigned int currentFrameIndex, RHI_CommandBufferHandle cmd);

extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::Begin instead")]] void RHI_Cmd_Begin(RHI_CommandBufferHandle cmd, unsigned int frameIndex, unsigned int usageFlags = 0);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::End instead")]] void RHI_Cmd_End(RHI_CommandBufferHandle cmd);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::ExecuteCommands instead")]] void RHI_Cmd_ExecuteCommands(RHI_CommandBufferHandle cmd, ArisenEngine::Containers::Vector<RHI_CommandBufferHandle>* secondaryBuffers);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::BeginRenderPass instead")]] void RHI_Cmd_BeginRenderPass(RHI_CommandBufferHandle cmd, ArisenEngine::RHI::RenderPassBeginDesc* desc);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::EndRenderPass instead")]] void RHI_Cmd_EndRenderPass(RHI_CommandBufferHandle cmd);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::SetViewport instead")]] void RHI_Cmd_SetViewport(RHI_CommandBufferHandle cmd, float x, float y, float width, float height, float minDepth, float maxDepth);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::SetScissor instead")]] void RHI_Cmd_SetScissor(RHI_CommandBufferHandle cmd, unsigned int offsetX, unsigned int offsetY, unsigned int width, unsigned int height);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::PushConstants instead")]] void RHI_Cmd_PushConstants(RHI_CommandBufferHandle cmd, unsigned int offset, unsigned int size, const void* data, unsigned int stageFlags);

extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::Draw instead")]] void RHI_Cmd_Draw(RHI_CommandBufferHandle cmd, unsigned int vertexCount, unsigned int instanceCount, unsigned int firstVertex, unsigned int firstInstance, unsigned int firstBinding);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::DrawIndexed instead")]] void RHI_Cmd_DrawIndexed(RHI_CommandBufferHandle cmd, unsigned int indexCount, unsigned int instanceCount, unsigned int firstIndex, unsigned int vertexOffset, unsigned int firstInstance, unsigned int firstBinding);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::DrawMeshTasks instead")]] void RHI_Cmd_DrawMeshTasks(RHI_CommandBufferHandle cmd, unsigned int groupCountX, unsigned int groupCountY, unsigned int groupCountZ);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::Dispatch instead")]] void RHI_Cmd_Dispatch(RHI_CommandBufferHandle cmd, unsigned int groupCountX, unsigned int groupCountY, unsigned int groupCountZ);

extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::DrawIndirect instead")]] void RHI_Cmd_DrawIndirect(RHI_CommandBufferHandle cmd, RHI_BufferHandle buffer, unsigned long long offset, unsigned int drawCount, unsigned int stride);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::DrawIndexedIndirect instead")]] void RHI_Cmd_DrawIndexedIndirect(RHI_CommandBufferHandle cmd, RHI_BufferHandle buffer, unsigned long long offset, unsigned int drawCount, unsigned int stride);

// Added exports for VulkanTest refactor
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::BindPipeline instead")]] void RHI_Cmd_BindPipeline(RHI_CommandBufferHandle cmd, RHI_PipelineHandle pipeline);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::BindVertexBuffers instead")]] void RHI_Cmd_BindVertexBuffers(RHI_CommandBufferHandle cmd, RHI_BufferHandle buffer, unsigned long long offset);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::BindIndexBuffer instead")]] void RHI_Cmd_BindIndexBuffer(RHI_CommandBufferHandle cmd, RHI_BufferHandle buffer, unsigned long long offset, ArisenEngine::RHI::EIndexType type);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::CopyBuffer instead")]] void RHI_Cmd_CopyBuffer(RHI_CommandBufferHandle cmd, RHI_BufferHandle src, unsigned long long srcOffset, RHI_BufferHandle dst, unsigned long long dstOffset, unsigned long long size);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::CopyBufferToImage instead")]] void RHI_Cmd_CopyBufferToImage(RHI_CommandBufferHandle cmd, RHI_BufferHandle src, RHI_ImageHandle dst, ArisenEngine::RHI::EImageLayout dstLayout, ArisenEngine::Containers::Vector<ArisenEngine::RHI::RHIBufferImageCopy>* regions);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::CopyImage instead")]] void RHI_Cmd_CopyImage(RHI_CommandBufferHandle cmd, RHI_ImageHandle src, ArisenEngine::RHI::EImageLayout srcLayout, RHI_ImageHandle dst, ArisenEngine::RHI::EImageLayout dstLayout, unsigned int regionCount, const ArisenEngine::RHI::RHIImageCopy* pRegions);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::PipelineBarrier instead")]] void RHI_Cmd_PipelineBarrier_Image(RHI_CommandBufferHandle cmd, unsigned int srcStage, unsigned int dstStage, unsigned int dependency, ArisenEngine::Containers::Vector<ArisenEngine::RHI::RHIImageMemoryBarrier>* imageBarriers);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::PipelineBarrier instead")]] void RHI_Cmd_PipelineBarrier_Buffer(RHI_CommandBufferHandle cmd, unsigned int srcStage, unsigned int dstStage, unsigned int dependency, ArisenEngine::Containers::Vector<ArisenEngine::RHI::RHIBufferMemoryBarrier>* bufferBarriers);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::PipelineBarrier instead")]] void RHI_Cmd_PipelineBarrier_Memory(RHI_CommandBufferHandle cmd, unsigned int srcStage, unsigned int dstStage, unsigned int dependency, unsigned int memoryBarrierCount, const ArisenEngine::RHI::RHIMemoryBarrier* pMemoryBarriers);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::PipelineBarrier instead")]] void RHI_Cmd_BatchPipelineBarrier(RHI_CommandBufferHandle cmd, unsigned int srcStage, unsigned int dstStage, unsigned int dependency, ArisenEngine::Containers::Vector<ArisenEngine::RHI::RHIMemoryBarrier>* memoryBarriers, ArisenEngine::Containers::Vector<ArisenEngine::RHI::RHIImageMemoryBarrier>* imageBarriers, ArisenEngine::Containers::Vector<ArisenEngine::RHI::RHIBufferMemoryBarrier>* bufferBarriers);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::GenerateMipmaps instead")]] void RHI_Cmd_GenerateMipmaps(RHI_CommandBufferHandle cmd, RHI_ImageHandle image);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::TransitionImageLayout instead")]] void RHI_Cmd_TransitionImageLayout(RHI_CommandBufferHandle cmd, RHI_ImageHandle image, ArisenEngine::RHI::EImageLayout targetLayout);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::TransitionImageLayout instead")]] void RHI_Cmd_TransitionImageLayout_Full(RHI_CommandBufferHandle cmd, RHI_ImageHandle image, ArisenEngine::RHI::EImageLayout oldLayout, ArisenEngine::RHI::EImageLayout targetLayout);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::BeginRendering instead")]] void RHI_Cmd_BeginRendering(RHI_CommandBufferHandle cmd, ArisenEngine::RHI::RHIRenderingInfo* info);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::EndRendering instead")]] void RHI_Cmd_EndRendering(RHI_CommandBufferHandle cmd);

extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::BuildAccelerationStructures instead")]] void RHI_Cmd_BuildAccelerationStructures(RHI_CommandBufferHandle cmd, unsigned int infoCount, const ArisenEngine::RHI::RHIAccelerationStructureBuildGeometryInfo* pInfos, const ArisenEngine::RHI::RHIAccelerationStructureBuildRangeInfo* const* ppBuildRangeInfos);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::TraceRays instead")]] void RHI_Cmd_TraceRays(RHI_CommandBufferHandle cmd, const ArisenEngine::RHI::RHITraceRaysDescriptor* desc);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::SetFragmentShadingRate instead")]] void RHI_Cmd_SetFragmentShadingRate(RHI_CommandBufferHandle cmd, ArisenEngine::RHI::EShadingRate rate, ArisenEngine::RHI::EShadingRateCombiner combinerOp[2]);

extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::BeginDebugLabel instead")]] void RHI_Cmd_BeginDebugLabel(RHI_CommandBufferHandle cmd, const char* label, const float color[4]);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::EndDebugLabel instead")]] void RHI_Cmd_EndDebugLabel(RHI_CommandBufferHandle cmd);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::InsertDebugMarker instead")]] void RHI_Cmd_InsertDebugMarker(RHI_CommandBufferHandle cmd, const char* label, const float color[4]);

// Bind descriptor sets using all sets from a pool/poolId
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::BindDescriptorSets instead")]] void RHI_Cmd_BindDescriptorSets_FromPool(RHI_CommandBufferHandle cmd, ArisenEngine::RHI::EPipelineBindPoint bindPoint, unsigned int firstSet, RHI_DescriptorPoolHandle pool, unsigned int poolId);
extern "C" ENGINE_DLL [[deprecated("Use RHICommandBuffer::BindDescriptorSet instead")]] void RHI_Cmd_BindDescriptorSet_FromPool(RHI_CommandBufferHandle cmd, ArisenEngine::RHI::EPipelineBindPoint bindPoint, unsigned int firstSet, RHI_DescriptorPoolHandle pool, unsigned int poolId, unsigned int setIndex);



