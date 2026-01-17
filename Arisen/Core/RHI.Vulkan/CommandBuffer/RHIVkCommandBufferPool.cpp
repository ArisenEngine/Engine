#include "RHIVkCommandBufferPool.h"

#include "RHIVkCommandBuffer.h"
#include "../Devices/RHIVkDevice.h"
#include "../Surfaces/RHIVkSurface.h"

ArisenEngine::RHI::RHIVkCommandBufferPool::RHIVkCommandBufferPool(RHIVkDevice* device, UInt32 maxFramesInFlight)
: RHICommandBufferPool(device, maxFramesInFlight)
{
    m_VkDevice = static_cast<VkDevice>(device->GetHandle());

    VkQueueFamilyIndices queueFamilyIndices = static_cast<RHIVkSurface*>(device->GetSurface())->GetQueueFamilyIndices();

    VkCommandPoolCreateInfo poolInfo{};
    poolInfo.sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO;
    poolInfo.flags = VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT | VK_COMMAND_POOL_CREATE_TRANSIENT_BIT;
    poolInfo.queueFamilyIndex = queueFamilyIndices.graphicsFamily.value();

    if (vkCreateCommandPool(m_VkDevice, &poolInfo, nullptr, &m_VkCommandPool) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkCommandBufferPool::RHIVkCommandBufferPool]: failed to create command pool!");
    }
    
    (void)maxFramesInFlight;
}

ArisenEngine::RHI::RHIVkCommandBufferPool::~RHIVkCommandBufferPool() noexcept
{
    LOG_DEBUG("[RHIVkCommandBufferPool::~RHIVkCommandBufferPool]: ~RHIVkCommandBufferPool");
    m_CommandBuffers.clear();
    m_OwnedCommandBuffers.clear();
    {
        std::lock_guard<std::mutex> lock(m_PoolsMutex);
        for (auto pool : m_AllCommandPools)
        {
            vkDestroyCommandPool(m_VkDevice, pool, nullptr);
        }
        m_AllCommandPools.clear();
    }
    vkDestroyCommandPool(m_VkDevice, m_VkCommandPool, nullptr);
    LOG_DEBUG("## Destroy Vulkan Command Pool ##");
}

ArisenEngine::RHI::RHICommandBuffer* ArisenEngine::RHI::RHIVkCommandBufferPool::CreateCommandBuffer()
{
    auto* vkDevice = dynamic_cast<RHIVkDevice*>(m_Device);
    ASSERT(vkDevice != nullptr);
    m_OwnedCommandBuffers.emplace_back(std::make_unique<RHIVkCommandBuffer>(vkDevice, this));
    return m_OwnedCommandBuffers.back().get();
}

VkCommandPool ArisenEngine::RHI::RHIVkCommandBufferPool::AcquireThreadCommandPool()
{
    // For now, return the shared pool. Future: create per-thread pools.
    // If called from multiple threads, we can expand to thread-local pools.
    return m_VkCommandPool;
}
