#pragma once
#include <vulkan/vulkan_core.h>
#include "RHIVkCommandBuffer.h"
#include "RHI/CommandBuffer/RHICommandBufferPool.h"


namespace NebulaEngine::RHI
{
    class RHIVkDevice;

    
    class RHIVkCommandBufferPool final : public RHICommandBufferPool
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkCommandBufferPool)
        RHIVkCommandBufferPool(RHIVkDevice* device, UInt32 maxFramesInFlight);
        ~RHIVkCommandBufferPool() noexcept override;

        void* GetHandle() override { return m_VkCommandPool; }

        std::shared_ptr<RHICommandBuffer> CreateCommandBuffer() override;
    private:
        VkCommandPool m_VkCommandPool;
        VkDevice m_VkDevice;
        
    };
    
}
