#pragma once
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
        virtual void AllocDescriptorSets(UInt32 poolId, GPUPipelineStateObject* pso) = 0;
        virtual void UpdateDescriptorSets(UInt32 poolId, GPUPipelineStateObject* pso, UInt32 frameIndex) = 0;
        
    };
}
