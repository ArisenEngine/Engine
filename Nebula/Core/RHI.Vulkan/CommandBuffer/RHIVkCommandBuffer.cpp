#include "RHIVkCommandBuffer.h"

#include "RHI/Handles/BufferHandle.h"
#include "RHI/Synchronization/SynchScope.h"


NebulaEngine::RHI::RHIVkCommandBuffer::~RHIVkCommandBuffer() noexcept
{
    LOG_DEBUG("[RHIVkCommandBuffer::~RHIVkCommandBuffer]: ~RHIVkCommandBuffer");
    LOG_DEBUG("## Destory Vulkan CommandBuffer ##");
    // vkFreeCommandBuffers(m_VkDevice, m_VkCommandPool, 1, &m_VkCommandBuffer);
}

NebulaEngine::RHI::RHIVkCommandBuffer::RHIVkCommandBuffer(RHIVkDevice* device, RHIVkCommandBufferPool* pool)
: RHICommandBuffer(device, pool),
m_RHICommandPool(pool)
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

        // todo: separate alloc memory and free memory
        if (vkAllocateCommandBuffers(m_VkDevice, &allocInfo, &m_VkCommandBuffer) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkCommandBuffer::RHIVkCommandBuffer]: failed to allocate command buffers!");
        }
    }
    
    m_State = ECommandState::ReadyForBegin;
}

void* NebulaEngine::RHI::RHIVkCommandBuffer::GetHandle() const
{
    return m_VkCommandBuffer;
}

void* NebulaEngine::RHI::RHIVkCommandBuffer::GetHandlerPointer()
{
    return &m_VkCommandBuffer;
}

void NebulaEngine::RHI::RHIVkCommandBuffer::BeginRenderPass(u32 frameIndex, RenderPassBeginDesc&& desc)
{
    ASSERT(m_State == ECommandState::IsInsideBegin);
    
    VkRenderPassBeginInfo renderPassInfo{};
    renderPassInfo.sType = VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO;
    renderPassInfo.renderPass = static_cast<VkRenderPass>(desc.renderPass->GetHandle(frameIndex));
    renderPassInfo.framebuffer = static_cast<VkFramebuffer>(desc.frameBuffer->GetHandle(frameIndex));
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
}

void NebulaEngine::RHI::RHIVkCommandBuffer::Reset()
{
    m_WaitSemaphores.clear();
    m_SignalSemaphores.clear();
    m_WaitStages.clear();
    m_State = ECommandState::ReadyForBegin;
    m_VkBeginInfo = {};
    m_SubmissionFence.reset();
}

void NebulaEngine::RHI::RHIVkCommandBuffer::ReadyForBegin(u32 frameIndex)
{
        {
            if(m_State == ECommandState::NeedReset)
            {
                Reset();
                vkResetCommandBuffer(m_VkCommandBuffer, /*VkCommandBufferResetFlagBits*/ 0);
            }
        }

    ASSERT(m_State == ECommandState::ReadyForBegin);
}

void NebulaEngine::RHI::RHIVkCommandBuffer::DoBegin()
{
    if (vkBeginCommandBuffer(m_VkCommandBuffer, &m_VkBeginInfo) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to begin recording command buffer!");
    }

    m_State = ECommandState::IsInsideBegin;
}

void NebulaEngine::RHI::RHIVkCommandBuffer::Begin(u32 frameIndex)
{
    ReadyForBegin(frameIndex);
    m_VkBeginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
    DoBegin();
}

void NebulaEngine::RHI::RHIVkCommandBuffer::Begin(u32 frameIndex, u32 commandBufferUsage)
{
    ReadyForBegin(frameIndex);
    m_VkBeginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
    m_VkBeginInfo.flags = (commandBufferUsage);
    DoBegin();
}

void NebulaEngine::RHI::RHIVkCommandBuffer::End()
{
    ASSERT(m_WaitSemaphores.size() == m_WaitStages.size());
    
    if (vkEndCommandBuffer(m_VkCommandBuffer) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkCommandBuffer::End]: failed to record command buffer!");
    }
    
    m_State = ECommandState::ReadyForSubmit;
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

void NebulaEngine::RHI::RHIVkCommandBuffer::BindPipeline(u32 frameIndex, GPUPipeline* pipeline)
{
    vkCmdBindPipeline(m_VkCommandBuffer, static_cast<VkPipelineBindPoint>(pipeline->GetBindPoint()), static_cast<VkPipeline>(pipeline->GetGraphicsPipeline(frameIndex)));
}

void NebulaEngine::RHI::RHIVkCommandBuffer::Draw(u32 vertexCount, u32 instanceCount, u32 firstVertex, u32 firstInstance)
{
    vkCmdDraw(m_VkCommandBuffer, vertexCount, instanceCount, firstVertex, firstInstance);
}

void NebulaEngine::RHI::RHIVkCommandBuffer::BindVertexBuffers(u32 firstBinding,
    Containers::Vector<BufferHandle*> buffers, Containers::Vector<u64> offsets)
{
    ASSERT(buffers.size() == offsets.size());
    m_VertexBuffers.resize(buffers.size());
    for (int i = 0; i < buffers.size(); ++i)
    {
        m_VertexBuffers[i] = static_cast<VkBuffer>(buffers[i]->GetHandle());
    }
    ASSERT(m_VertexBuffers.size() > 0);
    vkCmdBindVertexBuffers(m_VkCommandBuffer, firstBinding, buffers.size(), m_VertexBuffers.data(), offsets.data());
}

void NebulaEngine::RHI::RHIVkCommandBuffer::WaitSemaphore(RHISemaphore* semaphore, EPipelineStageFlag stage)
{
    m_WaitSemaphores.emplace_back(static_cast<VkSemaphore>(semaphore->GetHandle()));
    m_WaitStages.emplace_back(static_cast<VkPipelineStageFlags>(stage));
}

const VkSemaphore* NebulaEngine::RHI::RHIVkCommandBuffer::GetWaitSemaphores() const
{
    return m_WaitSemaphores.data();
}

NebulaEngine::u32 NebulaEngine::RHI::RHIVkCommandBuffer::GetWaitSemaphoresCount() const
{
    return m_WaitSemaphores.size();
}

void NebulaEngine::RHI::RHIVkCommandBuffer::SignalSemaphore(RHISemaphore* semaphore)
{
    m_SignalSemaphores.emplace_back(static_cast<VkSemaphore>(semaphore->GetHandle()));
}

const VkSemaphore* NebulaEngine::RHI::RHIVkCommandBuffer::GetSignalSemaphores() const
{
    return m_SignalSemaphores.data();
}

NebulaEngine::u32 NebulaEngine::RHI::RHIVkCommandBuffer::GetSignalSemaphoresCount() const
{
    return m_SignalSemaphores.size();
}

const VkPipelineStageFlags* NebulaEngine::RHI::RHIVkCommandBuffer::GetWaitStageMask() const
{
    return m_WaitStages.data();
}

void NebulaEngine::RHI::RHIVkCommandBuffer::CopyBuffer(BufferHandle const * src, u64 srcOffset,
                                                       BufferHandle const * dst, u64 dstOffset, u64 size)
{
    // TODO: support multiple copy regions
    ASSERT(m_State == ECommandState::IsInsideBegin);
    VkBufferCopy copyRegion {};
    copyRegion.srcOffset = srcOffset;
    copyRegion.dstOffset = dstOffset;
    copyRegion.size = size;
    vkCmdCopyBuffer(m_VkCommandBuffer,
        static_cast<VkBuffer>(src->GetHandle()),
        static_cast<VkBuffer>(dst->GetHandle()),
        1, &copyRegion);
}

VkFence NebulaEngine::RHI::RHIVkCommandBuffer::GetSubmissionFence() const
{
    return m_SubmissionFence.has_value() ? m_SubmissionFence.value() : VK_NULL_HANDLE;
}

void NebulaEngine::RHI::RHIVkCommandBuffer::InjectFence(RHIFence* fence)
{
    m_SubmissionFence = static_cast<VkFence>(fence->GetHandle());
}

void NebulaEngine::RHI::RHIVkCommandBuffer::WaitForFence(u32 frameIndex)
{
   {
       ScopeLock ScopeLock(m_CommandBufferPool->GetFence(frameIndex));
   }
}

void NebulaEngine::RHI::RHIVkCommandBuffer::Release()
{
    m_State = ECommandState::NeedReset;
}

