#include "CommandBufferExports.h"
#include "../../Core/Core.RHI/RHI/Core/RHIFactory.h"
#include "../../Core/Core.RHI/RHI/Commands/RHICommandBufferPool.h"
#include "../../Core/Core.RHI/RHI/Core/RHIDevice.h"
#include "../../Core/RHI.Vulkan/Core/RHIVkDevice.h"
#include "../../Core/Core.RHI/RHI/Handles/RHIHandle.h"

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
    if (dev == nullptr) return 0;
    
    auto h = *reinterpret_cast<RHI::RHICommandBufferPoolHandle*>(&pool);
    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (!vkDev) return 0;

    auto* item = RHI::RHINativeBridge::GetCommandBufferPoolItem(vkDev, h);
    if (!item || !item->pool) return 0;
    
    auto* raw = item->pool->GetCommandBuffer(currentFrameIndex);
    return reinterpret_cast<RHI_CommandBufferHandle>(raw);
}

extern "C" ENGINE_DLL RHI_CommandBufferHandle RHI_Device_GetSecondaryCommandBuffer(RHI_DeviceHandle device, RHI_CommandBufferPoolHandle pool, unsigned int currentFrameIndex)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return 0;
    
    auto h = *reinterpret_cast<RHI::RHICommandBufferPoolHandle*>(&pool);
    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (!vkDev) return 0;

    auto* item = RHI::RHINativeBridge::GetCommandBufferPoolItem(vkDev, h);
    if (!item || !item->pool) return 0;
    
    auto* raw = item->pool->GetCommandBuffer(currentFrameIndex, RHI::COMMAND_BUFFER_LEVEL_SECONDARY);
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
    auto* commandBuffer = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (commandBuffer)
    {
        if (commandBuffer->GetLevel() == RHI::ECommandBufferLevel::COMMAND_BUFFER_LEVEL_SECONDARY)
        {
            RHI::RHICommandBufferInheritanceInfo inheritanceInfo = {};
            commandBuffer->Begin(frameIndex, usageFlags, &inheritanceInfo);
        }
        else
        {
            commandBuffer->Begin(frameIndex, usageFlags, nullptr);
        }
    }
}

extern "C" ENGINE_DLL void RHI_Cmd_End(RHI_CommandBufferHandle cmd)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr) return;
    c->End();
}

extern "C" ENGINE_DLL void RHI_Cmd_ExecuteCommands(RHI_CommandBufferHandle cmd, ArisenEngine::Containers::Vector<RHI_CommandBufferHandle>* secondaryBuffers)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || secondaryBuffers == nullptr) return;

    ArisenEngine::Containers::Vector<RHI::RHICommandBuffer*> buffers;
    for (auto h : *secondaryBuffers)
    {
        if (h) buffers.push_back(reinterpret_cast<RHI::RHICommandBuffer*>(h));
    }
    c->ExecuteCommands(std::move(buffers));
}

extern "C" ENGINE_DLL void RHI_Cmd_BeginRenderPass(RHI_CommandBufferHandle cmd, RHI::RenderPassBeginDesc* desc)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || desc == nullptr) return;
    // Both RHI::RenderPassBeginDesc and the pointers in it are now POD handles if we updated the struct in RHICommandBuffer.h
    RHI::RenderPassBeginDesc copy = *desc;
    c->BeginRenderPass(std::move(copy));
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

extern "C" ENGINE_DLL void RHI_Cmd_PushConstants(RHI_CommandBufferHandle cmd, unsigned int offset, unsigned int size, const void* data, unsigned int stageFlags)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr) return;
    c->PushConstants(offset, size, data, stageFlags);
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

extern "C" ENGINE_DLL void RHI_Cmd_DrawMeshTasks(RHI_CommandBufferHandle cmd, unsigned int groupCountX, unsigned int groupCountY, unsigned int groupCountZ)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr) return;
    c->DrawMeshTasks(groupCountX, groupCountY, groupCountZ);
}

extern "C" ENGINE_DLL void RHI_Cmd_Dispatch(RHI_CommandBufferHandle cmd, unsigned int groupCountX, unsigned int groupCountY, unsigned int groupCountZ)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr) return;
    c->Dispatch(groupCountX, groupCountY, groupCountZ);
}

extern "C" ENGINE_DLL void RHI_Cmd_DrawIndirect(RHI_CommandBufferHandle cmd, RHI_BufferHandle buffer, unsigned long long offset, unsigned int drawCount, unsigned int stride)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || buffer == 0) return;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    c->DrawIndirect(h, offset, drawCount, stride);
}

extern "C" ENGINE_DLL void RHI_Cmd_DrawIndexedIndirect(RHI_CommandBufferHandle cmd, RHI_BufferHandle buffer, unsigned long long offset, unsigned int drawCount, unsigned int stride)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || buffer == 0) return;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    c->DrawIndexedIndirect(h, offset, drawCount, stride);
}

extern "C" ENGINE_DLL void RHI_Cmd_BindPipeline(RHI_CommandBufferHandle cmd, RHI_PipelineHandle pipeline)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || pipeline == 0) return;
    auto h = *reinterpret_cast<RHI::RHIPipelineHandle*>(&pipeline);
    c->BindPipeline(h);
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

extern "C" ENGINE_DLL void RHI_Cmd_CopyBufferToImage(RHI_CommandBufferHandle cmd, RHI_BufferHandle src, RHI_ImageHandle dst, RHI::EImageLayout dstLayout, Containers::Vector<RHI::RHIBufferImageCopy>* regions)
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

extern "C" ENGINE_DLL void RHI_Cmd_PipelineBarrier_Buffer(RHI_CommandBufferHandle cmd, unsigned int srcStage, unsigned int dstStage, unsigned int dependency, Containers::Vector<RHI::RHIBufferMemoryBarrier>* bufferBarriers)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || bufferBarriers == nullptr) return;
    Containers::Vector<RHI::RHIMemoryBarrier> none1; none1.resize(0);
    Containers::Vector<RHI::RHIImageMemoryBarrier> none2; none2.resize(0);
    c->PipelineBarrier(static_cast<RHI::EPipelineStageFlag>(srcStage), static_cast<RHI::EPipelineStageFlag>(dstStage), dependency, std::move(none1), std::move(none2), std::move(*bufferBarriers));
}

extern "C" ENGINE_DLL void RHI_Cmd_BatchPipelineBarrier(RHI_CommandBufferHandle cmd, unsigned int srcStage, unsigned int dstStage, unsigned int dependency, Containers::Vector<RHI::RHIMemoryBarrier>* memoryBarriers, Containers::Vector<RHI::RHIImageMemoryBarrier>* imageBarriers, Containers::Vector<RHI::RHIBufferMemoryBarrier>* bufferBarriers)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr) return;

    Containers::Vector<RHI::RHIMemoryBarrier> m;
    Containers::Vector<RHI::RHIImageMemoryBarrier> i;
    Containers::Vector<RHI::RHIBufferMemoryBarrier> b;

    if (memoryBarriers) m = std::move(*memoryBarriers);
    if (imageBarriers) i = std::move(*imageBarriers);
    if (bufferBarriers) b = std::move(*bufferBarriers);

    c->PipelineBarrier(static_cast<RHI::EPipelineStageFlag>(srcStage), static_cast<RHI::EPipelineStageFlag>(dstStage), dependency, std::move(m), std::move(i), std::move(b));
}

// Deprecated: WaitSemaphore/SignalSemaphore moved to RHISubmitDescriptor
// extern "C" ENGINE_DLL void RHI_Cmd_WaitSemaphore(RHI_CommandBufferHandle cmd, RHI_SemaphoreHandle semaphore, unsigned int stageFlags)
// {}
// extern "C" ENGINE_DLL void RHI_Cmd_SignalSemaphore(RHI_CommandBufferHandle cmd, RHI_SemaphoreHandle semaphore)
// {}

extern "C" ENGINE_DLL void RHI_Cmd_GenerateMipmaps(RHI_CommandBufferHandle cmd, RHI_ImageHandle image)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (c == nullptr || image == 0) return;
    auto h = *reinterpret_cast<RHI::RHIImageHandle*>(&image);
    c->GenerateMipmaps(h);
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

extern "C" ENGINE_DLL void RHI_Cmd_BindDescriptorSets_FromPool(RHI_CommandBufferHandle cmd, RHI::EPipelineBindPoint bindPoint, unsigned int firstSet, RHI_DescriptorPoolHandle pool, unsigned int poolId)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    auto* p = reinterpret_cast<RHI::RHIDescriptorPool*>(pool);
    if (c == nullptr || p == nullptr) return;
    auto& sets = p->GetDescriptorSets(poolId);
    c->BindDescriptorSets(bindPoint, firstSet, const_cast<Containers::Vector<std::shared_ptr<RHI::RHIDescriptorSet>>&>(sets), 0, nullptr);
    c->TrackDescriptorPoolUse(p, poolId);
}

extern "C" ENGINE_DLL void RHI_Cmd_BindDescriptorSet_FromPool(RHI_CommandBufferHandle cmd, RHI::EPipelineBindPoint bindPoint, unsigned int firstSet, RHI_DescriptorPoolHandle pool, unsigned int poolId, unsigned int setIndex)
{
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    auto* p = reinterpret_cast<RHI::RHIDescriptorPool*>(pool);
    if (c == nullptr || p == nullptr) return;
    auto& sets = p->GetDescriptorSets(poolId);
    if (setIndex >= sets.size()) return;
    
    Containers::Vector<std::shared_ptr<RHI::RHIDescriptorSet>> singleSet;
    singleSet.push_back(sets[setIndex]);
    c->BindDescriptorSets(bindPoint, firstSet, singleSet, 0, nullptr);
    c->TrackDescriptorPoolUse(p, poolId);
}


