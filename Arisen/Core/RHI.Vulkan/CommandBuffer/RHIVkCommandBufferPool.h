#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/CommandBuffer/RHICommandBufferPool.h"
#include <memory>
#include <mutex>
#include <thread>


namespace ArisenEngine::RHI
{
    class RHIVkDevice;
    class RHIVkCommandBuffer;

    
    class RHIVkCommandBufferPool final : public RHICommandBufferPool
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkCommandBufferPool)
        RHIVkCommandBufferPool(RHIVkDevice* device, UInt32 maxFramesInFlight);
        ~RHIVkCommandBufferPool() noexcept override;

        void* GetHandle() override { return AcquireThreadCommandPool(); }

        RHICommandBuffer* CreateCommandBuffer() override;
        VkCommandPool AcquireThreadCommandPool();
    private:
        VkDevice m_VkDevice;
        Containers::Map<std::thread::id, VkCommandPool> m_ThreadPools;
        Containers::Vector<std::unique_ptr<RHIVkCommandBuffer>> m_OwnedCommandBuffers;
        std::mutex m_PoolsMutex;
        
    };
    
}
