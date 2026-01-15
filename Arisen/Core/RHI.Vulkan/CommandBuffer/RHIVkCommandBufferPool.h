#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/CommandBuffer/RHICommandBufferPool.h"
#include <memory>
#include <mutex>


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

        void* GetHandle() override { return m_VkCommandPool; }

        RHICommandBuffer* CreateCommandBuffer() override;
        VkCommandPool AcquireThreadCommandPool();
    private:
        VkCommandPool m_VkCommandPool;
        VkDevice m_VkDevice;
        Containers::Vector<VkCommandPool> m_AllCommandPools;
        Containers::Vector<std::unique_ptr<RHIVkCommandBuffer>> m_OwnedCommandBuffers;
        std::mutex m_PoolsMutex;
        
    };
    
}
