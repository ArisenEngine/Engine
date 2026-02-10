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
        struct ThreadLocalFreeList {
             Containers::Vector<RHICommandBuffer*> freeBuffers;
             Containers::Vector<std::pair<RHIGpuTicket, RHICommandBuffer*>> pendingBuffers;
             bool registeredCleanup = false;
        };

    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkCommandBufferPool)
        RHIVkCommandBufferPool(RHIVkDevice* device, UInt32 maxFramesInFlight, RHIQueueType queueType = RHIQueueType::Graphics);
        ~RHIVkCommandBufferPool() noexcept override;


        RHICommandBuffer* GetCommandBuffer(UInt32 currentFrameIndex, ECommandBufferLevel level = COMMAND_BUFFER_LEVEL_PRIMARY) override;
        void ReleaseCommandBuffer(UInt32 currentFrameIndex, RHICommandBuffer* commandBuffer) override;

    private:
        void FlushPendingBuffers(ThreadLocalFreeList* tlsList);
        void ConsumeMailbox(ThreadLocalFreeList* tlsList);

        RHICommandBuffer *CreateCommandBuffer(ECommandBufferLevel level) override;
        VkCommandPool AcquireThreadCommandPool();

        void InternalRecycle(RHICommandBuffer* commandBuffer) override;

        VkDevice m_VkDevice;
        
        // Mailboxes for cross-thread recycling. One per thread index.
        static constexpr size_t MAX_THREADS = 128;
        Containers::LockFreeStack<RHICommandBuffer*> m_Mailboxes[MAX_THREADS];

        // Global pools and handles for cleanup
        Containers::Map<std::thread::id, VkCommandPool> m_ThreadPools;
        Containers::Vector<RHICommandBufferHandle> m_OwnedHandles;
        
        RHIQueueType m_QueueType;
        std::mutex m_PoolsMutex;

        friend class RHIVkCommandBuffer;
    };
    
}




