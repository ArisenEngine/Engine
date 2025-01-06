#pragma once
#include "../../Common/CommandHeaders.h"
#include "../Enums/Pipeline/EDescriptorType.h"

namespace NebulaEngine::RHI
{
    
    class DescriptorPool
    {
    public:
        NO_COPY_NO_MOVE(DescriptorPool)
        DescriptorPool() = default;
        VIRTUAL_DECONSTRUCTOR(DescriptorPool)

        virtual u32 AddPool(Containers::Vector<EDescriptorType> types, Containers::Vector<u32> counts, u32 maxSets) = 0;
        virtual bool ResetPool(u32 poolId) = 0;
        virtual void AddDescriptorSet(u32 poolId) = 0;
        
    };
}
