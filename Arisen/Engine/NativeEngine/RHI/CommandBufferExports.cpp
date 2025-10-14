#include "CommandBufferExports.h"
#include "../../Core/Core.Infra/RHI/CommandBuffer/RHICommandBufferPool.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL unsigned int RHI_Device_CreateCommandBufferPool(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::Device*>(device);
    if (dev == nullptr) return 0U;
    return dev->CreateCommandBufferPool();
}

extern "C" ENGINE_DLL RHI_CommandBufferHandle RHI_Device_GetCommandBuffer(RHI_DeviceHandle device, unsigned int poolId, unsigned int currentFrameIndex)
{
    auto* dev = reinterpret_cast<RHI::Device*>(device);
    if (dev == nullptr) return nullptr;
    auto pool = dev->GetCommandBufferPool(poolId);
    auto sp = pool->GetCommandBuffer(currentFrameIndex);
    return reinterpret_cast<RHI_CommandBufferHandle>(sp.get());
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseCommandBuffer(RHI_DeviceHandle device, unsigned int poolId, unsigned int currentFrameIndex, RHI_CommandBufferHandle cmd)
{
    auto* dev = reinterpret_cast<RHI::Device*>(device);
    if (dev == nullptr) return;
    auto pool = dev->GetCommandBufferPool(poolId);
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (pool == nullptr || c == nullptr) return;
    std::shared_ptr<RHI::RHICommandBuffer> sp(c, [](RHI::RHICommandBuffer*){});
    pool->ReleaseCommandBuffer(currentFrameIndex, sp);
}

extern "C" ENGINE_DLL void RHI_Cmd_Begin(RHI_CommandBufferHandle cmd, unsigned int frameIndex, unsigned int usageFlags)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr) return;
    c->Begin(frameIndex, usageFlags);
}

extern "C" ENGINE_DLL void RHI_Cmd_End(RHI_CommandBufferHandle cmd)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr) return;
    c->End();
}

extern "C" ENGINE_DLL void RHI_Cmd_BeginRenderPass(RHI_CommandBufferHandle cmd, unsigned int frameIndex, RHI::RenderPassBeginDesc* desc)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || desc == nullptr) return;
    RHI::RenderPassBeginDesc copy = *desc;
    c->BeginRenderPass(frameIndex, std::move(copy));
}

extern "C" ENGINE_DLL void RHI_Cmd_EndRenderPass(RHI_CommandBufferHandle cmd)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr) return;
    c->EndRenderPass();
}

extern "C" ENGINE_DLL void RHI_Cmd_SetViewport(RHI_CommandBufferHandle cmd, float x, float y, float width, float height, float minDepth, float maxDepth)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr) return;
    c->SetViewport(x, y, width, height, minDepth, maxDepth);
}

extern "C" ENGINE_DLL void RHI_Cmd_SetScissor(RHI_CommandBufferHandle cmd, unsigned int offsetX, unsigned int offsetY, unsigned int width, unsigned int height)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr) return;
    c->SetScissor(offsetX, offsetY, width, height);
}

extern "C" ENGINE_DLL void RHI_Cmd_Draw(RHI_CommandBufferHandle cmd, unsigned int vertexCount, unsigned int instanceCount, unsigned int firstVertex, unsigned int firstInstance, unsigned int firstBinding)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr) return;
    c->Draw(vertexCount, instanceCount, firstVertex, firstInstance, firstBinding);
}

extern "C" ENGINE_DLL void RHI_Cmd_DrawIndexed(RHI_CommandBufferHandle cmd, unsigned int indexCount, unsigned int instanceCount, unsigned int firstIndex, unsigned int vertexOffset, unsigned int firstInstance, unsigned int firstBinding)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr) return;
    c->DrawIndexed(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance, firstBinding);
}


