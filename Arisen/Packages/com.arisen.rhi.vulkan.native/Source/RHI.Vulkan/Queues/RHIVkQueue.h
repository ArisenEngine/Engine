#pragma once

#include <vulkan/vulkan_core.h>

#include "Base/FoundationMinimal.h"
#include "RHI/Queues/RHIQueue.h"
#include "RHI/Resources/RHIDeferredDeletionQueue.h"

#include <atomic>
#include <mutex>
#include "RHI/Resources/RHIResourceRegistry.h"

namespace ArisenEngine::RHI
{
    class RHIVkDevice;
    // Per-queue submit sequencing and GPU completion tracking.
    // Uses timeline semaphores for CPU<->GPU synchronization.
    class RHIVkQueue final : public RHIQueue
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkQueue)

        RHIVkQueue(RHIVkDevice* rhiDevice, VkDevice device, VkQueue queue, RHIQueueType type,
                   IRHIDeferredDeletionQueue* deferredDeletionQueue, RHIResourceRegistry* resourceRegistry);
        ~RHIVkQueue() noexcept;

        RHIQueueType GetType() const override { return m_Type; }
        VkQueue GetVkQueue() const { return m_Queue; }

        // Returns the submitID assigned to this submission.
        RHIGpuTicket Submit(RHICommandBufferHandle commandBuffer,
                            const RHISubmitDescriptor* descriptor = nullptr) override;

        // Expose timeline semaphore for cross-queue synchronization
        RHISemaphoreHandle GetTimelineSemaphoreHandle() const { return m_TimelineSemaphoreHandle; }

        // Poll GPU completion and flush deferred deletions up to completed submitID.
        void Update() override;

        RHIGpuTicket GetCompletedTicket() const override
        {
            return m_CompletedSubmitTicket.load(std::memory_order_acquire);
        }

        RHIGpuTicket GetLatestTicket() const override
        {
            return m_LatestTicket.load(std::memory_order_acquire);
        }

        void WaitIdle() override;
        void WaitForTicket(RHIGpuTicket ticket) override;

    private:
        void CreateTimelineSemaphore();

        RHIVkDevice* m_RHIDevice{nullptr};
        VkDevice m_Device{VK_NULL_HANDLE};
        VkQueue m_Queue{VK_NULL_HANDLE};
        RHIQueueType m_Type{RHIQueueType::Graphics};
        IRHIDeferredDeletionQueue* m_DeferredDeletion{nullptr}; // not owned
        RHIResourceRegistry* m_ResourceRegistry{nullptr}; // not owned

        RHISemaphoreHandle m_TimelineSemaphoreHandle;
        VkSemaphore m_TimelineSemaphore{VK_NULL_HANDLE}; // Cache the raw handle internally

        std::mutex m_SubmitMutex;

        std::atomic<RHIGpuTicket> m_LatestTicket{0};
        std::atomic<RHIGpuTicket> m_CompletedSubmitTicket{0};
    };
}
