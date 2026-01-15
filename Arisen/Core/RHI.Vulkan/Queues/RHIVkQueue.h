#pragma once

#include <vulkan/vulkan_core.h>

#include "Common/CommandHeaders.h"
#include "RHI/Queues/IRHIQueue.h"
#include "RHI/Utils/RHIDeferredDeletionQueue.h"

#include <atomic>
#include <deque>
#include <mutex>
#include <vector>

namespace ArisenEngine::RHI
{
    // Per-queue submit sequencing and GPU completion tracking.
    // This is the canonical owner of submit fences (not command buffers).
    class RHIVkQueue final : public IRHIQueue
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkQueue)

        RHIVkQueue(VkDevice device, VkQueue queue, IRHIDeferredDeletionQueue* deferredDeletionQueue);
        ~RHIVkQueue() noexcept;

        RHIQueueType GetType() const override { return RHIQueueType::Graphics; } // current: graphics queue only

        // Returns the submitID assigned to this submission.
        RHIGpuTicket Submit(RHICommandBuffer* commandBuffer) override;

        // Poll GPU completion and flush deferred deletions up to completed submitID.
        void Update() override;

        RHIGpuTicket GetCompletedTicket() const override
        {
            return m_CompletedSubmitId.load(std::memory_order_acquire);
        }

    private:
        struct InFlightSubmit
        {
            VkFence fence { VK_NULL_HANDLE };
            RHIGpuTicket submitId { 0 };
        };

        VkFence AcquireFence();
        void RecycleFence(VkFence fence);

        VkDevice m_Device { VK_NULL_HANDLE };
        VkQueue m_Queue { VK_NULL_HANDLE };
        IRHIDeferredDeletionQueue* m_DeferredDeletion { nullptr }; // not owned

        std::mutex m_Mutex;
        std::vector<VkFence> m_FreeFences;
        std::deque<InFlightSubmit> m_InFlight;

        std::atomic<RHIGpuTicket> m_NextSubmitId { 1 };
        std::atomic<RHIGpuTicket> m_CompletedSubmitId { 0 };
    };
}

