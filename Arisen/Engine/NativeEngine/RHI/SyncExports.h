#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/Synchronization/RHISemaphore.h"
#include "../../Core/Core.Infra/RHI/Synchronization/RHIFence.h"

typedef void* RHI_SemaphoreHandle;
typedef void* RHI_FenceHandle;

extern "C" ENGINE_DLL void RHI_Semaphore_Lock(RHI_SemaphoreHandle semaphore);
extern "C" ENGINE_DLL void RHI_Semaphore_Unlock(RHI_SemaphoreHandle semaphore);

extern "C" ENGINE_DLL void RHI_Fence_Lock(RHI_FenceHandle fence);
extern "C" ENGINE_DLL void RHI_Fence_Unlock(RHI_FenceHandle fence);


