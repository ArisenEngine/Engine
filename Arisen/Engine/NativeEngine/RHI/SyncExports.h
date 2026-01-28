#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.RHI/RHI/Synchronization/RHISemaphore.h"
#include "../../Core/Core.RHI/RHI/Synchronization/RHIFence.h"

#include "RHIHandleExports.h"

extern "C" ENGINE_DLL void RHI_Semaphore_Lock(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore);
extern "C" ENGINE_DLL void RHI_Semaphore_Unlock(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore);

extern "C" ENGINE_DLL void RHI_Fence_Lock(RHI_DeviceHandle device, RHI_FenceHandle fence);
extern "C" ENGINE_DLL void RHI_Fence_Unlock(RHI_DeviceHandle device, RHI_FenceHandle fence);


