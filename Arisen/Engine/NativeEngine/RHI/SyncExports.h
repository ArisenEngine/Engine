#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.RHI/RHI/Sync/RHISemaphore.h"
#include "../../Core/Core.RHI/RHI/Sync/RHIFence.h"

#include "RHITypesExports.h"

extern "C" ENGINE_DLL void RHI_Semaphore_Wait(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore);
extern "C" ENGINE_DLL void RHI_Semaphore_Signal(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore);

// Timeline operations
extern "C" ENGINE_DLL void RHI_Semaphore_WaitValue(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore, unsigned long long value);
extern "C" ENGINE_DLL void RHI_Semaphore_SignalValue(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore, unsigned long long value);
extern "C" ENGINE_DLL unsigned long long RHI_Semaphore_GetValue(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore);

extern "C" ENGINE_DLL void RHI_Fence_Lock(RHI_DeviceHandle device, RHI_FenceHandle fence);
extern "C" ENGINE_DLL void RHI_Fence_Unlock(RHI_DeviceHandle device, RHI_FenceHandle fence);



