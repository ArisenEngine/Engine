#include "RHIVkCommandBuffer.h"

#include "RHI/Synchronization/SynchScope.h"


NebulaEngine::RHI::RHIVkCommandBuffer::~RHIVkCommandBuffer() noexcept
{
    LOG_DEBUG("[RHIVkCommandBuffer::~RHIVkCommandBuffer]: ~RHIVkCommandBuffer");
    
}

NebulaEngine::RHI::RHIVkCommandBuffer::RHIVkCommandBuffer(RHIVkDevice* device, RHIVkCommandBufferPool* pool): RHICommandBuffer(device, pool)
{
    m_VkDevice = static_cast<VkDevice>(device->GetHandle());
    m_VkCommandPool = static_cast<VkCommandPool>(pool->GetHandle());

    // Alloc Memory
    {
        VkCommandBufferAllocateInfo allocInfo{};
        allocInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
        allocInfo.commandPool = m_VkCommandPool;
        // todo 
        allocInfo.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
        allocInfo.commandBufferCount = 1;

        // todo: sperate alloc memory and free memory
        if (vkAllocateCommandBuffers(m_VkDevice, &allocInfo, &m_VkCommandBuffer) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkCommandBuffer::RHIVkCommandBuffer]: failed to allocate command buffers!");
        }
    }
    
    m_State = ECommandState::ReadyForBegin;
}

void NebulaEngine::RHI::RHIVkCommandBuffer::BeginRenderPass(RenderPassBeginDesc&& desc)
{
    ASSERT(m_State == ECommandState::IsInsideBegin);
    
    VkRenderPassBeginInfo renderPassInfo{};
    renderPassInfo.sType = VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO;
    renderPassInfo.renderPass = static_cast<VkRenderPass>(desc.renderPass->GetHandle());
    renderPassInfo.framebuffer = static_cast<VkFramebuffer>(desc.frameBuffer->GetHandle());
    auto renderArea = desc.frameBuffer->GetRenderArea();
    renderPassInfo.renderArea.offset = {renderArea.offsetX, renderArea.offsetY};
    renderPassInfo.renderArea.extent = {renderArea.width, renderArea.height};
    
    VkClearValue clearColor = {{{0.0f, 0.0f, 0.0f, 1.0f}}};
    renderPassInfo.clearValueCount = 1;
    renderPassInfo.pClearValues = &clearColor;

    vkCmdBeginRenderPass(m_VkCommandBuffer, &renderPassInfo, VK_SUBPASS_CONTENTS_INLINE);

    m_State = ECommandState::IsInsideRenderPass;
}

void NebulaEngine::RHI::RHIVkCommandBuffer::EndRenderPass()
{
    ASSERT(m_State == ECommandState::IsInsideRenderPass);
    vkCmdEndRenderPass(m_VkCommandBuffer);
    m_State = ECommandState::ReadyForSubmit;
}

void NebulaEngine::RHI::RHIVkCommandBuffer::Clear()
{
    m_State = ECommandState::NeedReset;
}

void NebulaEngine::RHI::RHIVkCommandBuffer::Begin()
{
    {
        {
            ScopeLock ScopeLock(m_CommandBufferPool->GetFence());
            if(m_State == ECommandState::NeedReset)
            {
                vkResetCommandBuffer(m_VkCommandBuffer, /*VkCommandBufferResetFlagBits*/ 0);
            }
            else
            {
                ASSERT(m_State == ECommandState::ReadyForBegin);
            }
        }
    }

    VkCommandBufferBeginInfo beginInfo{};
    beginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
    // todo: set usage
    if (vkBeginCommandBuffer(m_VkCommandBuffer, &beginInfo) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to begin recording command buffer!");
    }

    m_State = ECommandState::IsInsideBegin;
}

void NebulaEngine::RHI::RHIVkCommandBuffer::End()
{
    ASSERT(m_State == ECommandState::ReadyForEnd);
    
    if (vkEndCommandBuffer(m_VkCommandBuffer) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkCommandBuffer::End]: failed to record command buffer!");
    }
    
    m_State = ECommandState::HasEnded;
}

void NebulaEngine::RHI::RHIVkCommandBuffer::SetViewport(f32 x, f32 y, f32 width, f32 height, f32 minDepth, f32 maxDepth)
{
    const VkViewport viewport
    {
        x, y, width, height, minDepth, maxDepth
    };
    vkCmdSetViewport(m_VkCommandBuffer, 0, 1, &viewport);
    
}

void NebulaEngine::RHI::RHIVkCommandBuffer::SetViewport(f32 x, f32 y, f32 width, f32 height)
{
    const VkViewport viewport
   {
       x, y, width, height
   };
    vkCmdSetViewport(m_VkCommandBuffer, 0, 1, &viewport);
}

void NebulaEngine::RHI::RHIVkCommandBuffer::SetScissor(u32 offsetX, u32 offsetY, u32 width, u32 height)
{
    const VkRect2D scissor =
    {
        {0, 0}, {width, height}
    };

    vkCmdSetScissor(m_VkCommandBuffer, 0, 1, &scissor);
    
}

void NebulaEngine::RHI::RHIVkCommandBuffer::BindPipeline(GPUPipeline* pipeline)
{
    vkCmdBindPipeline(m_VkCommandBuffer, static_cast<VkPipelineBindPoint>(pipeline->GetBindPoint()), static_cast<VkPipeline>(pipeline->GetGraphicsPipeline()));
}

