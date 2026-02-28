// RHICommandBufferPoolBridge.cpp
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "Base/BindingMacros.h"

using namespace ArisenEngine::RHI;

ARISEN_BIND_BEGIN_BRIDGE("RHICommandBufferPool", "Core.RHI.dll", "Arisen.Native.RHI")

extern "C" {
RHI_DLL void RHICommandBufferPool_GetCommandBuffer(RHICommandBufferPool* pool, uint32_t currentFrameIndex, int level,
                                                   uint32_t* outIndex, uint32_t* outGeneration)
{
    RHICommandBufferHandle handle = pool->GetCommandBuffer(currentFrameIndex, static_cast<ECommandBufferLevel>(level));
    *outIndex = handle.index;
    *outGeneration = handle.generation;
}

RHI_DLL void RHICommandBufferPool_ReleaseCommandBuffer(RHICommandBufferPool* pool, uint32_t currentFrameIndex,
                                                       uint32_t index, uint32_t generation)
{
    RHICommandBufferHandle handle;
    handle.index = index;
    handle.generation = generation;
    pool->ReleaseCommandBuffer(currentFrameIndex, handle);
}
} // extern "C"

ARISEN_BIND_END_BRIDGE()
