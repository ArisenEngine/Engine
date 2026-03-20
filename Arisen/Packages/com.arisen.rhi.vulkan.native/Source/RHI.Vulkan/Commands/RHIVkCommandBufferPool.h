#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Commands/RHICommandBufferPool.h"
#include <memory>
#include <mutex>
#include <thread>
#include "Threadable/ThreadLocalCache.h"
#include "Threadable/ThreadRegistry.h"
#include "Containers/LockFreeStack.h"
#include "RHI/Queues/RHIQueueType.h"


namespace ArisenEngine::RHI
{
    class RHIVkDevice;
    class RHIVkCommandBuffer;


    /**
     * @brief RHIVkCommandBufferPool manages Vulkan command buffers using a three-tier caching system:
     *        1. Thread-local cache (FreeListCache) for zero-lock same-thread recycling.
     *        2. Lock-free Mailboxes for efficient cross-thread recycling without mutex contention.
     *        3. Global storage for long-term resource management and cleanup.
     */
    class RHIVkCommandBufferPool final : public RHICommandBufferPool
    {
    private:
        struct ThreadSlot
        {
            // Tier 1: Thread-local free list
            Containers::Vector<RHICommandBuffer*> freePrimaryBuffers;
            Containers::Vector<RHICommandBuffer*> freeSecondaryBuffers;
            Containers::Vector<std::pair<RHIGpuTicket, RHICommandBuffer*>> pendingBuffers;

            // Tier 2: Mailbox for cross-thread recycling
            Containers::LockFreeStack<RHICommandBuffer*> mailbox;

            // Resource ownership
            VkCommandPool commandPool = VK_NULL_HANDLE;
            std::atomic<bool> initialized{false};
        };

    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkCommandBufferPool)
        RHIVkCommandBufferPool(RHIVkDevice* device, UInt32 maxFramesInFlight,
                               RHIQueueType queueType = RHIQueueType::Graphics);
        ~RHIVkCommandBufferPool() noexcept override;


        RHICommandBufferHandle GetCommandBuffer(UInt32 currentFrameIndex,
                                                ECommandBufferLevel level = COMMAND_BUFFER_LEVEL_PRIMARY) override;
        void ReleaseCommandBuffer(UInt32 currentFrameIndex, RHICommandBufferHandle handle) override;

    private:
        void FlushPendingBuffers(ThreadSlot& slot);
        void ConsumeMailbox(ThreadSlot& slot);

        RHICommandBufferHandle CreateCommandBuffer(ECommandBufferLevel level) override;
        ThreadSlot& GetCurrentThreadSlot();

        void InternalRecycle(RHICommandBufferHandle handle) override;

        VkDevice m_VkDevice;

        // Fixed-size slots for bounded resource management
        static constexpr size_t MAX_THREADS = ThreadRegistry::MAX_THREADS;
        ThreadSlot m_Slots[MAX_THREADS];

        // Global storage for long-term resource management and cleanup
        Containers::Vector<RHICommandBufferHandle> m_OwnedHandles;

        std::mutex m_PoolsMutex;

        friend class RHIVkCommandBuffer;
    };
}
