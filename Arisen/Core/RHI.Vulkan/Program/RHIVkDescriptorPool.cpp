#include "RHIVkDescriptorPool.h"

#include "RHIVkGPUPipelineStateObject.h"
#include "../Devices/RHIVkDevice.h"
#include "Logger/Logger.h"
#include "../VkInitializer.h"
#include "RHI/Memory/ImageView.h"

ArisenEngine::RHI::RHIVkDescriptorPool::RHIVkDescriptorPool(RHIVkDevice* device, UInt32 maxFramesInFlight):
m_pDevice(device), m_MaxFramesInFlight(maxFramesInFlight)
{
    
}

ArisenEngine::RHI::RHIVkDescriptorPool::~RHIVkDescriptorPool()
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

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDescriptorPool::AddPool(Containers::Vector<EDescriptorType> types,
    Containers::Vector<UInt32> counts, UInt32 maxSets)
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

bool ArisenEngine::RHI::RHIVkDescriptorPool::ResetPool(UInt32 poolId)
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

void ArisenEngine::RHI::RHIVkDescriptorPool::AllocDescriptorSets(UInt32 poolId, GPUPipelineStateObject* pso)
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

const VkDescriptorImageInfo* GetImageInfos(ArisenEngine::RHI::RHIDescriptorUpdateInfo updateInfo,
    ArisenEngine::Containers::Vector<VkDescriptorImageInfo> results)
{
    results.clear();
    for (int i = 0; i < updateInfo.descriptorCount; ++i)
    {
        auto pImageInfo = updateInfo.pImageInfo + i;
        if (pImageInfo != nullptr)
        {
            results.emplace_back(ArisenEngine::RHI::DescriptorImageInfo(
                static_cast<VkSampler>(pImageInfo->sampler->GetHandle()),
                static_cast<VkImageView>(pImageInfo->imageView->GetView()),
                static_cast<VkImageLayout>(pImageInfo->imageLayout)
                ));
        }
    }

    return results.data();
}

const VkDescriptorBufferInfo* GetBufferInfos(ArisenEngine::RHI::RHIDescriptorUpdateInfo updateInfo,
    ArisenEngine::Containers::Vector<VkDescriptorBufferInfo> results)
{
    results.clear();
    for (int i = 0; i < updateInfo.descriptorCount; ++i)
    {
        auto pBufferInfo = updateInfo.pBufferInfo + i;
        if (pBufferInfo != nullptr)
        {
            results.emplace_back(ArisenEngine::RHI::DescriptorBufferInfo(
                static_cast<VkBuffer>(pBufferInfo->bufferHandle->GetHandle()),
                static_cast<VkDeviceSize>(pBufferInfo->offset),
                static_cast<VkDeviceSize>(pBufferInfo->range)
                ));
        }
    }
    return results.data();
}

const VkBufferView* GetBufferViews(ArisenEngine::RHI::RHIDescriptorUpdateInfo updateInfo,
    ArisenEngine::Containers::Vector<VkBufferView> results)
{
    results.clear();
    for (int i = 0; i < updateInfo.descriptorCount; ++i)
    {
        auto pTexelBufferView = updateInfo.pTexelBufferView + i;
        if (pTexelBufferView != nullptr)
        {
            results.emplace_back(static_cast<VkBufferView>(pTexelBufferView->GetView()));
        }
    }
    return results.data();
}

void ArisenEngine::RHI::RHIVkDescriptorPool::UpdateDescriptorSets(UInt32 poolId, GPUPipelineStateObject* pso,
    UInt32 frameIndex)
{
    ASSERT(m_Pools[poolId] != VK_NULL_HANDLE);
    auto currentIndex = frameIndex % m_MaxFramesInFlight;
    auto descriptorSets = m_DescriptorSets[poolId];
    auto allUpdateInfos = pso->GetAllDescriptorUpdateInfos();
    auto numLayouts = pso->DescriptorSetLayoutCount();
    
    Containers::Vector<VkWriteDescriptorSet> descriptorWrites;
    Containers::Vector<Containers::Vector<VkDescriptorImageInfo>> imageInfos;
    Containers::Vector<Containers::Vector<VkDescriptorBufferInfo>> bufferInfos;
    Containers::Vector<Containers::Vector<VkBufferView>> bufferViews;
    
    for (size_t layoutIndex = 0; layoutIndex < numLayouts; ++layoutIndex)
    {
        auto dstSet = descriptorSets[layoutIndex][currentIndex];
        for (auto& const updateInfoForBinding : allUpdateInfos[layoutIndex])
        {
            for (auto& const updateInfoPerType : updateInfoForBinding.second)
            {
                imageInfos.emplace_back();
                bufferInfos.emplace_back();
                bufferViews.emplace_back();
                auto updateInfo = updateInfoPerType.second;
                auto writeDescriptorSet = WriteDescriptorSet(
                    dstSet, updateInfo.binding, 0, updateInfo.descriptorCount, 
                    static_cast<VkDescriptorType>(updateInfo.type),
                    GetImageInfos(updateInfo, imageInfos.back()),
                    GetBufferInfos(updateInfo, bufferInfos.back()),
                    GetBufferViews(updateInfo, bufferViews.back()));
                descriptorWrites.push_back(writeDescriptorSet);
            }
        }
    }

    vkUpdateDescriptorSets(static_cast<VkDevice>(m_pDevice->GetHandle()),
        descriptorWrites.size(), descriptorWrites.data(),
        0, nullptr);
    
}
