#include "RHIVkQueue.h"

#include "Logger/Logger.h"
#include "RHI/CommandBuffer/RHICommandBuffer.h"
#include "../CommandBuffer/RHIVkCommandBuffer.h"
#include "../Program/RHIVkDescriptorPool.h"

ArisenEngine::RHI::RHIVkQueue::RHIVkQueue(VkDevice device, VkQueue queue, RHIQueueType type, IRHIDeferredDeletionQueue* deferredDeletionQueue)
    : m_Device(device), m_Queue(queue), m_Type(type), m_DeferredDeletion(deferredDeletionQueue)
{
}

ArisenEngine::RHI::RHIVkQueue::~RHIVkQueue() noexcept
{
    // Ensure GPU finished before destroying fences.
    if (m_Queue != VK_NULL_HANDLE)
    {
        vkQueueWaitIdle(m_Queue);
    }

    std::lock_guard<std::mutex> lock(m_Mutex);
    for (auto fence : m_FreeFences)
    {
        if (fence) vkDestroyFence(m_Device, fence, nullptr);
    }
    m_FreeFences.clear();

    for (auto& s : m_InFlight)
    {
        if (s.ownedFence && s.fence) vkDestroyFence(m_Device, s.fence, nullptr);
    }
    m_InFlight.clear();
}

VkFence ArisenEngine::RHI::RHIVkQueue::AcquireFence()
{
    if (!m_FreeFences.empty())
    {
        VkFence fence = m_FreeFences.back();
        m_FreeFences.pop_back();
        vkResetFences(m_Device, 1, &fence);
        return fence;
    }

    VkFenceCreateInfo fenceInfo{};
    fenceInfo.sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
    fenceInfo.flags = 0; // unsignaled

    VkFence fence = VK_NULL_HANDLE;
    if (vkCreateFence(m_Device, &fenceInfo, nullptr, &fence) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkQueue::AcquireFence]: failed to create fence!");
    }
    return fence;
}

void ArisenEngine::RHI::RHIVkQueue::RecycleFence(VkFence fence)
{
    if (fence == VK_NULL_HANDLE) return;
    // Fence is signaled; reset so it can be reused.
    vkResetFences(m_Device, 1, &fence);
    m_FreeFences.emplace_back(fence);
}

ArisenEngine::RHI::RHIGpuTicket ArisenEngine::RHI::RHIVkQueue::Submit(RHICommandBuffer* commandBuffer)
{
    ASSERT(commandBuffer && commandBuffer->ReadyForSubmit());

    // Internal fence ownership path (queue-managed fences)
    VkFence fence = AcquireFence();
    return SubmitWithFence(commandBuffer, fence, true);
}

ArisenEngine::RHI::RHIGpuTicket ArisenEngine::RHI::RHIVkQueue::SubmitWithFence(RHICommandBuffer* commandBuffer, VkFence fence, bool ownedFence)
{
    ASSERT(commandBuffer && commandBuffer->ReadyForSubmit());

    auto* vkCmd = static_cast<RHIVkCommandBuffer*>(commandBuffer);

    VkSubmitInfo submitInfo{};
    submitInfo.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;

    if (vkCmd->GetWaitSemaphoresCount() > 0)
    {
        submitInfo.waitSemaphoreCount = vkCmd->GetWaitSemaphoresCount();
        submitInfo.pWaitSemaphores = vkCmd->GetWaitSemaphores();
        submitInfo.pWaitDstStageMask = vkCmd->GetWaitStageMask();
    }

    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = (static_cast<VkCommandBuffer*>(commandBuffer->GetHandlerPointer()));

    if (vkCmd->GetSignalSemaphoresCount() > 0)
    {
        submitInfo.signalSemaphoreCount = vkCmd->GetSignalSemaphoresCount();
        submitInfo.pSignalSemaphores = vkCmd->GetSignalSemaphores();
    }

    std::lock_guard<std::mutex> lock(m_Mutex);

    const auto submitId = m_NextSubmitId.fetch_add(1, std::memory_order_acq_rel);

    if (vkQueueSubmit(m_Queue, 1, &submitInfo, fence) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkQueue::Submit]: failed to submit command buffer!");
    }

    m_LastSubmittedSubmitId.store(submitId, std::memory_order_release);
    m_InFlight.push_back({ fence, submitId, ownedFence });

    // Mark descriptor pools used by this submission so ResetPool can be GPU-safe.
    for (const auto& t : vkCmd->GetTrackedDescriptorPools())
    {
        auto* p = static_cast<RHIVkDescriptorPool*>(t.pool);
        if (p) p->MarkPoolUsed(t.poolId, m_Type, submitId);
    }
    vkCmd->ClearTrackedDescriptorPools();
    return submitId;
}

void ArisenEngine::RHI::RHIVkQueue::Update()
{
    RHIGpuTicket newCompleted = m_CompletedSubmitId.load(std::memory_order_acquire);
    Containers::Vector<VkFence> recycle;

    {
        std::lock_guard<std::mutex> lock(m_Mutex);

        while (!m_InFlight.empty())
        {
            const auto& front = m_InFlight.front();
            const auto status = vkGetFenceStatus(m_Device, front.fence);
            if (status == VK_SUCCESS)
            {
                newCompleted = front.submitId;
                if (front.ownedFence)
                {
                    recycle.emplace_back(front.fence);
                }
                m_InFlight.pop_front();
                continue;
            }
            break; // in-order queue: stop at first not completed
        }

        for (auto fence : recycle)
        {
            RecycleFence(fence);
        }
    }

    m_CompletedSubmitId.store(newCompleted, std::memory_order_release);

    if (m_DeferredDeletion)
    {
        m_DeferredDeletion->Flush(m_Type, newCompleted);
    }
}

