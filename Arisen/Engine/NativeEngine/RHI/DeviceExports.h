#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIDevice.h"
#include "../../Core/Core.Infra/RHI/DeviceLimits.h"
#include "../../Core/Core.Infra/RHI/Program/RHISampler.h"
#include "../../Core/Core.Infra/RHI/RHICommon.h"

#include "RHIHandleExports.h"

// Explicitly use void* and PODs to avoid typedef/namespace issues in extern C

extern "C" ENGINE_DLL void RHI_Device_WaitIdle(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_GraphicQueueWaitIdle(RHI_DeviceHandle device);

// Moved to HandlesExports: CreateGPUProgram, ReleaseGPUProgram, AttachByteCode
extern "C" ENGINE_DLL void RHI_Device_SetResolution(RHI_DeviceHandle device, unsigned int width, unsigned int height);
extern "C" ENGINE_DLL RHIDeviceLimits RHI_Device_GetDeviceLimits(RHI_DeviceHandle device);
extern "C" ENGINE_DLL unsigned long long RHI_Device_Submit(RHI_DeviceHandle device, RHI_CommandBufferHandle cmd, unsigned int frameIndex);
// extern "C" ENGINE_DLL void RHI_Device_Update(RHI_DeviceHandle device); // Removed
extern "C" ENGINE_DLL void RHI_Device_WaitFrameFence(RHI_DeviceHandle device, unsigned int frameIndex);
extern "C" ENGINE_DLL void RHI_Device_WaitQueueTicket(RHI_DeviceHandle device, unsigned long long ticket);

// Moved to HandlesExports: CreateSampler
