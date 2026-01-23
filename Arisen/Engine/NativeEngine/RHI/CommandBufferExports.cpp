#include "CommandBufferExports.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIFactory.h"
#include "../../Core/Core.Infra/RHI/CommandBuffer/RHICommandBufferPool.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIDevice.h"
#include "../../Core/RHI.Vulkan/Devices/RHIVkDevice.h"
#include "../../Core/Core.Infra/RHI/Handles/RHIHandle.h"

using namespace ArisenEngine;

#include "RHINativeBridge.h"

extern "C" ENGINE_DLL RHI_CommandBufferPoolHandle RHI_Device_CreateCommandBufferPool(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return 0;
    auto handle = dev->GetFactory()->CreateCommandBufferPool();
    return *reinterpret_cast<unsigned long long*>(&handle);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseCommandBufferPool(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return;
    auto h = *reinterpret_cast<RHI::RHICommandBufferPoolHandle*>(&pool);
    dev->GetFactory()->ReleaseCommandBufferPool(h);
}

extern "C" ENGINE_DLL RHI_CommandBufferHandle RHI_Device_GetCommandBuffer(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool, unsigned int currentFrameIndex)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return nullptr;
    
    auto h = *reinterpret_cast<RHI::RHICommandBufferPoolHandle*>(&pool);
    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (!vkDev) return nullptr;

    auto* item = RHI::RHINativeBridge::GetCommandBufferPoolItem(vkDev, h);
    if (!item || !item->pool) return nullptr;
    
    auto* raw = item->pool->GetCommandBuffer(currentFrameIndex);
    return reinterpret_cast<RHI_CommandBufferHandle>(raw);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseCommandBuffer(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool, unsigned int currentFrameIndex, RHI_CommandBufferHandle cmd)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return;
    
    auto h = *reinterpret_cast<RHI::RHICommandBufferPoolHandle*>(&pool);
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    
    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (!vkDev || !c) return;

    auto* item = RHI::RHINativeBridge::GetCommandBufferPoolItem(vkDev, h);
    if (item && item->pool)
    {
        item->pool->ReleaseCommandBuffer(currentFrameIndex, c);
    }
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
    // Both RHI::RenderPassBeginDesc and the pointers in it are now POD handles if we updated the struct in RHICommandBuffer.h
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
    if (c == nullptr || pipeline == 0) return;
    auto h = *reinterpret_cast<RHI::RHIPipelineHandle*>(&pipeline);
    c->BindPipeline(frameIndex, h);
}

extern "C" ENGINE_DLL void RHI_Cmd_BindVertexBuffers(RHI_CommandBufferHandle cmd, RHI_BufferHandle buffer, unsigned long long offset)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || buffer == 0) return;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    c->BindVertexBuffers(h, static_cast<UInt64>(offset));
}

extern "C" ENGINE_DLL void RHI_Cmd_BindIndexBuffer(RHI_CommandBufferHandle cmd, RHI_BufferHandle buffer, unsigned long long offset, RHI::EIndexType type)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || buffer == 0) return;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    c->BindIndexBuffer(h, static_cast<UInt64>(offset), type);
}

extern "C" ENGINE_DLL void RHI_Cmd_CopyBuffer(RHI_CommandBufferHandle cmd, RHI_BufferHandle src, unsigned long long srcOffset, RHI_BufferHandle dst, unsigned long long dstOffset, unsigned long long size)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || src == 0 || dst == 0) return;
    auto sh = *reinterpret_cast<RHI::RHIBufferHandle*>(&src);
    auto dh = *reinterpret_cast<RHI::RHIBufferHandle*>(&dst);
    c->CopyBuffer(sh, static_cast<UInt64>(srcOffset), dh, static_cast<UInt64>(dstOffset), static_cast<UInt64>(size));
}

extern "C" ENGINE_DLL void RHI_Cmd_CopyBufferToImage(RHI_CommandBufferHandle cmd, RHI_BufferHandle src, RHI_ImageHandle dst, RHI::EImageLayout dstLayout, Containers::Vector<RHI::BufferImageCopy>* regions)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || src == 0 || dst == 0 || regions == nullptr) return;
    auto sh = *reinterpret_cast<RHI::RHIBufferHandle*>(&src);
    auto dh = *reinterpret_cast<RHI::RHIImageHandle*>(&dst);
    c->CopyBufferToImage(sh, dh, dstLayout, std::move(*regions));
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
    if (c == nullptr || semaphore == 0) return;
    auto h = *reinterpret_cast<RHI::RHISemaphoreHandle*>(&semaphore);
    c->WaitSemaphore(h, static_cast<RHI::EPipelineStageFlag>(stageFlags));
}

extern "C" ENGINE_DLL void RHI_Cmd_SignalSemaphore(RHI_CommandBufferHandle cmd, RHI_SemaphoreHandle semaphore)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || semaphore == 0) return;
    auto h = *reinterpret_cast<RHI::RHISemaphoreHandle*>(&semaphore);
    c->SignalSemaphore(h);
}

extern "C" ENGINE_DLL void RHI_Cmd_InjectFence(RHI_CommandBufferHandle cmd, RHI_FenceHandle fence)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr) return;
    auto h = *reinterpret_cast<RHI::RHIFenceHandle*>(&fence);
    c->InjectFence(h);
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

