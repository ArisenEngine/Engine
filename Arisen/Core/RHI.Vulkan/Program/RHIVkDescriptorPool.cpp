#include "RHIVkDescriptorPool.h"

#include "RHIVkDescriptorSet.h"
#include "RHIVkGPUPipelineStateObject.h"
#include "../Devices/RHIVkDevice.h"
#include "Logger/Logger.h"
#include "../VkInitializer.h"
#include "RHI/Memory/ImageView.h"

ArisenEngine::RHI::RHIVkDescriptorPool::RHIVkDescriptorPool(RHIVkDevice* device):
m_pDevice(device)
{
    
}

ArisenEngine::RHI::RHIVkDescriptorPool::~RHIVkDescriptorPool()
{
    LOG_DEBUG("[RHIVkDescriptorPool::~RHIVkDescriptorPool] ~RHIVkDescriptorPool");
    auto device = static_cast<VkDevice>(m_pDevice->GetHandle());
    for (const auto& holder : m_DescriptorSetsHolder)
    {
        LOG_DEBUG("## Destroy Vulkan Descriptor Pool ##");
        vkDestroyDescriptorPool(device, holder.descriptorPool, nullptr);
    }

    m_DescriptorSetsHolder.clear();
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDescriptorPool::AddPool(Containers::Vector<EDescriptorType> types,
    Containers::Vector<UInt32> counts, UInt32 maxSets)
{
    RHIVkDescriptorSetsHolder descriptorSetsHolder;
    for (int i = 0; i < counts.size(); ++i)
    {
        descriptorSetsHolder.poolSizes.emplace_back(DescriptorPoolSize(types[i], counts[i]));
    }
    
    VkDescriptorPoolCreateInfo poolInfo =
        DescriptorPoolCreateInfo(
            descriptorSetsHolder.poolSizes.size(),
            descriptorSetsHolder.poolSizes.data(), maxSets);
    
    if (vkCreateDescriptorPool(static_cast<VkDevice>(m_pDevice->GetHandle()),
        &poolInfo, nullptr, &descriptorSetsHolder.descriptorPool) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::AddPool] failed to create descriptor pool!");
    }

    m_DescriptorSetsHolder.emplace_back(descriptorSetsHolder);
    
    return m_DescriptorSetsHolder.size() - 1;
}

bool ArisenEngine::RHI::RHIVkDescriptorPool::ResetPool(UInt32 poolId)
{
    ASSERT(poolId < m_DescriptorSetsHolder.size());
    VkDescriptorPool pool = m_DescriptorSetsHolder[poolId].descriptorPool;
    VkResult result = vkResetDescriptorPool(static_cast<VkDevice>(m_pDevice->GetHandle()), pool, 0);
    if (result != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDescriptorPool::ResetPool] Failed to reset descriptor pool: " + result);
        return false;
    }

    m_DescriptorSetsHolder[poolId].sets.clear();
    LOG_DEBUG("[RHIVkDescriptorPool::ResetPool] Reset descriptor pool:" + std::to_string(poolId));
    
    return true;
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDescriptorPool::AllocDescriptorSet(UInt32 poolId, UInt32 layoutIndex, GPUPipelineStateObject* pso)
{
    ASSERT(poolId < m_DescriptorSetsHolder.size());
    ASSERT(m_DescriptorSetsHolder[poolId].descriptorPool != VK_NULL_HANDLE);

    RHIVkGPUPipelineStateObject* vkPipelineStateObject = static_cast<RHIVkGPUPipelineStateObject*>(pso);
    VkDescriptorSetLayout descriptorSetLayout = vkPipelineStateObject->GetVkDescriptorSetLayout(layoutIndex);
    VkDescriptorSetAllocateInfo descriptorSetAllocateInfo = DescriptorSetAllocateInfo(
    m_DescriptorSetsHolder[poolId].descriptorPool,
    1,
    &descriptorSetLayout
        );
    VkDescriptorSet descriptorSet;
    if (vkAllocateDescriptorSets(static_cast<VkDevice>(m_pDevice->GetHandle()),
        &descriptorSetAllocateInfo, &descriptorSet) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::AllocDescriptorSet] failed to allocate descriptor sets!");
    }
    
  
        m_DescriptorSetsHolder[poolId].sets.emplace_back(
            std::make_shared<RHIVkDescriptorSet>(
this, layoutIndex, descriptorSet
));
    
    return m_DescriptorSetsHolder[poolId].sets.size() - 1;
}

ArisenEngine::RHI::RHIDescriptorSet* ArisenEngine::RHI::RHIVkDescriptorPool::GetDescriptorSet(UInt32 poolId,
    UInt32 setIndex)
{
    ASSERT(poolId < m_DescriptorSetsHolder.size());
    ASSERT(setIndex < m_DescriptorSetsHolder[poolId].sets.size());

    return m_DescriptorSetsHolder[poolId].sets[setIndex].get();
}

const ArisenEngine::Containers::Vector<std::shared_ptr<ArisenEngine::RHI::RHIDescriptorSet>>&
    ArisenEngine::RHI::RHIVkDescriptorPool::
GetDescriptorSets(UInt32 poolId)
{
    ASSERT(poolId < m_DescriptorSetsHolder.size());
    return m_DescriptorSetsHolder[poolId].sets;
}

const VkDescriptorImageInfo* GetImageInfos(ArisenEngine::RHI::RHIDescriptorUpdateInfo& updateInfo,
                                           ArisenEngine::Containers::Vector<VkDescriptorImageInfo>& results)
{
    if (updateInfo.imageInfo.size() <= 0)
    {
        return nullptr;
    }
    
    results.clear();
    for (int i = 0; i < updateInfo.imageInfo.size(); ++i)
    {
        auto pImageInfo = updateInfo.imageInfo[i];
        results.emplace_back(ArisenEngine::RHI::DescriptorImageInfo(
                static_cast<VkSampler>(pImageInfo.sampler->GetHandle()),
                static_cast<VkImageView>(pImageInfo.imageView->GetView()),
                static_cast<VkImageLayout>(pImageInfo.imageLayout)
                ));
    }

    return results.data();
}

const VkDescriptorBufferInfo* GetBufferInfos(ArisenEngine::RHI::RHIDescriptorUpdateInfo& updateInfo,
    ArisenEngine::Containers::Vector<VkDescriptorBufferInfo>& results)
{
    if (updateInfo.bufferHaneles.size() <= 0)
    {
        return nullptr;
    }
    
    results.clear();
    for (int i = 0; i < updateInfo.bufferHaneles.size(); ++i)
    {
        auto pBufferInfo = updateInfo.bufferHaneles[i];
        if (pBufferInfo != nullptr)
        {
            results.emplace_back(ArisenEngine::RHI::DescriptorBufferInfo(
                static_cast<VkBuffer>(pBufferInfo->GetHandle()),
                static_cast<VkDeviceSize>(pBufferInfo->Offset()),
                static_cast<VkDeviceSize>(pBufferInfo->Range())
                ));
        }
    }
    return results.data();
}

const VkBufferView* GetBufferViews(ArisenEngine::RHI::RHIDescriptorUpdateInfo& updateInfo,
    ArisenEngine::Containers::Vector<VkBufferView>& results)
{
    if (updateInfo.texelBufferViews.size() <= 0)
    {
        return nullptr;
    }
    
    results.clear();
    for (int i = 0; i < updateInfo.texelBufferViews.size(); ++i)
    {
        auto bufferView = updateInfo.texelBufferViews[i];
        results.emplace_back(static_cast<VkBufferView>(bufferView->GetView()));
    }
    return results.data();
}

void ArisenEngine::RHI::RHIVkDescriptorPool::UpdateDescriptorSets(UInt32 poolId, GPUPipelineStateObject* pso)
{
    ASSERT(poolId < m_DescriptorSetsHolder.size());
    ASSERT(m_DescriptorSetsHolder[poolId].descriptorPool != VK_NULL_HANDLE);
    
    auto descriptorSets = m_DescriptorSetsHolder[poolId].sets;
    Containers::Vector<VkWriteDescriptorSet> descriptorWrites;
    Containers::Vector<Containers::Vector<VkDescriptorImageInfo>> imageInfos;
    Containers::Vector<Containers::Vector<VkDescriptorBufferInfo>> bufferInfos;
    Containers::Vector<Containers::Vector<VkBufferView>> bufferViews;

    RHIVkGPUPipelineStateObject* vkPipelineStateObject = static_cast<RHIVkGPUPipelineStateObject*>(pso);
    
    for (UInt32 i = 0; i < descriptorSets.size(); ++i)
    {
        auto descriptorSet = descriptorSets[i].get();
        VkDescriptorSet dstSet = static_cast<VkDescriptorSet>(descriptorSet->GetHandle());
        UInt32 layoutIndex = descriptorSet->GetLayoutIndex();
        auto updateInfosForAllBindings = vkPipelineStateObject->GetDescriptorUpdateInfos(layoutIndex);
        for (const auto& updateInfoForAllTypePair : updateInfosForAllBindings)
        {
            auto updateInfoForAllType = updateInfoForAllTypePair.second;
            for (const auto& updateInfoPair : updateInfoForAllType)
            {
                imageInfos.emplace_back();
                bufferInfos.emplace_back();
                bufferViews.emplace_back();
                
                auto updateInfo = updateInfoPair.second;
                auto pImageInfos = GetImageInfos(updateInfo, imageInfos.back());
                auto pBufferInfos = GetBufferInfos(updateInfo, bufferInfos.back());
                auto pBufferViews = GetBufferViews(updateInfo, bufferViews.back());
                auto writeDescriptorSet = WriteDescriptorSet(
                   dstSet, updateInfo.binding, 0, updateInfo.descriptorCount, 
                   static_cast<VkDescriptorType>(updateInfo.type),
                   // TODO: add type validation to figure out whether it can be nullptr
                   pImageInfos,
                   pBufferInfos,
                   pBufferViews);
                
                descriptorWrites.push_back(writeDescriptorSet);
            }
        }
    }
    
    vkUpdateDescriptorSets(static_cast<VkDevice>(m_pDevice->GetHandle()),
        descriptorWrites.size(), descriptorWrites.data(),
        0, nullptr);
    
}

void ArisenEngine::RHI::RHIVkDescriptorPool::UpdateDescriptorSet(UInt32 poolId, UInt32 setIndex,
    GPUPipelineStateObject* pso)
{
    // TODO: 
}
