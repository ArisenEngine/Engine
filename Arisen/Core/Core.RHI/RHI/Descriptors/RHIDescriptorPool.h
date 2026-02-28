#pragma once
#include "RHIDescriptorSet.h"
#include "Base/FoundationMinimal.h"
#include "../Enums/Pipeline/EDescriptorType.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    class RHIPipelineState;
}

namespace ArisenEngine::RHI
{
    class RHI_DLL RHIDescriptorPool
    {
    public:
        NO_COPY_NO_MOVE(RHIDescriptorPool)
        RHIDescriptorPool();
        VIRTUAL_DECONSTRUCTOR(RHIDescriptorPool)

        virtual UInt32 AddPool(Containers::Vector<EDescriptorType> types, Containers::Vector<UInt32> counts,
                               UInt32 maxSets) = 0;
        virtual bool ResetPool(UInt32 poolId) = 0;
        virtual UInt32 AllocDescriptorSet(UInt32 poolId, UInt32 layoutIndex, RHIPipelineState* pso) = 0;
        virtual RHIDescriptorSet* GetDescriptorSet(UInt32 poolId, UInt32 setIndex) = 0;
        virtual const Containers::Vector<std::shared_ptr<RHIDescriptorSet>>& GetDescriptorSets(UInt32 poolId) = 0;
        virtual void UpdateDescriptorSets(UInt32 poolId, RHIPipelineState* pso) = 0;
        virtual void UpdateDescriptorSet(UInt32 poolId, UInt32 setIndex, RHIPipelineState* pso) = 0;
    };
}
