#pragma once
#include <vulkan/vulkan.h>
#include "RHI/Program/DescriptorPool.h"

namespace ArisenEngine::RHI
{
    class RHIVkDevice;
}

namespace ArisenEngine::RHI
{
    class RHIVkDescriptorPool final : public RHI::DescriptorPool 
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDescriptorPool)
        RHIVkDescriptorPool(RHIVkDevice* device, UInt32 maxFramesInFlight);
        virtual ~RHIVkDescriptorPool() override;

        UInt32 AddPool(Containers::Vector<EDescriptorType> types, Containers::Vector<UInt32> counts, UInt32 maxSets) override;
        bool ResetPool(UInt32 poolId) override;
        void AllocDescriptorSets(UInt32 poolId, GPUPipelineStateObject* pso) override;
        void UpdateDescriptorSets(UInt32 poolId, GPUPipelineStateObject* pso, UInt32 frameIndex) override;
    private:
        
        RHIVkDevice* m_pDevice = nullptr;
        UInt32 m_MaxFramesInFlight;
        Containers::Vector<VkDescriptorPool> m_Pools {};
        Containers::Vector<VkDescriptorPoolSize> m_DescriptorPoolSizes {};
        Containers::Vector<Containers::Vector<Containers::Vector<VkDescriptorSet>>> m_DescriptorSets {};
    };
}
