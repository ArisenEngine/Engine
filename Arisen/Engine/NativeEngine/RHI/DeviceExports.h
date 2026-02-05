#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.RHI/RHI/Core/RHIDevice.h"
#include "../../Core/Core.RHI/RHI/Definitions/DeviceLimits.h"
#include "../../Core/Core.RHI/RHI/Samplers/RHISampler.h"
#include "../../Core/Core.RHI/RHI/Core/RHICommon.h"

#include "RHIHandleExports.h"

// Explicitly use void* and PODs to avoid typedef/namespace issues in extern C

extern "C" ENGINE_DLL void RHI_Device_WaitIdle(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_GraphicQueueWaitIdle(RHI_DeviceHandle device);

// Moved to HandlesExports: CreateGPUProgram, ReleaseGPUProgram, AttachByteCode
extern "C" ENGINE_DLL void RHI_Device_SetResolution(RHI_DeviceHandle device, unsigned int width, unsigned int height);
extern "C" ENGINE_DLL RHIDeviceLimits RHI_Device_GetDeviceLimits(RHI_DeviceHandle device);

/** @ownership Borrowed - CommandBuffer handle returned to pool after submit by internal logic (caller should release if Pool-based) */
extern "C" ENGINE_DLL unsigned long long RHI_Device_Submit(RHI_DeviceHandle device, RHI_CommandBufferHandle cmd, unsigned int frameIndex);
// extern "C" ENGINE_DLL void RHI_Device_Update(RHI_DeviceHandle device); // Removed
extern "C" ENGINE_DLL void RHI_Device_WaitQueueTicket(RHI_DeviceHandle device, unsigned long long ticket);

// Moved to HandlesExports: CreateSampler

