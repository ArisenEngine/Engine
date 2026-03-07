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

#include "Presentation/RHIVkSwapChain.h"

ArisenEngine::RHI::RHIGpuTicket ArisenEngine::RHI::RHIVkQueue::Submit(RHICommandBufferHandle handle,
                                                                      const RHISubmitDescriptor* descriptor)
{
    auto* commandBuffer = m_RHIDevice->GetCommandBuffer(handle);
    ASSERT(commandBuffer && commandBuffer->ReadyForSubmit());

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

    return SubmitWithFence(handle, VK_NULL_HANDLE, false, waitSems, waitStages, waitValues, signalSems, signalValues);
}

ArisenEngine::RHI::RHIGpuTicket ArisenEngine::RHI::RHIVkQueue::SubmitWithFence(
    RHICommandBufferHandle handle, VkFence fence, bool ownedFence,
    const Containers::Vector<VkSemaphore>& extraWaitSems,
    const Containers::Vector<VkPipelineStageFlags>& extraWaitStages,
    const Containers::Vector<uint64_t>& extraWaitValues,
    const Containers::Vector<VkSemaphore>& extraSignalSems,
    const Containers::Vector<uint64_t>& extraSignalValues)
{
    ARISEN_PROFILE_ZONE("RHI::VulkanQueueSubmit");
    std::lock_guard<std::mutex> lock(m_SubmitMutex);
    RHICommandBuffer* pCmd = m_RHIDevice->GetCommandBuffer(handle);
    ASSERT(pCmd && pCmd->ReadyForSubmit());
    (void)ownedFence;

    RHIVkCommandBuffer* vkCmd = static_cast<RHIVkCommandBuffer*>(pCmd);
    if (!vkCmd->IsCompiled())
    {
        vkCmd->Compile();
    }

    // Timeline signal value
    const auto submitTicket = m_LatestTicket.fetch_add(1, std::memory_order_acq_rel) + 1;

    VkTimelineSemaphoreSubmitInfo timelineInfo{};
    timelineInfo.sType = VK_STRUCTURE_TYPE_TIMELINE_SEMAPHORE_SUBMIT_INFO;

    VkSubmitInfo submitInfo{};
    submitInfo.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;
    submitInfo.pNext = &timelineInfo;

    Containers::Vector<VkSemaphore> waitSemaphores;
    Containers::Vector<VkPipelineStageFlags> waitDstStageMask;
    Containers::Vector<uint64_t> waitValues;

    // Add CommandBuffer Internal waits (Removed: Sync moved to Descriptor)
    // if (vkCmd->GetWaitSemaphoresCount() > 0) ...

    // Add Extra waits
    for (size_t i = 0; i < extraWaitSems.size(); ++i)
    {
        waitSemaphores.push_back(extraWaitSems[i]);
        waitDstStageMask.push_back(extraWaitStages[i]);
        waitValues.push_back(extraWaitValues.size() > i ? extraWaitValues[i] : 0);
    }

    submitInfo.waitSemaphoreCount = static_cast<uint32_t>(waitSemaphores.size());
    submitInfo.pWaitSemaphores = waitSemaphores.data();
    submitInfo.pWaitDstStageMask = waitDstStageMask.data();

    timelineInfo.waitSemaphoreValueCount = submitInfo.waitSemaphoreCount;
    timelineInfo.pWaitSemaphoreValues = waitValues.data();


    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = &vkCmd->m_VkCommandBuffer;

    Containers::Vector<VkSemaphore> signalSemaphores;
    Containers::Vector<uint64_t> signalValues;

    signalSemaphores.emplace_back(m_TimelineSemaphore);
    signalValues.emplace_back(submitTicket);

    // if (vkCmd->GetSignalSemaphoresCount() > 0) ... (Removed)

    // Add Extra signals
    for (size_t i = 0; i < extraSignalSems.size(); ++i)
    {
        signalSemaphores.emplace_back(extraSignalSems[i]);
        signalValues.emplace_back(extraSignalValues.size() > i ? extraSignalValues[i] : 0);
    }

    submitInfo.signalSemaphoreCount = static_cast<UInt32>(signalSemaphores.size());
    submitInfo.pSignalSemaphores = signalSemaphores.data();

    timelineInfo.signalSemaphoreValueCount = static_cast<uint32_t>(signalValues.size());
    timelineInfo.pSignalSemaphoreValues = signalValues.data();


    vkCmd->SetLatestSubmitTicket(submitTicket);

    if (vkQueueSubmit(m_Queue, 1, &submitInfo, fence) != VK_SUCCESS)
    {
        // If submission fails, we should clear the ticket.
        vkCmd->SetLatestSubmitTicket(0);
        LOG_FATAL_AND_THROW("[RHIVkQueue::Submit]: failed to submit command buffer!");
    }

    // m_LatestTicket already updated via fetch_add above.

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

ArisenEngine::RHI::RHIGpuTicket ArisenEngine::RHI::RHIVkQueue::SubmitRaw(
    VkCommandBuffer commandBuffer,
    const Containers::Vector<VkSemaphore>& waitSems,
    const Containers::Vector<VkPipelineStageFlags>& waitStages,
    const Containers::Vector<uint64_t>& waitValues,
    const Containers::Vector<VkSemaphore>& signalSems,
    const Containers::Vector<uint64_t>& signalValues)
{
    ARISEN_PROFILE_ZONE("RHI::VulkanQueueSubmitRaw");
    std::lock_guard<std::mutex> lock(m_SubmitMutex);

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
    submitInfo.pCommandBuffers = &commandBuffer;

    Containers::Vector<VkSemaphore> fullSignalSems = signalSems;
    Containers::Vector<uint64_t> fullSignalValues = signalValues;

    fullSignalSems.push_back(m_TimelineSemaphore);
    fullSignalValues.push_back(submitTicket);

    submitInfo.signalSemaphoreCount = static_cast<uint32_t>(fullSignalSems.size());
    submitInfo.pSignalSemaphores = fullSignalSems.data();

    timelineInfo.signalSemaphoreValueCount = static_cast<uint32_t>(fullSignalValues.size());
    timelineInfo.pSignalSemaphoreValues = fullSignalValues.data();

    if (vkQueueSubmit(m_Queue, 1, &submitInfo, VK_NULL_HANDLE) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkQueue::SubmitRaw]: failed to submit raw command buffer!");
    }

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
