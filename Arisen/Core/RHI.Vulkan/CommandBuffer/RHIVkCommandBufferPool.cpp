#include "RHIVkCommandBufferPool.h"

#include "RHIVkCommandBuffer.h"
#include "../Devices/RHIVkDevice.h"
#include "../Surfaces/RHIVkSurface.h"

ArisenEngine::RHI::RHIVkCommandBufferPool::RHIVkCommandBufferPool(RHIVkDevice* device, UInt32 maxFramesInFlight)
: RHICommandBufferPool(device, maxFramesInFlight)
{
    m_VkDevice = static_cast<VkDevice>(device->GetHandle());
    
    (void)maxFramesInFlight;
}

ArisenEngine::RHI::RHIVkCommandBufferPool::~RHIVkCommandBufferPool() noexcept
{
    LOG_DEBUG("[RHIVkCommandBufferPool::~RHIVkCommandBufferPool]: ~RHIVkCommandBufferPool");
    m_CommandBuffers.clear();
    m_OwnedCommandBuffers.clear();
    {
        std::lock_guard<std::mutex> lock(m_PoolsMutex);
        for (auto& [threadId, pool] : m_ThreadPools)
        {
            if (pool != VK_NULL_HANDLE)
            {
                vkDestroyCommandPool(m_VkDevice, pool, nullptr);
            }
        }
        m_ThreadPools.clear();
    }
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
    std::thread::id threadId = std::this_thread::get_id();

    std::lock_guard<std::mutex> lock(m_PoolsMutex);
    if (m_ThreadPools.find(threadId) != m_ThreadPools.end())
    {
        return m_ThreadPools[threadId];
    }

    VkQueueFamilyIndices queueFamilyIndices = static_cast<RHIVkSurface*>(m_Device->GetSurface())->GetQueueFamilyIndices();

    VkCommandPoolCreateInfo poolInfo{};
    poolInfo.sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO;
    poolInfo.flags = VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT | VK_COMMAND_POOL_CREATE_TRANSIENT_BIT;
    poolInfo.queueFamilyIndex = queueFamilyIndices.graphicsFamily.value();

    VkCommandPool pool;
    if (vkCreateCommandPool(m_VkDevice, &poolInfo, nullptr, &pool) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkCommandBufferPool::AcquireThreadCommandPool]: failed to create command pool for thread!");
    }

    m_ThreadPools[threadId] = pool;
    return pool;
}
