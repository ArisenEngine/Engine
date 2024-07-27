#include "RHIVkCommandBuffer.h"

NebulaEngine::RHI::RHIVkCommandBuffer::~RHIVkCommandBuffer() noexcept
{
    
}

NebulaEngine::RHI::RHIVkCommandBuffer::RHIVkCommandBuffer(RHIVkDevice* device, RHIVkCommandBufferPool* pool): RHICommandBuffer(device, pool)
{
    
}

void NebulaEngine::RHI::RHIVkCommandBuffer::BeginRenderPass(RenderPassBeginDesc&& desc)
{
}

void NebulaEngine::RHI::RHIVkCommandBuffer::EndRenderPass()
{
}
