#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/Synchronization/RHISemaphore.h"
#include "../../Core/Core.Infra/RHI/Synchronization/RHIFence.h"

typedef void* RHI_DeviceHandle;
typedef unsigned long long RHI_SemaphoreHandle;
typedef unsigned long long RHI_FenceHandle;

extern "C" ENGINE_DLL void RHI_Semaphore_Lock(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore);
extern "C" ENGINE_DLL void RHI_Semaphore_Unlock(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore);

extern "C" ENGINE_DLL void RHI_Fence_Lock(RHI_DeviceHandle device, RHI_FenceHandle fence);
extern "C" ENGINE_DLL void RHI_Fence_Unlock(RHI_DeviceHandle device, RHI_FenceHandle fence);


