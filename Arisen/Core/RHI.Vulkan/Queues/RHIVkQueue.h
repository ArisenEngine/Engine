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

        RHIVkQueue(VkDevice device, VkQueue queue, RHIQueueType type, IRHIDeferredDeletionQueue* deferredDeletionQueue);
        ~RHIVkQueue() noexcept;

        RHIQueueType GetType() const override { return m_Type; }

        // Returns the submitID assigned to this submission.
        RHIGpuTicket Submit(RHICommandBuffer* commandBuffer) override;

        // Fence is owned outside of the command buffer (e.g. per-frame fence in the command buffer pool).
        // RHIVkQueue will NOT destroy or recycle this fence.
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

    private:
        struct InFlightSubmit
        {
            VkFence fence { VK_NULL_HANDLE };
            RHIGpuTicket submitId { 0 };
            bool ownedFence { true };
        };

        VkFence AcquireFence();
        void RecycleFence(VkFence fence);

        VkDevice m_Device { VK_NULL_HANDLE };
        VkQueue m_Queue { VK_NULL_HANDLE };
        RHIQueueType m_Type { RHIQueueType::Graphics };
        IRHIDeferredDeletionQueue* m_DeferredDeletion { nullptr }; // not owned

        std::mutex m_Mutex;
        std::vector<VkFence> m_FreeFences;
        std::deque<InFlightSubmit> m_InFlight;

        std::atomic<RHIGpuTicket> m_NextSubmitId { 1 };
        std::atomic<RHIGpuTicket> m_LastSubmittedSubmitId { 0 };
        std::atomic<RHIGpuTicket> m_CompletedSubmitId { 0 };
    };
}

