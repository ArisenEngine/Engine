#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIDevice.h"
#include "../../Core/Core.Infra/RHI/DeviceLimits.h"
#include "../../Core/Core.Infra/RHI/Program/RHISampler.h"
#include "../../Core/Core.Infra/RHI/RHICommon.h"

#include "RHIHandleExports.h"

// Explicitly use void* and PODs to avoid typedef/namespace issues in extern C

extern "C" ENGINE_DLL void RHI_Device_WaitIdle(void* device);
extern "C" ENGINE_DLL void RHI_Device_GraphicQueueWaitIdle(void* device);

// Moved to HandlesExports: CreateGPUProgram, ReleaseGPUProgram, AttachByteCode
extern "C" ENGINE_DLL void RHI_Device_SetResolution(void* device, unsigned int width, unsigned int height);
extern "C" ENGINE_DLL RHIDeviceLimits RHI_Device_GetDeviceLimits(void* device);
extern "C" ENGINE_DLL unsigned long long RHI_Device_Submit(void* device, void* cmd, unsigned int frameIndex);
// extern "C" ENGINE_DLL void RHI_Device_Update(RHI_DeviceHandle device); // Removed
extern "C" ENGINE_DLL void RHI_Device_WaitFrameFence(void* device, unsigned int frameIndex);
extern "C" ENGINE_DLL void RHI_Device_WaitQueueTicket(void* device, unsigned long long ticket);

// Moved to HandlesExports: CreateSampler
