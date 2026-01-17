#include "RHIVkDescriptorPool.h"

#include "RHIVkDescriptorSet.h"
#include "RHIVkGPUPipelineStateObject.h"
#include "../Devices/RHIVkDevice.h"
#include "Logger/Logger.h"
#include "../VkInitializer.h"
#include "RHI/Memory/ImageView.h"
#include <thread>
#include <chrono>
#include <utility>

namespace
{
    struct DeferredVkDescriptorPool
    {
        VkDevice device { VK_NULL_HANDLE };
        VkDescriptorPool pool { VK_NULL_HANDLE };
        ~DeferredVkDescriptorPool()
        {
            if (device && pool) vkDestroyDescriptorPool(device, pool, nullptr);
        }
    };

    struct DeferredVkDescriptorPoolWithCallback
    {
        VkDevice device { VK_NULL_HANDLE };
        VkDescriptorPool pool { VK_NULL_HANDLE };
        ArisenEngine::RHI::RHIVkDescriptorPool* owner { nullptr };
        ArisenEngine::UInt32 poolId { 0 };
        ~DeferredVkDescriptorPoolWithCallback()
        {
            if (device && pool) vkDestroyDescriptorPool(device, pool, nullptr);
            if (owner) owner->OnDeferredPoolDestroyed(poolId);
        }
    };
}

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
    std::lock_guard<std::mutex> lock(m_Mutex);
    RHIVkDescriptorSetsHolder descriptorSetsHolder;
    descriptorSetsHolder.maxSets = maxSets;
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
    m_PoolLastUsedTicket.emplace_back(0);
    m_PoolOutstandingRotations.emplace_back(0);
    
    return m_DescriptorSetsHolder.size() - 1;
}

bool ArisenEngine::RHI::RHIVkDescriptorPool::ResetPool(UInt32 poolId)
{
    // We intentionally avoid holding m_Mutex while calling queue->Update(),
    // because Update() may flush deferred deletions which can call back into this pool.
    std::unique_lock<std::mutex> lock(m_Mutex);
    if (poolId >= m_DescriptorSetsHolder.size())
    {
        LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::ResetPool] poolId out of range: " + std::to_string(poolId));
    }
    auto& holder = m_DescriptorSetsHolder[poolId];
    VkDescriptorPool pool = holder.descriptorPool;
    if (pool == VK_NULL_HANDLE)
    {
        LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::ResetPool] descriptorPool is VK_NULL_HANDLE for poolId: " + std::to_string(poolId));
    }

    // Non-blocking, GPU-safe reset strategy:
    // - If GPU has finished using this poolId (completed >= lastUsed), we can vkResetDescriptorPool immediately.
    // - Otherwise, rotate to a fresh VkDescriptorPool for this poolId and defer-destroy the old pool at lastUsed ticket.
    const auto lastUsed = (poolId < m_PoolLastUsedTicket.size()) ? m_PoolLastUsedTicket[poolId] : 0;
    auto* q = m_pDevice ? m_pDevice->GetQueue(RHIQueueType::Graphics) : nullptr;
    const auto completed = q ? q->GetCompletedTicket() : lastUsed;
    const bool canResetNow = (lastUsed == 0) || (completed >= lastUsed);

    if (!canResetNow)
    {
        // Cap the number of outstanding rotated pools to avoid unbounded growth when GPU is far behind.
        // When the cap is reached, we fall back to a bounded wait (rare).
        const UInt32 outstanding = (poolId < m_PoolOutstandingRotations.size()) ? m_PoolOutstandingRotations[poolId] : 0;
        const UInt32 maxOutstanding = 8; // heuristic; future: tie to maxFramesInFlight

        if (outstanding >= maxOutstanding && q)
        {
            lock.unlock();
            
            // Use hardware wait instead of busy loop
            q->WaitForTicket(lastUsed);

            lock.lock();
            // Re-read holder after waiting.
            pool = holder.descriptorPool;
        }
        else
        {
        // Create a new pool with the same sizes/maxSets for continued allocations this frame.
        VkDescriptorPoolCreateInfo poolInfo =
            DescriptorPoolCreateInfo(
                holder.poolSizes.size(),
                holder.poolSizes.data(),
                holder.maxSets);

        VkDescriptorPool newPool = VK_NULL_HANDLE;
        if (vkCreateDescriptorPool(static_cast<VkDevice>(m_pDevice->GetHandle()),
                &poolInfo, nullptr, &newPool) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::ResetPool] failed to create replacement descriptor pool!");
        }

        // Defer destruction of the old pool at the last-used ticket.
        if (m_pDevice)
        {
            auto* deferred = new DeferredVkDescriptorPoolWithCallback{
                static_cast<VkDevice>(m_pDevice->GetHandle()),
                pool,
                this,
                poolId,
            };
            if (poolId < m_PoolOutstandingRotations.size()) m_PoolOutstandingRotations[poolId] += 1;
            m_pDevice->DeferredDelete(RHIQueueType::Graphics, lastUsed, MakeDeferredDeleteItem(deferred));
        }
        else
        {
            vkDestroyDescriptorPool(static_cast<VkDevice>(m_pDevice->GetHandle()), pool, nullptr);
        }

        holder.descriptorPool = newPool;
        m_PoolLastUsedTicket[poolId] = 0;
        holder.sets.clear();
        LOG_DEBUG("[RHIVkDescriptorPool::ResetPool] Rotated descriptor poolId=" + std::to_string(poolId));
        return true;
        }
    }

    VkResult result = vkResetDescriptorPool(static_cast<VkDevice>(m_pDevice->GetHandle()), holder.descriptorPool, 0);
    if (result != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDescriptorPool::ResetPool] Failed to reset descriptor pool, VkResult: " + std::to_string(static_cast<int>(result)));
        return false;
    }

    m_PoolLastUsedTicket[poolId] = 0;
    holder.sets.clear();
    LOG_DEBUG("[RHIVkDescriptorPool::ResetPool] Reset descriptor pool:" + std::to_string(poolId));
    
    return true;
}

void ArisenEngine::RHI::RHIVkDescriptorPool::OnDeferredPoolDestroyed(UInt32 poolId)
{
    std::lock_guard<std::mutex> lock(m_Mutex);
    if (poolId >= m_PoolOutstandingRotations.size()) return;
    if (m_PoolOutstandingRotations[poolId] > 0) m_PoolOutstandingRotations[poolId] -= 1;
}

void ArisenEngine::RHI::RHIVkDescriptorPool::MarkPoolUsed(UInt32 poolId, RHIQueueType queue, RHIGpuTicket ticket)
{
    std::lock_guard<std::mutex> lock(m_Mutex);
    (void)queue; // current impl is per-device graphics queue
    if (poolId >= m_PoolLastUsedTicket.size()) return;
    if (ticket > m_PoolLastUsedTicket[poolId])
    {
        m_PoolLastUsedTicket[poolId] = ticket;
    }
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDescriptorPool::AllocDescriptorSet(UInt32 poolId, UInt32 layoutIndex, GPUPipelineStateObject* pso)
{
    LOG_DEBUG("[RHIVkDescriptorPool::AllocDescriptorSet] poolId=" + std::to_string(poolId) + ", layoutIndex=" + std::to_string(layoutIndex));
    if (poolId >= m_DescriptorSetsHolder.size())
    {
        LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::AllocDescriptorSet] poolId out of range: " + std::to_string(poolId));
    }
    if (m_DescriptorSetsHolder[poolId].descriptorPool == VK_NULL_HANDLE)
    {
        LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::AllocDescriptorSet] descriptorPool is VK_NULL_HANDLE for poolId: " + std::to_string(poolId));
    }
    if (pso == nullptr)
    {
        LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::AllocDescriptorSet] pso is null");
    }

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
    LOG_DEBUG("[RHIVkDescriptorPool::AllocDescriptorSet] vkAllocateDescriptorSets ok");
    
  
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
        if (pBufferInfo == nullptr)
        {
            LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::GetBufferInfos] null BufferHandle in descriptor update info (binding=" + std::to_string(updateInfo.binding) + ")");
        }

        const VkDeviceSize offset = static_cast<VkDeviceSize>(pBufferInfo->Offset());
        VkDeviceSize range = static_cast<VkDeviceSize>(pBufferInfo->Range());
        if (range == 0)
        {
            // Default to the remaining buffer size when caller didn't set an explicit binding range.
            const VkDeviceSize bufferSize = static_cast<VkDeviceSize>(pBufferInfo->BufferSize());
            if (bufferSize > offset)
            {
                range = bufferSize - offset;
            }
            else
            {
                // Fallback: satisfy validation (range must be > 0 if not VK_WHOLE_SIZE).
                range = VK_WHOLE_SIZE;
            }
        }
        results.emplace_back(ArisenEngine::RHI::DescriptorBufferInfo(
            static_cast<VkBuffer>(pBufferInfo->GetHandle()),
            offset,
            range
            ));
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
    LOG_DEBUG("[RHIVkDescriptorPool::UpdateDescriptorSets] poolId=" + std::to_string(poolId));
    if (poolId >= m_DescriptorSetsHolder.size())
    {
        LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::UpdateDescriptorSets] poolId out of range: " + std::to_string(poolId));
    }
    if (m_DescriptorSetsHolder[poolId].descriptorPool == VK_NULL_HANDLE)
    {
        LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::UpdateDescriptorSets] descriptorPool is VK_NULL_HANDLE for poolId: " + std::to_string(poolId));
    }
    if (pso == nullptr)
    {
        LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::UpdateDescriptorSets] pso is null");
    }
    
    auto descriptorSets = m_DescriptorSetsHolder[poolId].sets;
    Containers::Vector<VkWriteDescriptorSet> descriptorWrites;
    Containers::Vector<Containers::Vector<VkDescriptorImageInfo>> imageInfos;
    Containers::Vector<Containers::Vector<VkDescriptorBufferInfo>> bufferInfos;
    Containers::Vector<Containers::Vector<VkBufferView>> bufferViews;

    RHIVkGPUPipelineStateObject* vkPipelineStateObject = static_cast<RHIVkGPUPipelineStateObject*>(pso);

    // NOTE: keep logging minimal; this runs per-frame in some tests.
    
    for (UInt32 i = 0; i < descriptorSets.size(); ++i)
    {
        auto descriptorSet = descriptorSets[i].get();
        if (descriptorSet == nullptr)
        {
            LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::UpdateDescriptorSets] descriptorSet is null for poolId: " + std::to_string(poolId));
        }
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

                // Validate we have backing arrays for the descriptor type to avoid UB inside vkUpdateDescriptorSets.
                const auto type = updateInfo.type;
                if (type == DESCRIPTOR_TYPE_UNIFORM_BUFFER ||
                    type == DESCRIPTOR_TYPE_STORAGE_BUFFER ||
                    type == DESCRIPTOR_TYPE_UNIFORM_BUFFER_DYNAMIC ||
                    type == DESCRIPTOR_TYPE_STORAGE_BUFFER_DYNAMIC)
                {
                    if (pBufferInfos == nullptr || bufferInfos.back().size() != updateInfo.descriptorCount)
                    {
                        LOG_FATAL_AND_THROW("[RHIVkDescriptorPool::UpdateDescriptorSets] buffer descriptor missing infos: binding=" +
                            std::to_string(updateInfo.binding) + ", count=" + std::to_string(updateInfo.descriptorCount) +
                            ", provided=" + std::to_string(bufferInfos.back().size()));
                    }
                }
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
