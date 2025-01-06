#include "RHIVkDescriptorPool.h"

#include "RHIVkGPUPipelineStateObject.h"
#include "../Devices/RHIVkDevice.h"
#include "Logger/Logger.h"
#include "../VkInitializer.h"

NebulaEngine::RHI::RHIVkDescriptorPool::RHIVkDescriptorPool(RHIVkDevice* device, u32 maxFramesInFlight):
m_pDevice(device), m_MaxFramesInFlight(maxFramesInFlight)
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

void NebulaEngine::RHI::RHIVkDescriptorPool::AllocDescriptorSets(u32 poolId, GPUPipelineStateObject* pso)
{
    ASSERT(m_Pools[poolId] != VK_NULL_HANDLE);

    if (m_DescriptorSets.size() <= poolId)
    {
        m_DescriptorSets.resize(poolId + 1);
    }
    
    auto numLayouts = pso->DescriptorSetLayoutCount();
    VkDescriptorSetLayout* allLayouts = static_cast<VkDescriptorSetLayout*>(pso->GetDescriptorSetLayouts());
    // 每种 layout 的 descriptorSets
    Containers::Vector<Containers::Vector<VkDescriptorSet>> allDescriptorSets(numLayouts);
    
    for (size_t layoutIndex = 0; layoutIndex < numLayouts; ++layoutIndex)
    {
        Containers::Vector<VkDescriptorSetLayout> layouts(m_MaxFramesInFlight, allLayouts[layoutIndex]);
        VkDescriptorSetAllocateInfo allocInfo
        = DescriptorSetAllocateInfo(m_Pools[poolId], m_MaxFramesInFlight, layouts.data());
        allDescriptorSets[layoutIndex].resize(m_MaxFramesInFlight);
        if (vkAllocateDescriptorSets(static_cast<VkDevice>(m_pDevice->GetHandle()), &allocInfo,
            allDescriptorSets[layoutIndex].data()) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::UpdateDescriptorSet]: failed to allocate descriptor sets!");
        }
    }

    m_DescriptorSets[poolId] = allDescriptorSets;
}

void NebulaEngine::RHI::RHIVkDescriptorPool::UpdateDescriptorSets(u32 poolId, GPUPipelineStateObject* pso,
    u32 frameIndex)
{
    ASSERT(m_Pools[poolId] != VK_NULL_HANDLE);
    auto numLayouts = pso->DescriptorSetLayoutCount();
    VkDescriptorSetLayout* allLayouts = static_cast<VkDescriptorSetLayout*>(pso->GetDescriptorSetLayouts());
    auto currentIndex = frameIndex % m_MaxFramesInFlight;
    auto descriptorSets = m_DescriptorSets[poolId];
    auto vkPso = static_cast<RHIVkGPUPipelineStateObject*>(pso);

    Containers::Vector<VkWriteDescriptorSet> descriptorWrites;
    
    for (size_t layoutIndex = 0; layoutIndex < numLayouts; ++layoutIndex)
    {
        auto layout = allLayouts[layoutIndex];
        auto layoutInfo = vkPso->m_DescriptorSetLayoutCreateInfo[layoutIndex];
        auto dstSet = descriptorSets[layoutIndex][currentIndex];

        for (size_t bindingIndex = 0; bindingIndex < layoutInfo.bindingCount; ++ bindingIndex)
        {
            auto descriptorSetLayoutBinding = layoutInfo.pBindings[bindingIndex]; 
            auto writeDescriptorSet = WriteDescriptorSet(
                dstSet, descriptorSetLayoutBinding.binding, 0, descriptorSetLayoutBinding.descriptorCount, 
                descriptorSetLayoutBinding.descriptorType,
                const VkDescriptorImageInfo* pImageInfo,
                const VkDescriptorBufferInfo* pBufferInfo,
            const VkBufferView* pTexelBufferView);

            
        }
    }

    vkUpdateDescriptorSets(static_cast<VkDevice>(m_pDevice->GetHandle()),
        descriptorWrites.size(), descriptorWrites.data(),
        0, nullptr);
    
}
