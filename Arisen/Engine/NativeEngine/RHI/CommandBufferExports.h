#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/CommandBuffer/RHICommandBuffer.h"

typedef void* RHI_CommandBufferPoolHandle;
typedef void* RHI_CommandBufferHandle;
typedef void* RHI_DeviceHandle;
typedef void* RHI_PipelineHandle;
typedef void* RHI_BufferHandle;
typedef void* RHI_ImageHandle;
typedef void* RHI_DescriptorPoolHandle;
typedef void* RHI_SemaphoreHandle;
typedef void* RHI_FenceHandle;

extern "C" ENGINE_DLL RHI_CommandBufferPoolHandle RHI_Device_CreateCommandBufferPool(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_ReleaseCommandBufferPool(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool);
extern "C" ENGINE_DLL RHI_CommandBufferHandle RHI_Device_GetCommandBuffer(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool, unsigned int currentFrameIndex);
extern "C" ENGINE_DLL void RHI_Device_ReleaseCommandBuffer(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool, unsigned int currentFrameIndex, RHI_CommandBufferHandle cmd);

extern "C" ENGINE_DLL void RHI_Cmd_Begin(RHI_CommandBufferHandle cmd, unsigned int frameIndex, unsigned int usageFlags);
extern "C" ENGINE_DLL void RHI_Cmd_End(RHI_CommandBufferHandle cmd);
extern "C" ENGINE_DLL void RHI_Cmd_BeginRenderPass(RHI_CommandBufferHandle cmd, unsigned int frameIndex, ArisenEngine::RHI::RenderPassBeginDesc* desc);
extern "C" ENGINE_DLL void RHI_Cmd_EndRenderPass(RHI_CommandBufferHandle cmd);
extern "C" ENGINE_DLL void RHI_Cmd_SetViewport(RHI_CommandBufferHandle cmd, float x, float y, float width, float height, float minDepth, float maxDepth);
extern "C" ENGINE_DLL void RHI_Cmd_SetScissor(RHI_CommandBufferHandle cmd, unsigned int offsetX, unsigned int offsetY, unsigned int width, unsigned int height);
extern "C" ENGINE_DLL void RHI_Cmd_Draw(RHI_CommandBufferHandle cmd, unsigned int vertexCount, unsigned int instanceCount, unsigned int firstVertex, unsigned int firstInstance, unsigned int firstBinding);
extern "C" ENGINE_DLL void RHI_Cmd_DrawIndexed(RHI_CommandBufferHandle cmd, unsigned int indexCount, unsigned int instanceCount, unsigned int firstIndex, unsigned int vertexOffset, unsigned int firstInstance, unsigned int firstBinding);

// Added exports for VulkanTest refactor
extern "C" ENGINE_DLL void RHI_Cmd_BindPipeline(RHI_CommandBufferHandle cmd, unsigned int frameIndex, RHI_PipelineHandle pipeline);
extern "C" ENGINE_DLL void RHI_Cmd_BindVertexBuffers(RHI_CommandBufferHandle cmd, RHI_BufferHandle buffer, unsigned long long offset);
extern "C" ENGINE_DLL void RHI_Cmd_BindIndexBuffer(RHI_CommandBufferHandle cmd, RHI_BufferHandle buffer, unsigned long long offset, ArisenEngine::RHI::EIndexType type);
extern "C" ENGINE_DLL void RHI_Cmd_CopyBuffer(RHI_CommandBufferHandle cmd, RHI_BufferHandle src, unsigned long long srcOffset, RHI_BufferHandle dst, unsigned long long dstOffset, unsigned long long size);
extern "C" ENGINE_DLL void RHI_Cmd_CopyBufferToImage(RHI_CommandBufferHandle cmd, RHI_BufferHandle src, RHI_ImageHandle dst, ArisenEngine::RHI::EImageLayout dstLayout, ArisenEngine::Containers::Vector<ArisenEngine::RHI::BufferImageCopy>* regions);
extern "C" ENGINE_DLL void RHI_Cmd_PipelineBarrier_Image(RHI_CommandBufferHandle cmd, unsigned int srcStage, unsigned int dstStage, unsigned int dependency, ArisenEngine::Containers::Vector<ArisenEngine::RHI::RHIImageMemoryBarrier>* imageBarriers);
extern "C" ENGINE_DLL void RHI_Cmd_WaitSemaphore(RHI_CommandBufferHandle cmd, RHI_SemaphoreHandle semaphore, unsigned int stageFlags);
extern "C" ENGINE_DLL void RHI_Cmd_SignalSemaphore(RHI_CommandBufferHandle cmd, RHI_SemaphoreHandle semaphore);
extern "C" ENGINE_DLL void RHI_Cmd_InjectFence(RHI_CommandBufferHandle cmd, RHI_FenceHandle fence);
extern "C" ENGINE_DLL void RHI_Cmd_WaitForFence(RHI_CommandBufferHandle cmd, unsigned int frameIndex);

extern "C" ENGINE_DLL void RHI_Cmd_BeginRendering(RHI_CommandBufferHandle cmd, ArisenEngine::RHI::RHIRenderingInfo* info);
extern "C" ENGINE_DLL void RHI_Cmd_EndRendering(RHI_CommandBufferHandle cmd);

// Bind descriptor sets using all sets from a pool/poolId
extern "C" ENGINE_DLL void RHI_Cmd_BindDescriptorSets_FromPool(RHI_CommandBufferHandle cmd, unsigned int frameIndex, ArisenEngine::RHI::EPipelineBindPoint bindPoint, unsigned int firstSet, RHI_DescriptorPoolHandle pool, unsigned int poolId);


