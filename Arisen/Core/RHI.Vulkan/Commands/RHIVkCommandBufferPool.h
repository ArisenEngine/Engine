#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Commands/RHICommandBufferPool.h"
#include <memory>
#include <mutex>
#include <thread>
#include "Threadable/ThreadLocalCache.h"


namespace ArisenEngine::RHI
{
    class RHIVkDevice;
    class RHIVkCommandBuffer;

    
    /**
     * @brief RHIVkCommandBufferPool manages Vulkan command buffers using a two-tier caching system:
     *        1. Thread-local cache (FreeListCache) for zero-lock same-thread recycling.
     *        2. Global map (m_ThreadFreeBuffers) as a "mailbox" for cross-thread recycling.
     * 
     * TODO: Future Performance Optimizations:
     * 1. [Lock-Free Mailbox]: Replace m_ThreadFreeBuffers with a per-thread MPSC lock-free stack 
     *    to eliminate mutex contention during InternalRecycle (cross-thread return).
     * 2. [Avoid Map Lookup]: Use a unique thread index and a fixed-size array instead of 
     *    std::map<std::thread::id, ...> to eliminate hash/lookup overhead in GetCommandBuffer.
     * 3. [Pool Reset]: Implement vkResetCommandPool optimization if command buffer usage 
     *    follows strict frame-based lifecycles.
     * 4. [TLS Cleanup]: Implement a ThreadRegistry to properly delete ThreadLocalFreeList 
     *    objects when threads terminate to avoid minor memory leaks.
     */
    class RHIVkCommandBufferPool final : public RHICommandBufferPool
    {
    private:
        struct ThreadLocalFreeList {
             Containers::Vector<RHICommandBuffer*> freeBuffers;
             Containers::Vector<std::pair<RHIGpuTicket, RHICommandBuffer*>> pendingBuffers;
        };

    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkCommandBufferPool)
        RHIVkCommandBufferPool(RHIVkDevice* device, UInt32 maxFramesInFlight);
        ~RHIVkCommandBufferPool() noexcept override;


        RHICommandBuffer* GetCommandBuffer(UInt32 currentFrameIndex, ECommandBufferLevel level = COMMAND_BUFFER_LEVEL_PRIMARY) override;
        void ReleaseCommandBuffer(UInt32 currentFrameIndex, RHICommandBuffer* commandBuffer) override;

    private:
        void FlushPendingBuffers(ThreadLocalFreeList* tlsList);
        RHICommandBuffer *CreateCommandBuffer(ECommandBufferLevel level) override;
        VkCommandPool AcquireThreadCommandPool();

        void InternalRecycle(RHICommandBuffer* commandBuffer) override;

        VkDevice m_VkDevice;
        Containers::Map<std::thread::id, VkCommandPool> m_ThreadPools;
        Containers::Map<std::thread::id, Containers::Vector<RHICommandBuffer*>> m_ThreadFreeBuffers;
        Containers::Vector<RHICommandBufferHandle> m_OwnedHandles;
        std::mutex m_PoolsMutex;

        friend class RHIVkCommandBuffer;
    };
    
}




