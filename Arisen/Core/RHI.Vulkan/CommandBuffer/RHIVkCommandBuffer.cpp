#include "RHIVkCommandBuffer.h"

#include "../Program/RHIVkGPUPipeline.h"
#include "../Program/RHIVkGPUPipelineStateObject.h"
#include "RHI/Handles/BufferHandle.h"
#include "RHI/Synchronization/SynchScope.h"


ArisenEngine::RHI::RHIVkCommandBuffer::~RHIVkCommandBuffer() noexcept
{
    LOG_DEBUG("[RHIVkCommandBuffer::~RHIVkCommandBuffer]: ~RHIVkCommandBuffer");
    LOG_DEBUG("## Destory Vulkan CommandBuffer ##");
    // vkFreeCommandBuffers(m_VkDevice, m_VkCommandPool, 1, &m_VkCommandBuffer);
}

ArisenEngine::RHI::RHIVkCommandBuffer::RHIVkCommandBuffer(RHIVkDevice* device, RHIVkCommandBufferPool* pool)
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

void* ArisenEngine::RHI::RHIVkCommandBuffer::GetHandle() const
{
    return m_VkCommandBuffer;
}

void* ArisenEngine::RHI::RHIVkCommandBuffer::GetHandlerPointer()
{
    return &m_VkCommandBuffer;
}

void ArisenEngine::RHI::RHIVkCommandBuffer::BeginRenderPass(UInt32 frameIndex, RenderPassBeginDesc&& desc)
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

void ArisenEngine::RHI::RHIVkCommandBuffer::EndRenderPass()
{
    ASSERT(m_State == ECommandState::IsInsideRenderPass);
    vkCmdEndRenderPass(m_VkCommandBuffer);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::Reset()
{
    m_WaitSemaphores.clear();
    m_SignalSemaphores.clear();
    m_WaitStages.clear();
    m_State = ECommandState::ReadyForBegin;
    m_VkBeginInfo = {};
    m_SubmissionFence.reset();

    m_VertexBuffers.clear();
    m_VertexBindingOffsets.clear();
    m_IndexBuffer.reset();
    m_IndexOffset.reset();
    m_IndexType.reset();

    m_CurrentPipeline = nullptr;
}

void ArisenEngine::RHI::RHIVkCommandBuffer::ReadyForBegin(UInt32 frameIndex)
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

void ArisenEngine::RHI::RHIVkCommandBuffer::DoBegin()
{
    if (vkBeginCommandBuffer(m_VkCommandBuffer, &m_VkBeginInfo) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to begin recording command buffer!");
    }

    m_State = ECommandState::IsInsideBegin;
}

void ArisenEngine::RHI::RHIVkCommandBuffer::Begin(UInt32 frameIndex)
{
    ReadyForBegin(frameIndex);
    m_VkBeginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
    DoBegin();
}

void ArisenEngine::RHI::RHIVkCommandBuffer::Begin(UInt32 frameIndex, UInt32 commandBufferUsage)
{
    ReadyForBegin(frameIndex);
    m_VkBeginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
    m_VkBeginInfo.flags = (commandBufferUsage);
    DoBegin();
}

void ArisenEngine::RHI::RHIVkCommandBuffer::End()
{
    ASSERT(m_WaitSemaphores.size() == m_WaitStages.size());
    
    if (vkEndCommandBuffer(m_VkCommandBuffer) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkCommandBuffer::End]: failed to record command buffer!");
    }
    
    m_State = ECommandState::ReadyForSubmit;
}

void ArisenEngine::RHI::RHIVkCommandBuffer::SetViewport(Float32 x, Float32 y, Float32 width, Float32 height, Float32 minDepth, Float32 maxDepth)
{
    const VkViewport viewport
    {
        x, y, width, height, minDepth, maxDepth
    };
    vkCmdSetViewport(m_VkCommandBuffer, 0, 1, &viewport);
    
}

void ArisenEngine::RHI::RHIVkCommandBuffer::SetViewport(Float32 x, Float32 y, Float32 width, Float32 height)
{
    const VkViewport viewport
   {
       x, y, width, height
   };
    vkCmdSetViewport(m_VkCommandBuffer, 0, 1, &viewport);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width, UInt32 height)
{
    const VkRect2D scissor =
    {
        {0, 0}, {width, height}
    };

    vkCmdSetScissor(m_VkCommandBuffer, 0, 1, &scissor);
    
}

void ArisenEngine::RHI::RHIVkCommandBuffer::BindPipeline(UInt32 frameIndex, GPUPipeline* pipeline)
{
    m_CurrentPipeline = pipeline;
    vkCmdBindPipeline(m_VkCommandBuffer, static_cast<VkPipelineBindPoint>(pipeline->GetBindPoint()),
        static_cast<VkPipeline>(pipeline->GetGraphicsPipeline(frameIndex)));
}

void ArisenEngine::RHI::RHIVkCommandBuffer::BindDescriptorSets(UInt32 frameIndex, EPipelineBindPoint bindPoint,
    UInt32 firstSet, Containers::Vector<std::shared_ptr<RHIDescriptorSet>>& descriptorsets, UInt32 dynamicOffsetCount, const UInt32* pDynamicOffsets)
{
    if (m_CurrentPipeline == nullptr)
    {
        LOG_FATAL("[RHIVkCommandBuffer::BindDescriptorSets] pipeline is null, should binding pipeline first.")
        return;
    }

    RHIVkGPUPipeline* pipeline = static_cast<RHIVkGPUPipeline*>(m_CurrentPipeline);

    Containers::Vector<VkDescriptorSet> vkDescriptorSets;
    vkDescriptorSets.resize(descriptorsets.size());
    for (UInt32 i = 0; i < descriptorsets.size(); ++i)
    {
        vkDescriptorSets[i] = static_cast<VkDescriptorSet>(descriptorsets[i]->GetHandle());
    }
    vkCmdBindDescriptorSets(m_VkCommandBuffer, static_cast<VkPipelineBindPoint>(bindPoint),
        pipeline->GetPipelineLayout(frameIndex),
        firstSet, descriptorsets.size(),
       vkDescriptorSets.data(),
        dynamicOffsetCount, pDynamicOffsets);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance, UInt32 firstBinding)
{
    if (m_VertexBuffers.size() > 0)
    {
        vkCmdBindVertexBuffers(m_VkCommandBuffer, firstBinding, m_VertexBuffers.size(), m_VertexBuffers.data(), m_VertexBindingOffsets.data());
    }
    vkCmdDraw(m_VkCommandBuffer, vertexCount, instanceCount, firstVertex, firstInstance);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset, UInt32 firstInstance,  UInt32 firstBinding)
{
    if (m_VertexBuffers.size() > 0)
    {
        vkCmdBindVertexBuffers(m_VkCommandBuffer, firstBinding, m_VertexBuffers.size(), m_VertexBuffers.data(), m_VertexBindingOffsets.data());
    }
    
    if (m_IndexBuffer.has_value())
    {
        vkCmdBindIndexBuffer(m_VkCommandBuffer, m_IndexBuffer.value(), m_IndexOffset.value(), static_cast<
                                 VkIndexType>(m_IndexType.value()));
    }

    vkCmdDrawIndexed(m_VkCommandBuffer, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::BindVertexBuffers(BufferHandle* buffer, UInt64 offset)
{
    m_VertexBuffers.emplace_back(static_cast<VkBuffer>(buffer->GetHandle()));
    m_VertexBindingOffsets.emplace_back(offset);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::WaitSemaphore(RHISemaphore* semaphore, EPipelineStageFlag stage)
{
    m_WaitSemaphores.emplace_back(static_cast<VkSemaphore>(semaphore->GetHandle()));
    m_WaitStages.emplace_back(static_cast<VkPipelineStageFlags>(stage));
}

const VkSemaphore* ArisenEngine::RHI::RHIVkCommandBuffer::GetWaitSemaphores() const
{
    return m_WaitSemaphores.data();
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkCommandBuffer::GetWaitSemaphoresCount() const
{
    return m_WaitSemaphores.size();
}

void ArisenEngine::RHI::RHIVkCommandBuffer::SignalSemaphore(RHISemaphore* semaphore)
{
    m_SignalSemaphores.emplace_back(static_cast<VkSemaphore>(semaphore->GetHandle()));
}

const VkSemaphore* ArisenEngine::RHI::RHIVkCommandBuffer::GetSignalSemaphores() const
{
    return m_SignalSemaphores.data();
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkCommandBuffer::GetSignalSemaphoresCount() const
{
    return m_SignalSemaphores.size();
}

const VkPipelineStageFlags* ArisenEngine::RHI::RHIVkCommandBuffer::GetWaitStageMask() const
{
    return m_WaitStages.data();
}

void ArisenEngine::RHI::RHIVkCommandBuffer::CopyBuffer(BufferHandle const * src, UInt64 srcOffset,
                                                       BufferHandle const * dst, UInt64 dstOffset, UInt64 size)
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

void ArisenEngine::RHI::RHIVkCommandBuffer::BindIndexBuffer(BufferHandle* indexBuffer, UInt64 offset, EIndexType type)
{ 
    m_IndexBuffer = static_cast<VkBuffer>(indexBuffer->GetHandle());
    m_IndexOffset = offset;
    m_IndexType = type;
}

VkFence ArisenEngine::RHI::RHIVkCommandBuffer::GetSubmissionFence() const
{
    return m_SubmissionFence.has_value() ? m_SubmissionFence.value() : VK_NULL_HANDLE;
}

void ArisenEngine::RHI::RHIVkCommandBuffer::InjectFence(RHIFence* fence)
{
    m_SubmissionFence = static_cast<VkFence>(fence->GetHandle());
}

void ArisenEngine::RHI::RHIVkCommandBuffer::WaitForFence(UInt32 frameIndex)
{
   {
       ScopeLock ScopeLock(m_CommandBufferPool->GetFence(frameIndex));
   }
}

void ArisenEngine::RHI::RHIVkCommandBuffer::Release()
{
    m_State = ECommandState::NeedReset;
}

