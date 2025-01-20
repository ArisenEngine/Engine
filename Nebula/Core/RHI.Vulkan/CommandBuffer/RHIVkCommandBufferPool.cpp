#include "RHIVkCommandBufferPool.h"

#include "../Synchronization/RHIVkFence.h"

NebulaEngine::RHI::RHIVkCommandBufferPool::RHIVkCommandBufferPool(RHIVkDevice* device, UInt32 maxFramesInFlight)
: RHICommandBufferPool(device, maxFramesInFlight)
{
    m_VkDevice = static_cast<VkDevice>(device->GetHandle());

    VkQueueFamilyIndices queueFamilyIndices = static_cast<RHIVkSurface*>(device->GetSurface())->GetQueueFamilyIndices();

    VkCommandPoolCreateInfo poolInfo{};
    poolInfo.sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO;
    poolInfo.flags = VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT;
    poolInfo.queueFamilyIndex = queueFamilyIndices.graphicsFamily.value();

    if (vkCreateCommandPool(m_VkDevice, &poolInfo, nullptr, &m_VkCommandPool) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkCommandBufferPool::RHIVkCommandBufferPool]: failed to create command pool!");
    }
    
    m_Fences.resize(maxFramesInFlight);
    for (int i = 0; i < m_MaxFramesInFlight; ++i)
    {
        m_Fences[i] = std::make_unique<RHIVkFence>(m_VkDevice);
    }
}

NebulaEngine::RHI::RHIVkCommandBufferPool::~RHIVkCommandBufferPool() noexcept
{
    LOG_DEBUG("[RHIVkCommandBufferPool::~RHIVkCommandBufferPool]: ~RHIVkCommandBufferPool");
    m_CommandBuffers.clear();
    vkDestroyCommandPool(m_VkDevice, m_VkCommandPool, nullptr);
    LOG_DEBUG("## Destroy Vulkan Command Pool ##");
}

std::shared_ptr<NebulaEngine::RHI::RHICommandBuffer> NebulaEngine::RHI::RHIVkCommandBufferPool::CreateCommandBuffer()
{
    return std::make_shared<RHIVkCommandBuffer>(dynamic_cast<RHIVkDevice*>(m_Device), this);
}
