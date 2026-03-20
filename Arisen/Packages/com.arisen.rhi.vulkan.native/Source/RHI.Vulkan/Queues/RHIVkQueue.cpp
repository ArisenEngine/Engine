#include "Queues/RHIVkQueue.h"
#include "Core/RHIVkDevice.h"
#include "Logger/Logger.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "Commands/RHIVkCommandBuffer.h"
#include "Descriptors/RHIVkDescriptorPool.h"
#include "Profiler.h"

ArisenEngine::RHI::RHIVkQueue::RHIVkQueue(RHIVkDevice* rhiDevice, VkDevice device, VkQueue queue, RHIQueueType type,
                                          IRHIDeferredDeletionQueue* deferredDeletionQueue,
                                          RHIResourceRegistry* resourceRegistry)
    : m_RHIDevice(rhiDevice), m_Device(device), m_Queue(queue), m_Type(type), m_DeferredDeletion(deferredDeletionQueue),
      m_ResourceRegistry(resourceRegistry)
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
    if (m_TimelineSemaphoreHandle.IsValid())
    {
        m_RHIDevice->GetFactory()->ReleaseSemaphore(m_TimelineSemaphoreHandle);
        m_TimelineSemaphoreHandle = RHISemaphoreHandle::Invalid();
        m_TimelineSemaphore = VK_NULL_HANDLE;
    }
}

void ArisenEngine::RHI::RHIVkQueue::CreateTimelineSemaphore()
{
    m_TimelineSemaphoreHandle = m_RHIDevice->GetFactory()->CreateTimelineSemaphore(0);
    if (!m_TimelineSemaphoreHandle.IsValid())
    {
        LOG_FATAL_AND_THROW("[RHIVkQueue]: failed to create timeline semaphore from factory!");
    }

    auto* semItem = m_RHIDevice->GetSemaphorePool()->Get(m_TimelineSemaphoreHandle);
    if (semItem && semItem->semaphore != VK_NULL_HANDLE)
    {
        m_TimelineSemaphore = semItem->semaphore;
    }
    else
    {
        LOG_FATAL_AND_THROW("[RHIVkQueue]: timeline semaphore has invalid raw handle!");
    }
}

#include "Presentation/RHIVkSwapChain.h"

ArisenEngine::RHI::RHIGpuTicket ArisenEngine::RHI::RHIVkQueue::Submit(RHICommandBufferHandle handle,
                                                                      const RHISubmitDescriptor* descriptor)
{
    ARISEN_PROFILE_ZONE("RHI::VulkanQueueSubmit");
    std::lock_guard<std::mutex> lock(m_SubmitMutex);

    auto* commandBuffer = m_RHIDevice->GetCommandBuffer(handle);
    ASSERT(commandBuffer && commandBuffer->ReadyForSubmit());

    RHIVkCommandBuffer* vkCmd = static_cast<RHIVkCommandBuffer*>(commandBuffer);
    if (!vkCmd->IsCompiled())
    {
        vkCmd->Compile();
    }

    Containers::Vector<VkSemaphore> waitSems;
    Containers::Vector<VkPipelineStageFlags> waitStages;
    Containers::Vector<uint64_t> waitValues;
    Containers::Vector<VkSemaphore> signalSems;
    Containers::Vector<uint64_t> signalValues;

    if (descriptor)
    {
        auto* vkDevice = m_RHIDevice;
        UInt32 frameIndex = commandBuffer->GetCurrentFrameIndex();

        if (descriptor->WaitSwapChain)
        {
            auto semHandle = static_cast<RHIVkSwapChain*>(descriptor->WaitSwapChain)->GetImageAvailableSemaphore(
                frameIndex);
            if (semHandle.IsValid())
            {
                auto* semItem = vkDevice->GetSemaphorePool()->Get(semHandle);
                if (semItem)
                {
                    waitSems.push_back(semItem->semaphore);
                    waitStages.push_back(VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT);
                    waitValues.push_back(0);
                }
            }
        }

        if (descriptor->SignalSwapChain)
        {
            auto semHandle = static_cast<RHIVkSwapChain*>(descriptor->SignalSwapChain)->GetRenderFinishSemaphore(
                frameIndex);
            if (semHandle.IsValid())
            {
                auto* semItem = vkDevice->GetSemaphorePool()->Get(semHandle);
                if (semItem)
                {
                    signalSems.push_back(semItem->semaphore);
                    signalValues.push_back(0);
                }
            }
        }

        // Handle explicit semaphores
        for (UInt32 i = 0; i < descriptor->waitSemaphoreCount; ++i)
        {
            auto* semItem = vkDevice->GetSemaphorePool()->Get(descriptor->pWaitSemaphores[i]);
            if (semItem)
            {
                waitSems.push_back(semItem->semaphore);
                waitStages.push_back(descriptor->pWaitDstStageMask
                                         ? descriptor->pWaitDstStageMask[i]
                                         : VK_PIPELINE_STAGE_ALL_COMMANDS_BIT);
                waitValues.push_back(descriptor->pWaitValues ? descriptor->pWaitValues[i] : 0);
            }
        }

        for (UInt32 i = 0; i < descriptor->signalSemaphoreCount; ++i)
        {
            auto* semItem = vkDevice->GetSemaphorePool()->Get(descriptor->pSignalSemaphores[i]);
            if (semItem)
            {
                signalSems.push_back(semItem->semaphore);
                signalValues.push_back(descriptor->pSignalValues ? descriptor->pSignalValues[i] : 0);
            }
        }
    }

    const auto submitTicket = m_LatestTicket.fetch_add(1, std::memory_order_acq_rel) + 1;

    VkTimelineSemaphoreSubmitInfo timelineInfo{};
    timelineInfo.sType = VK_STRUCTURE_TYPE_TIMELINE_SEMAPHORE_SUBMIT_INFO;

    VkSubmitInfo submitInfo{};
    submitInfo.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;
    submitInfo.pNext = &timelineInfo;

    submitInfo.waitSemaphoreCount = static_cast<uint32_t>(waitSems.size());
    submitInfo.pWaitSemaphores = waitSems.data();
    submitInfo.pWaitDstStageMask = waitStages.data();

    timelineInfo.waitSemaphoreValueCount = submitInfo.waitSemaphoreCount;
    timelineInfo.pWaitSemaphoreValues = waitValues.data();

    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = &vkCmd->m_VkCommandBuffer;

    signalSems.push_back(m_TimelineSemaphore);
    signalValues.push_back(submitTicket);

    submitInfo.signalSemaphoreCount = static_cast<UInt32>(signalSems.size());
    submitInfo.pSignalSemaphores = signalSems.data();

    timelineInfo.signalSemaphoreValueCount = static_cast<uint32_t>(signalValues.size());
    timelineInfo.pSignalSemaphoreValues = signalValues.data();

    vkCmd->SetLatestSubmitTicket(submitTicket);

    if (vkQueueSubmit(m_Queue, 1, &submitInfo, VK_NULL_HANDLE) != VK_SUCCESS)
    {
        // If submission fails, we should clear the ticket.
        vkCmd->SetLatestSubmitTicket(0);
        LOG_FATAL_AND_THROW("[RHIVkQueue::Submit]: failed to submit command buffer!");
    }

    // Mark descriptor pools used by this submission so ResetPool can be GPU-safe.
    for (const auto& trackedPool : vkCmd->GetTrackedDescriptorPools())
    {
        auto* poolItem = m_RHIDevice->GetDescriptorPoolPool()->Get(trackedPool.poolHandle);
        if (poolItem && poolItem->pool)
        {
            static_cast<RHIVkDescriptorPool*>(poolItem->pool)->MarkPoolUsed(trackedPool.poolId, m_Type, submitTicket);
        }
    }
    vkCmd->ClearTrackedDescriptorPools();

    // Automatically release references captured by the CommandBuffer.
    // They will be destroyed only when the GPU work (ticket) is completed.
    if (m_ResourceRegistry)
    {
        for (RHIResourceHandle trackedHandle : vkCmd->GetTrackedResourceHandles())
        {
            // Stamp usage of this resource on this queue
            m_ResourceRegistry->UpdateTicket(trackedHandle, m_Type, submitTicket);
            m_ResourceRegistry->Release(trackedHandle);
        }
    }
    vkCmd->ClearTrackedResourceHandles();
    vkCmd->SetLatestSubmitTicket(submitTicket);

    return submitTicket;
}

void ArisenEngine::RHI::RHIVkQueue::Update()
{
    ARISEN_PROFILE_ZONE("RHI::VulkanQueueUpdate");
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

void ArisenEngine::RHI::RHIVkQueue::WaitIdle()
{
    ARISEN_PROFILE_ZONE("RHI::VulkanQueueWaitIdle");
    if (m_Queue != VK_NULL_HANDLE)
    {
        vkQueueWaitIdle(m_Queue);
    }
}

void ArisenEngine::RHI::RHIVkQueue::WaitForTicket(RHIGpuTicket ticket)
{
    ARISEN_PROFILE_ZONE("RHI::VulkanQueueWait");
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

    VkResult res = vkWaitSemaphores(m_Device, &waitInfo, UINT64_MAX);
    if (res != VK_SUCCESS)
    {
        LOG_ERROR(String::Format("[RHIVkQueue::WaitForTicket]: vkWaitSemaphores failed! VkResult: %d", (int)res));
    }
    Update();
}
