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


        RHICommandBuffer* GetCommandBuffer(UInt32 currentFrameIndex) override;
        void ReleaseCommandBuffer(UInt32 currentFrameIndex, RHICommandBuffer* commandBuffer) override;

    private:
        void FlushPendingBuffers(ThreadLocalFreeList* tlsList);
        RHICommandBuffer* CreateCommandBuffer() override;
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




