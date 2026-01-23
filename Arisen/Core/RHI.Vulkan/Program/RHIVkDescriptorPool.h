#pragma once
#include <vulkan/vulkan.h>
#include "RHI/Program/DescriptorPool.h"
#include "RHI/Utils/RHIDeferredDeletionQueue.h"
#include <mutex>
#include "RHI/Program/RHIDescriptorUpdateInfo.h"

namespace ArisenEngine::RHI
{
    class RHIVkDevice;
}

namespace ArisenEngine::RHI
{
    typedef struct RHIVkDescriptorSetsHolder
    {
        VkDescriptorPool descriptorPool {VK_NULL_HANDLE};
        
        Containers::Vector<VkDescriptorPoolSize> poolSizes;
        UInt32 maxSets {0};
        //sets list
        Containers::Vector<std::shared_ptr<RHIDescriptorSet>> sets;
        
    } RHIVkDescriptorSetsHolder;
    
    class RHIVkDescriptorPool final : public RHI::DescriptorPool 
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDescriptorPool)
        RHIVkDescriptorPool(RHIVkDevice* device);
        virtual ~RHIVkDescriptorPool() override;
        /// 
        /// @param types 总的类型数组，包括所有Set
        /// @param counts 所有Set每种类型的总个数
        /// @param maxSets 最多允许Set数
        /// @return 
        UInt32 AddPool(Containers::Vector<EDescriptorType> types, Containers::Vector<UInt32> counts, UInt32 maxSets) override;
        bool ResetPool(UInt32 poolId) override;
        UInt32 AllocDescriptorSet(UInt32 poolId, UInt32 layoutIndex, GPUPipelineStateObject* pso) override;
        RHIDescriptorSet* GetDescriptorSet(UInt32 poolId, UInt32 setIndex) override;
        const Containers::Vector<std::shared_ptr<RHIDescriptorSet>>& GetDescriptorSets(UInt32 poolId) override;
        void UpdateDescriptorSets(UInt32 poolId, GPUPipelineStateObject* pso) override;
        void UpdateDescriptorSet(UInt32 poolId, UInt32 setIndex, GPUPipelineStateObject* pso) override;

        // Called by queue submit to mark that a poolId's descriptor sets were used by a given submit ticket.
        void MarkPoolUsed(UInt32 poolId, RHIQueueType queue, RHIGpuTicket ticket);
        // Internal: called by deferred descriptor-pool destructor to decrement rotation counters.
        void OnDeferredPoolDestroyed(UInt32 poolId);
        // Internal helpers for descriptor updates (moved from global scope to access Device internals via friendship)
        static const VkDescriptorImageInfo* GetImageInfos(RHIVkDevice* device, const RHIDescriptorUpdateInfo& updateInfo, ArisenEngine::Containers::Vector<VkDescriptorImageInfo>& results);
        static const VkDescriptorBufferInfo* GetBufferInfos(RHIVkDevice* device, const RHIDescriptorUpdateInfo& updateInfo, ArisenEngine::Containers::Vector<VkDescriptorBufferInfo>& results);
        static const VkBufferView* GetBufferViews(RHIVkDevice* device, const RHIDescriptorUpdateInfo& updateInfo, ArisenEngine::Containers::Vector<VkBufferView>& results);

    private:
        
        RHIVkDevice* m_pDevice = nullptr;
        // poolId - layoutIndex - Array of sets
        ArisenEngine::Containers::Vector<RHIVkDescriptorSetsHolder> m_DescriptorSetsHolder {};
        ArisenEngine::Containers::Vector<RHIGpuTicket> m_PoolLatestTicket {};
        ArisenEngine::Containers::Vector<UInt32> m_PoolOutstandingRotations {};
        std::mutex m_Mutex;
    };
}
