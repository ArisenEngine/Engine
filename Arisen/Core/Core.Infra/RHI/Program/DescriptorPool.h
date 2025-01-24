#pragma once
#include "RHIDescriptorSet.h"
#include "../../Common/CommandHeaders.h"
#include "../Enums/Pipeline/EDescriptorType.h"

namespace ArisenEngine::RHI
{
    class GPUPipelineStateObject;
}

namespace ArisenEngine::RHI
{
    
    class DescriptorPool
    {
    public:
        NO_COPY_NO_MOVE(DescriptorPool)
        DescriptorPool() = default;
        VIRTUAL_DECONSTRUCTOR(DescriptorPool)

        virtual UInt32 AddPool(Containers::Vector<EDescriptorType> types, Containers::Vector<UInt32> counts, UInt32 maxSets) = 0;
        virtual bool ResetPool(UInt32 poolId) = 0;
        virtual UInt32 AllocDescriptorSet(UInt32 poolId, UInt32 layoutIndex, GPUPipelineStateObject* pso) = 0;
        virtual RHIDescriptorSet* GetDescriptorSet(UInt32 poolId, UInt32 setIndex) = 0;
        virtual const Containers::Vector<std::shared_ptr<RHIDescriptorSet>>& GetDescriptorSets(UInt32 poolId) = 0;
        virtual void UpdateDescriptorSets(UInt32 poolId, GPUPipelineStateObject* pso) = 0;
        virtual void UpdateDescriptorSet(UInt32 poolId, UInt32 setIndex, GPUPipelineStateObject* pso) = 0;
    };
}
