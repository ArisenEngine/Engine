#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/CommandBuffer/RHICommandBuffer.h"

typedef void* RHI_CommandBufferPoolHandle;
typedef void* RHI_CommandBufferHandle;
typedef void* RHI_DeviceHandle;

extern "C" ENGINE_DLL unsigned int RHI_Device_CreateCommandBufferPool(RHI_DeviceHandle device);
extern "C" ENGINE_DLL RHI_CommandBufferHandle RHI_Device_GetCommandBuffer(RHI_DeviceHandle device, unsigned int poolId, unsigned int currentFrameIndex);
extern "C" ENGINE_DLL void RHI_Device_ReleaseCommandBuffer(RHI_DeviceHandle device, unsigned int poolId, unsigned int currentFrameIndex, RHI_CommandBufferHandle cmd);

extern "C" ENGINE_DLL void RHI_Cmd_Begin(RHI_CommandBufferHandle cmd, unsigned int frameIndex, unsigned int usageFlags);
extern "C" ENGINE_DLL void RHI_Cmd_End(RHI_CommandBufferHandle cmd);
extern "C" ENGINE_DLL void RHI_Cmd_BeginRenderPass(RHI_CommandBufferHandle cmd, unsigned int frameIndex, ArisenEngine::RHI::RenderPassBeginDesc* desc);
extern "C" ENGINE_DLL void RHI_Cmd_EndRenderPass(RHI_CommandBufferHandle cmd);
extern "C" ENGINE_DLL void RHI_Cmd_SetViewport(RHI_CommandBufferHandle cmd, float x, float y, float width, float height, float minDepth, float maxDepth);
extern "C" ENGINE_DLL void RHI_Cmd_SetScissor(RHI_CommandBufferHandle cmd, unsigned int offsetX, unsigned int offsetY, unsigned int width, unsigned int height);
extern "C" ENGINE_DLL void RHI_Cmd_Draw(RHI_CommandBufferHandle cmd, unsigned int vertexCount, unsigned int instanceCount, unsigned int firstVertex, unsigned int firstInstance, unsigned int firstBinding);
extern "C" ENGINE_DLL void RHI_Cmd_DrawIndexed(RHI_CommandBufferHandle cmd, unsigned int indexCount, unsigned int instanceCount, unsigned int firstIndex, unsigned int vertexOffset, unsigned int firstInstance, unsigned int firstBinding);


