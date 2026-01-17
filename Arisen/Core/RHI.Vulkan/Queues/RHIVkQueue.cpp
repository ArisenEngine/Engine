#include "RHIVkQueue.h"

#include "Logger/Logger.h"
#include "RHI/CommandBuffer/RHICommandBuffer.h"
#include "../CommandBuffer/RHIVkCommandBuffer.h"
#include "../Program/RHIVkDescriptorPool.h"

ArisenEngine::RHI::RHIVkQueue::RHIVkQueue(VkDevice device, VkQueue queue, RHIQueueType type, IRHIDeferredDeletionQueue* deferredDeletionQueue)
    : m_Device(device), m_Queue(queue), m_Type(type), m_DeferredDeletion(deferredDeletionQueue)
{
    CreateTimelineSemaphore();
}

ArisenEngine::RHI::RHIVkQueue::~RHIVkQueue() noexcept
{
    // Ensure GPU finished before destroying the queue semaphore.
    if (m_Queue != VK_NULL_HANDLE)
    {
        vkQueueWaitIdle(m_Queue);
    }
    if (m_TimelineSemaphore != VK_NULL_HANDLE)
    {
        vkDestroySemaphore(m_Device, m_TimelineSemaphore, nullptr);
        m_TimelineSemaphore = VK_NULL_HANDLE;
    }
}

void ArisenEngine::RHI::RHIVkQueue::CreateTimelineSemaphore()
{
    VkSemaphoreTypeCreateInfo typeInfo{};
    typeInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_TYPE_CREATE_INFO;
    typeInfo.semaphoreType = VK_SEMAPHORE_TYPE_TIMELINE;
    typeInfo.initialValue = 0;

    VkSemaphoreCreateInfo createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO;
    createInfo.pNext = &typeInfo;

    if (vkCreateSemaphore(m_Device, &createInfo, nullptr, &m_TimelineSemaphore) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkQueue::CreateTimelineSemaphore]: failed to create timeline semaphore!");
    }
}

ArisenEngine::RHI::RHIGpuTicket ArisenEngine::RHI::RHIVkQueue::Submit(RHICommandBuffer* commandBuffer)
{
    ASSERT(commandBuffer && commandBuffer->ReadyForSubmit());

    return SubmitWithFence(commandBuffer, VK_NULL_HANDLE, false);
}

ArisenEngine::RHI::RHIGpuTicket ArisenEngine::RHI::RHIVkQueue::SubmitWithFence(RHICommandBuffer* commandBuffer, VkFence fence, bool ownedFence)
{
    ASSERT(commandBuffer && commandBuffer->ReadyForSubmit());
    (void)ownedFence;

    auto* vkCmd = static_cast<RHIVkCommandBuffer*>(commandBuffer);

    // Timeline signal value
    const auto submitId = m_NextSubmitId.fetch_add(1, std::memory_order_acq_rel);

    VkTimelineSemaphoreSubmitInfo timelineInfo{};
    timelineInfo.sType = VK_STRUCTURE_TYPE_TIMELINE_SEMAPHORE_SUBMIT_INFO;
    timelineInfo.signalSemaphoreValueCount = 1;
    timelineInfo.pSignalSemaphoreValues = &submitId;

    VkSubmitInfo submitInfo{};
    submitInfo.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;
    submitInfo.pNext = &timelineInfo;

    if (vkCmd->GetWaitSemaphoresCount() > 0)
    {
        submitInfo.waitSemaphoreCount = vkCmd->GetWaitSemaphoresCount();
        submitInfo.pWaitSemaphores = vkCmd->GetWaitSemaphores();
        submitInfo.pWaitDstStageMask = vkCmd->GetWaitStageMask();
    }

    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = (static_cast<VkCommandBuffer*>(commandBuffer->GetHandlerPointer()));

    // Always signal the timeline semaphore; also propagate any binary signal semaphores.
    Containers::Vector<VkSemaphore> signalSemaphores;
    Containers::Vector<uint64_t> signalValues;

    signalSemaphores.emplace_back(m_TimelineSemaphore);
    signalValues.emplace_back(submitId);

    if (vkCmd->GetSignalSemaphoresCount() > 0)
    {
        const auto* sems = vkCmd->GetSignalSemaphores();
        for (UInt32 i = 0; i < vkCmd->GetSignalSemaphoresCount(); ++i)
        {
            signalSemaphores.emplace_back(sems[i]);
            signalValues.emplace_back(0); // Value ignored for binary semaphores
        }
    }
    submitInfo.signalSemaphoreCount = static_cast<UInt32>(signalSemaphores.size());
    submitInfo.pSignalSemaphores = signalSemaphores.data();

    // Update timeline info to match counts
    timelineInfo.signalSemaphoreValueCount = static_cast<uint32_t>(signalValues.size());
    timelineInfo.pSignalSemaphoreValues = signalValues.data();

    std::lock_guard<std::mutex> lock(m_Mutex);

    if (vkQueueSubmit(m_Queue, 1, &submitInfo, fence) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkQueue::Submit]: failed to submit command buffer!");
    }

    m_LastSubmittedSubmitId.store(submitId, std::memory_order_release);

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
    if (m_TimelineSemaphore == VK_NULL_HANDLE)
    {
        return;
    }

    uint64_t completed = 0;
    if (vkGetSemaphoreCounterValue(m_Device, m_TimelineSemaphore, &completed) == VK_SUCCESS)
    {
        m_CompletedSubmitId.store(static_cast<RHIGpuTicket>(completed), std::memory_order_release);
    }

    if (m_DeferredDeletion)
    {
        m_DeferredDeletion->Flush(m_Type, m_CompletedSubmitId.load(std::memory_order_acquire));
    }
}

void ArisenEngine::RHI::RHIVkQueue::WaitForTicket(RHIGpuTicket ticket)
{
    if (ticket == 0)
    {
        return;
    }

    if (GetCompletedTicket() >= ticket)
    {
        return;
    }
    if (m_TimelineSemaphore == VK_NULL_HANDLE)
    {
        return;
    }

    VkSemaphoreWaitInfo waitInfo{};
    waitInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_WAIT_INFO;
    waitInfo.semaphoreCount = 1;
    waitInfo.pSemaphores = &m_TimelineSemaphore;
    waitInfo.pValues = &ticket;

    vkWaitSemaphores(m_Device, &waitInfo, UINT64_MAX);
    Update();
}
