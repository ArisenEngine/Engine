#pragma once
#include <vulkan/vulkan.h>
#include "RHI/Program/DescriptorPool.h"

namespace NebulaEngine::RHI
{
    class RHIVkDevice;
}

namespace NebulaEngine::RHI
{
    class RHIVkDescriptorPool final : public RHI::DescriptorPool 
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDescriptorPool)
        RHIVkDescriptorPool(RHIVkDevice* device, u32 maxFramesInFlight);
        virtual ~RHIVkDescriptorPool() override;

        u32 AddPool(Containers::Vector<EDescriptorType> types, Containers::Vector<u32> counts, u32 maxSets) override;
        bool ResetPool(u32 poolId) override;
        void AllocDescriptorSets(u32 poolId, GPUPipelineStateObject* pso) override;
        void UpdateDescriptorSets(u32 poolId, GPUPipelineStateObject* pso, u32 frameIndex) override;
    private:
        
        RHIVkDevice* m_pDevice = nullptr;
        u32 m_MaxFramesInFlight;
        Containers::Vector<VkDescriptorPool> m_Pools {};
        Containers::Vector<VkDescriptorPoolSize> m_DescriptorPoolSizes {};
        Containers::Vector<Containers::Vector<Containers::Vector<VkDescriptorSet>>> m_DescriptorSets {};
    };
}
