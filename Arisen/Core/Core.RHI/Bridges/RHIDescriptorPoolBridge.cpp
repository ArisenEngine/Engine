// RHIDescriptorPoolBridge.cpp — extern "C" bridge for RHIDescriptorPool
#include "RHI/Descriptors/RHIDescriptorPool.h"
#include "RHI/Pipeline/RHIPipelineState.h"
#include "Base/BindingMacros.h"

using namespace ArisenEngine::RHI;

ARISEN_BIND_BEGIN_BRIDGE("RHIDescriptorPool", "Core.RHI.dll", "Arisen.Native.RHI")

extern "C" {

RHI_DLL uint32_t RHIDescriptorPool_AddPool(RHIDescriptorPool* pool, int* types, uint32_t* counts, uint32_t typeCount, uint32_t maxSets)
{
    ArisenEngine::Containers::Vector<EDescriptorType> vTypes;
    ArisenEngine::Containers::Vector<ArisenEngine::UInt32> vCounts;
    for (uint32_t i = 0; i < typeCount; ++i)
    {
        vTypes.push_back(static_cast<EDescriptorType>(types[i]));
        vCounts.push_back(static_cast<ArisenEngine::UInt32>(counts[i]));
    }
    return pool->AddPool(std::move(vTypes), std::move(vCounts), static_cast<ArisenEngine::UInt32>(maxSets));
}

RHI_DLL bool RHIDescriptorPool_ResetPool(RHIDescriptorPool* pool, uint32_t poolId)
{
    return pool->ResetPool(poolId);
}

RHI_DLL uint32_t RHIDescriptorPool_AllocDescriptorSet(RHIDescriptorPool* pool, uint32_t poolId, uint32_t layoutIndex, RHIPipelineState* pso)
{
    return pool->AllocDescriptorSet(poolId, layoutIndex, pso);
}

RHI_DLL void RHIDescriptorPool_UpdateDescriptorSet(RHIDescriptorPool* pool, uint32_t poolId, uint32_t setIndex, RHIPipelineState* pso)
{
    pool->UpdateDescriptorSet(poolId, setIndex, pso);
}

} // extern "C"

ARISEN_BIND_END_BRIDGE()
