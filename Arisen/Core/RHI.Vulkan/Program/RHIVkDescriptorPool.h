#pragma once
#include <vulkan/vulkan.h>
#include "RHI/Program/DescriptorPool.h"

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
    private:
        
        RHIVkDevice* m_pDevice = nullptr;
        // poolId - layoutIndex - Array of sets
        Containers::Vector<RHIVkDescriptorSetsHolder> m_DescriptorSetsHolder {};
    };
}
