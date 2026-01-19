#pragma once

#include <vulkan/vulkan_core.h>

#include "Common/CommandHeaders.h"
#include "RHI/Queues/IRHIQueue.h"
#include "RHI/Utils/RHIDeferredDeletionQueue.h"

#include <atomic>
#include <mutex>
#include "RHI/Utils/RHIResourceRegistry.h"

namespace ArisenEngine::RHI
{
    // Per-queue submit sequencing and GPU completion tracking.
    // Uses timeline semaphores for CPU<->GPU synchronization.
    class RHIVkQueue final : public IRHIQueue
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkQueue)

        RHIVkQueue(VkDevice device, VkQueue queue, RHIQueueType type, IRHIDeferredDeletionQueue* deferredDeletionQueue, RHIResourceRegistry* resourceRegistry);
        ~RHIVkQueue() noexcept;

        RHIQueueType GetType() const override { return m_Type; }

        // Returns the submitID assigned to this submission.
        RHIGpuTicket Submit(RHICommandBuffer* commandBuffer) override;

        // Legacy path: fence argument is ignored when using timeline semaphores.
        RHIGpuTicket SubmitWithFence(RHICommandBuffer* commandBuffer, VkFence fence, bool ownedFence = false);

        // Poll GPU completion and flush deferred deletions up to completed submitID.
        void Update() override;

        RHIGpuTicket GetCompletedTicket() const override
        {
            return m_CompletedSubmitId.load(std::memory_order_acquire);
        }

        RHIGpuTicket GetLastSubmittedTicket() const override
        {
            return m_LastSubmittedSubmitId.load(std::memory_order_acquire);
        }

        void WaitForTicket(RHIGpuTicket ticket) override;

    private:
        void CreateTimelineSemaphore();

        VkDevice m_Device { VK_NULL_HANDLE };
        VkQueue m_Queue { VK_NULL_HANDLE };
        RHIQueueType m_Type { RHIQueueType::Graphics };
        IRHIDeferredDeletionQueue* m_DeferredDeletion { nullptr }; // not owned
        RHIResourceRegistry* m_ResourceRegistry { nullptr }; // not owned

        std::mutex m_Mutex;
        VkSemaphore m_TimelineSemaphore { VK_NULL_HANDLE };

        std::atomic<RHIGpuTicket> m_NextSubmitId { 1 };
        std::atomic<RHIGpuTicket> m_LastSubmittedSubmitId { 0 };
        std::atomic<RHIGpuTicket> m_CompletedSubmitId { 0 };
    };
}

