#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/CommandBuffer/RHICommandBufferPool.h"
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

        void FlushPendingBuffers(ThreadLocalFreeList* tlsList);

        RHICommandBuffer* CreateCommandBuffer() override;
        VkCommandPool AcquireThreadCommandPool();
    private:
        void InternalRecycle(RHICommandBuffer* commandBuffer) override;

        VkDevice m_VkDevice;
        Containers::Map<std::thread::id, VkCommandPool> m_ThreadPools;
        Containers::Map<std::thread::id, Containers::Vector<RHICommandBuffer*>> m_ThreadFreeBuffers;
        Containers::Vector<std::unique_ptr<RHIVkCommandBuffer>> m_OwnedCommandBuffers;
        std::mutex m_PoolsMutex;
    };
    
}
