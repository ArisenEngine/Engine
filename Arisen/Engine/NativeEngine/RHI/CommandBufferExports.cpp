#include "CommandBufferExports.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIFactory.h"
#include "../../Core/Core.Infra/RHI/CommandBuffer/RHICommandBufferPool.h"

#include "../../Core/Core.Infra/RHI/Devices/RHIFactory.h"
#include "../../Core/Core.Infra/RHI/CommandBuffer/RHICommandBufferPool.h"

// Force rebuild for ABI compatibility check
using namespace ArisenEngine;

extern "C" ENGINE_DLL RHI_CommandBufferPoolHandle RHI_Device_CreateCommandBufferPool(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return nullptr;
    return reinterpret_cast<RHI_CommandBufferPoolHandle>(dev->GetFactory()->CreateCommandBufferPool());
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseCommandBufferPool(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    auto* p = reinterpret_cast<RHI::RHICommandBufferPool*>(pool);
    if (dev == nullptr) return;
    return dev->GetFactory()->ReleaseCommandBufferPool(p);
}

extern "C" ENGINE_DLL RHI_CommandBufferHandle RHI_Device_GetCommandBuffer(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool, unsigned int currentFrameIndex)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return nullptr;
    auto* p = reinterpret_cast<RHI::RHICommandBufferPool*>(pool);
     if (p == nullptr) return nullptr;
    auto* raw = p->GetCommandBuffer(currentFrameIndex);
    return reinterpret_cast<RHI_CommandBufferHandle>(raw);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseCommandBuffer(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool, unsigned int currentFrameIndex, RHI_CommandBufferHandle cmd)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return;
    auto* p = reinterpret_cast<RHI::RHICommandBufferPool*>(pool);
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (p == nullptr || c == nullptr) return;
    p->ReleaseCommandBuffer(currentFrameIndex, c);
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

extern "C" ENGINE_DLL void RHI_Cmd_BindPipeline(RHI_CommandBufferHandle cmd, unsigned int frameIndex, RHI_PipelineHandle pipeline)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    auto* p = reinterpret_cast<RHI::GPUPipeline*>(pipeline);
    if (c == nullptr || p == nullptr) return;
    c->BindPipeline(frameIndex, p);
}

extern "C" ENGINE_DLL void RHI_Cmd_BindVertexBuffers(RHI_CommandBufferHandle cmd, RHI_BufferHandle buffer, unsigned long long offset)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    auto* b = reinterpret_cast<RHI::BufferHandle*>(buffer);
    if (c == nullptr || b == nullptr) return;
    c->BindVertexBuffers(b, static_cast<UInt64>(offset));
}

extern "C" ENGINE_DLL void RHI_Cmd_BindIndexBuffer(RHI_CommandBufferHandle cmd, RHI_BufferHandle buffer, unsigned long long offset, RHI::EIndexType type)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    auto* b = reinterpret_cast<RHI::BufferHandle*>(buffer);
    if (c == nullptr || b == nullptr) return;
    c->BindIndexBuffer(b, static_cast<UInt64>(offset), type);
}

extern "C" ENGINE_DLL void RHI_Cmd_CopyBuffer(RHI_CommandBufferHandle cmd, RHI_BufferHandle src, unsigned long long srcOffset, RHI_BufferHandle dst, unsigned long long dstOffset, unsigned long long size)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    auto* sb = reinterpret_cast<const RHI::BufferHandle*>(src);
    auto* db = reinterpret_cast<const RHI::BufferHandle*>(dst);
    if (c == nullptr || sb == nullptr || db == nullptr) return;
    c->CopyBuffer(sb, static_cast<UInt64>(srcOffset), db, static_cast<UInt64>(dstOffset), static_cast<UInt64>(size));
}

extern "C" ENGINE_DLL void RHI_Cmd_CopyBufferToImage(RHI_CommandBufferHandle cmd, RHI_BufferHandle src, RHI_ImageHandle dst, RHI::EImageLayout dstLayout, Containers::Vector<RHI::BufferImageCopy>* regions)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    auto* sb = reinterpret_cast<const RHI::BufferHandle*>(src);
    auto* di = reinterpret_cast<const RHI::ImageHandle*>(dst);
    if (c == nullptr || sb == nullptr || di == nullptr || regions == nullptr) return;
    c->CopyBufferToImage(sb, di, dstLayout, std::move(*regions));
}

extern "C" ENGINE_DLL void RHI_Cmd_PipelineBarrier_Image(RHI_CommandBufferHandle cmd, unsigned int srcStage, unsigned int dstStage, unsigned int dependency, Containers::Vector<RHI::RHIImageMemoryBarrier>* imageBarriers)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || imageBarriers == nullptr) return;
    Containers::Vector<RHI::RHIMemoryBarrier> none1; none1.resize(0);
    Containers::Vector<RHI::RHIBufferMemoryBarrier> none2; none2.resize(0);
    c->PipelineBarrier(static_cast<RHI::EPipelineStageFlag>(srcStage), static_cast<RHI::EPipelineStageFlag>(dstStage), dependency, std::move(none1), std::move(*imageBarriers), std::move(none2));
}

extern "C" ENGINE_DLL void RHI_Cmd_WaitSemaphore(RHI_CommandBufferHandle cmd, RHI_SemaphoreHandle semaphore, unsigned int stageFlags)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    auto* s = reinterpret_cast<RHI::RHISemaphore*>(semaphore);
    if (c == nullptr || s == nullptr) return;
    c->WaitSemaphore(s, static_cast<RHI::EPipelineStageFlag>(stageFlags));
}

extern "C" ENGINE_DLL void RHI_Cmd_SignalSemaphore(RHI_CommandBufferHandle cmd, RHI_SemaphoreHandle semaphore)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    auto* s = reinterpret_cast<RHI::RHISemaphore*>(semaphore);
    if (c == nullptr || s == nullptr) return;
    c->SignalSemaphore(s);
}

extern "C" ENGINE_DLL void RHI_Cmd_InjectFence(RHI_CommandBufferHandle cmd, RHI_FenceHandle fence)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    auto* f = reinterpret_cast<RHI::RHIFence*>(fence);
    if (c == nullptr || f == nullptr) return;
    c->InjectFence(f);
}

extern "C" ENGINE_DLL void RHI_Cmd_WaitForFence(RHI_CommandBufferHandle cmd, unsigned int frameIndex)
{
    auto* commandBuffer = reinterpret_cast<ArisenEngine::RHI::RHICommandBuffer*>(cmd);
    if (commandBuffer)
    {
        commandBuffer->WaitForFence(frameIndex);
    }
}

extern "C" ENGINE_DLL void RHI_Cmd_BeginRendering(RHI_CommandBufferHandle cmd, ArisenEngine::RHI::RHIRenderingInfo* info)
{
    auto* commandBuffer = reinterpret_cast<ArisenEngine::RHI::RHICommandBuffer*>(cmd);
    if (commandBuffer && info)
    {
        commandBuffer->BeginRendering(*info);
    }
}

extern "C" ENGINE_DLL void RHI_Cmd_EndRendering(RHI_CommandBufferHandle cmd)
{
    auto* commandBuffer = reinterpret_cast<ArisenEngine::RHI::RHICommandBuffer*>(cmd);
    if (commandBuffer)
    {
        commandBuffer->EndRendering();
    }
}

extern "C" ENGINE_DLL void RHI_Cmd_BindDescriptorSets_FromPool(RHI_CommandBufferHandle cmd, unsigned int frameIndex, RHI::EPipelineBindPoint bindPoint, unsigned int firstSet, RHI_DescriptorPoolHandle pool, unsigned int poolId)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    auto* p = reinterpret_cast<RHI::DescriptorPool*>(pool);
    if (c == nullptr || p == nullptr) return;
    auto& sets = p->GetDescriptorSets(poolId);
    c->BindDescriptorSets(frameIndex, bindPoint, firstSet, const_cast<Containers::Vector<std::shared_ptr<RHI::RHIDescriptorSet>>&>(sets), 0, nullptr);
    c->TrackDescriptorPoolUse(p, poolId);
}

