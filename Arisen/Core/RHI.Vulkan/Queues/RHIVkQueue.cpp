#include "RHIVkQueue.h"
#include "../Devices/RHIVkDevice.h"
#include "Logger/Logger.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "../CommandBuffer/RHIVkCommandBuffer.h"
#include "../Program/RHIVkDescriptorPool.h"

ArisenEngine::RHI::RHIVkQueue::RHIVkQueue(VkDevice device, VkQueue queue, RHIQueueType type, IRHIDeferredDeletionQueue* deferredDeletionQueue, RHIResourceRegistry* resourceRegistry)
    : m_Device(device), m_Queue(queue), m_Type(type), m_DeferredDeletion(deferredDeletionQueue), m_ResourceRegistry(resourceRegistry)
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

    VkSemaphoreCreateInfo semaphoreInfo{};
    semaphoreInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO;
    semaphoreInfo.pNext = &typeInfo;

    VkResult result = vkCreateSemaphore(m_Device, &semaphoreInfo, nullptr, &m_TimelineSemaphore);
    if (result != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkQueue]: failed to create timeline semaphore!");
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
    const auto submitTicket = m_LatestTicket.fetch_add(1, std::memory_order_acq_rel) + 1;

    VkTimelineSemaphoreSubmitInfo timelineInfo{};
    timelineInfo.sType = VK_STRUCTURE_TYPE_TIMELINE_SEMAPHORE_SUBMIT_INFO;

    VkSubmitInfo submitInfo{};
    submitInfo.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;
    submitInfo.pNext = &timelineInfo;

    Containers::Vector<uint64_t> waitValues;
    if (vkCmd->GetWaitSemaphoresCount() > 0)
    {
        submitInfo.waitSemaphoreCount = vkCmd->GetWaitSemaphoresCount();
        submitInfo.pWaitSemaphores = vkCmd->GetWaitSemaphores();
        submitInfo.pWaitDstStageMask = vkCmd->GetWaitStageMask();
        waitValues.resize(submitInfo.waitSemaphoreCount, 0); // Binary semaphores use 0
        timelineInfo.waitSemaphoreValueCount = submitInfo.waitSemaphoreCount;
        timelineInfo.pWaitSemaphoreValues = waitValues.data();
    }

    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = &vkCmd->m_VkCommandBuffer;

    Containers::Vector<VkSemaphore> signalSemaphores;
    Containers::Vector<uint64_t> signalValues;

    signalSemaphores.emplace_back(m_TimelineSemaphore);
    signalValues.emplace_back(submitTicket);

    if (vkCmd->GetSignalSemaphoresCount() > 0)
    {
        const auto* sems = vkCmd->GetSignalSemaphores();
        for (UInt32 i = 0; i < vkCmd->GetSignalSemaphoresCount(); ++i)
        {
            signalSemaphores.emplace_back(sems[i]);
            signalValues.emplace_back(0); // Binary semaphores ignore this value, but array size must match
        }
    }

    submitInfo.signalSemaphoreCount = static_cast<UInt32>(signalSemaphores.size());
    submitInfo.pSignalSemaphores = signalSemaphores.data();
    
    timelineInfo.waitSemaphoreValueCount = submitInfo.waitSemaphoreCount;
    timelineInfo.pWaitSemaphoreValues = waitValues.data();
    timelineInfo.signalSemaphoreValueCount = static_cast<uint32_t>(signalValues.size());
    timelineInfo.pSignalSemaphoreValues = signalValues.data();


    if (vkQueueSubmit(m_Queue, 1, &submitInfo, fence) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkQueue::Submit]: failed to submit command buffer!");
    }

    // m_LatestTicket already updated via fetch_add above.

    // Mark descriptor pools used by this submission so ResetPool can be GPU-safe.
    for (const auto& t : vkCmd->GetTrackedDescriptorPools())
    {
        auto* p = static_cast<RHIVkDescriptorPool*>(t.pool);
        p->MarkPoolUsed(t.poolId, m_Type, submitTicket);
    }
    vkCmd->ClearTrackedDescriptorPools();

    // Automatically release references captured by the CommandBuffer.
    // They will be destroyed only when the GPU work (ticket) is completed.
    if (m_ResourceRegistry)
    {
        for (auto h : vkCmd->GetTrackedResourceHandles())
        {
            m_ResourceRegistry->Release(h, m_Type, submitTicket);
        }
    }
    vkCmd->ClearTrackedResourceHandles();
    vkCmd->SetLatestSubmitTicket(submitTicket);

    return submitTicket;
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
        m_CompletedSubmitTicket.store(static_cast<RHIGpuTicket>(completed), std::memory_order_release);
    }

    if (m_DeferredDeletion)
    {
        m_DeferredDeletion->Flush(m_Type, m_CompletedSubmitTicket.load(std::memory_order_acquire));
    }
}

void ArisenEngine::RHI::RHIVkQueue::WaitForTicket(RHIGpuTicket ticket)
{
    if (ticket == 0)
    {
        return;
    }

    Update();
    
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
