#include "RHIVkCommandBufferPool.h"

NebulaEngine::RHI::RHIVkCommandBufferPool::RHIVkCommandBufferPool(RHIVkDevice* device): RHICommandBufferPool(device)
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
}

NebulaEngine::RHI::RHIVkCommandBufferPool::~RHIVkCommandBufferPool() noexcept
{
    vkDestroyCommandPool(m_VkDevice, m_VkCommandPool, nullptr);
    LOG_DEBUG("## Destroy Vulkan Command Pool ##");
}
