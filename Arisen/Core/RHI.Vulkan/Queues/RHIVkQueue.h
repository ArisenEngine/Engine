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

        RHIVkQueue(RHIVkDevice* rhiDevice, VkDevice device, VkQueue queue, RHIQueueType type, IRHIDeferredDeletionQueue* deferredDeletionQueue, RHIResourceRegistry* resourceRegistry);
        ~RHIVkQueue() noexcept;

        RHIQueueType GetType() const override { return m_Type; }

        // Returns the submitID assigned to this submission.
        RHIGpuTicket Submit(RHICommandBufferHandle commandBuffer, const RHISubmitDescriptor* descriptor = nullptr) override;

        // Legacy path: fence argument is ignored when using timeline semaphores.
        RHIGpuTicket SubmitWithFence(RHICommandBufferHandle commandBuffer, VkFence fence, bool ownedFence = false, 
            const Containers::Vector<VkSemaphore>& extraWaitSems = {}, 
            const Containers::Vector<VkPipelineStageFlags>& extraWaitStages = {}, 
            const Containers::Vector<uint64_t>& extraWaitValues = {},
            const Containers::Vector<VkSemaphore>& extraSignalSems = {},
            const Containers::Vector<uint64_t>& extraSignalValues = {});

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

        void WaitForTicket(RHIGpuTicket ticket) override;

    private:
        void CreateTimelineSemaphore();

        RHIVkDevice* m_RHIDevice { nullptr };
        VkDevice m_Device { VK_NULL_HANDLE };
        VkQueue m_Queue { VK_NULL_HANDLE };
        RHIQueueType m_Type { RHIQueueType::Graphics };
        IRHIDeferredDeletionQueue* m_DeferredDeletion { nullptr }; // not owned
        RHIResourceRegistry* m_ResourceRegistry { nullptr }; // not owned

        VkSemaphore m_TimelineSemaphore { VK_NULL_HANDLE };

        std::atomic<RHIGpuTicket> m_LatestTicket { 0 };
        std::atomic<RHIGpuTicket> m_CompletedSubmitTicket { 0 };
    };
}






