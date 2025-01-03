#include "RHIVkDescriptorPool.h"
#include "../Devices/RHIVkDevice.h"
#include "Logger/Logger.h"
#include "../VkInitializer.h"

NebulaEngine::RHI::RHIVkDescriptorPool::RHIVkDescriptorPool(RHIVkDevice* device, u32 maxFramesInFlight):
m_pDevice(device), m_maxFramesInFlight(maxFramesInFlight)
{
    
}

NebulaEngine::RHI::RHIVkDescriptorPool::~RHIVkDescriptorPool()
{
    LOG_DEBUG("[RHIVkDescriptorPool::~RHIVkDescriptorPool] ~RHIVkDescriptorPool");
    m_DescriptorPoolSizes.clear();
    auto device = static_cast<VkDevice>(m_pDevice->GetHandle());
    for (const auto& pool : m_Pools)
    {
        LOG_DEBUG("## Destroy Vulkan Descriptor Pool ##");
        vkDestroyDescriptorPool(device, pool, nullptr);
    }
}

NebulaEngine::u32 NebulaEngine::RHI::RHIVkDescriptorPool::AddPool(Containers::Vector<EDescriptorType> types,
    Containers::Vector<u32> counts, u32 maxSets)
{
    m_DescriptorPoolSizes.resize(counts.size());
    for (int i = 0; i < counts.size(); ++i)
    {
        m_DescriptorPoolSizes[i] = DescriptorPoolSize(types[i], counts[i]);
    }

    VkDescriptorPoolCreateInfo poolInfo = DescriptorPoolCreateInfo(m_DescriptorPoolSizes.size(), m_DescriptorPoolSizes.data(), maxSets);

    VkDescriptorPool pool = VK_NULL_HANDLE;
    if (vkCreateDescriptorPool(static_cast<VkDevice>(m_pDevice->GetHandle()), &poolInfo, nullptr, &pool) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::AddPool] failed to create descriptor pool!");
    }
    
    m_Pools.emplace_back(pool);
    
    return m_Pools.size() - 1;
}

bool NebulaEngine::RHI::RHIVkDescriptorPool::ResetPool(u32 poolId)
{
    ASSERT(poolId < m_Pools.size());
    VkDescriptorPool pool = m_Pools[poolId];
    VkResult result = vkResetDescriptorPool(static_cast<VkDevice>(m_pDevice->GetHandle()), pool, 0);
    if (result != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDescriptorPool::ResetPool] Failed to reset descriptor pool: " + result);
        return false;
    }
    
    return true;
}
