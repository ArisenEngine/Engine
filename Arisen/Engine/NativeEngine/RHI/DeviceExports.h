#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.RHI/RHI/Core/RHIDevice.h"
#include "../../Core/Core.RHI/RHI/Definitions/DeviceLimits.h"
#include "../../Core/Core.RHI/RHI/Samplers/RHISampler.h"
#include "../../Core/Core.RHI/RHI/Core/RHICommon.h"

#include "RHITypesExports.h"

// Explicitly use void* and PODs to avoid typedef/namespace issues in extern C

/** 
 * @warning [Zero-Stall] DO NOT use this for normal resource management (like OnResize).
 * It will cause a total pipeline stall and degrade performance.
 * Use for engine shutdown or massive state reset only.
 * For resource management, use deferred deletion or fine-grained sync via RHI_Device_WaitQueueTicket.
 */
extern "C" ENGINE_DLL void RHI_Device_WaitIdle(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_GraphicQueueWaitIdle(RHI_DeviceHandle device);

// Moved to HandlesExports: CreateGPUProgram, ReleaseGPUProgram, AttachByteCode
extern "C" ENGINE_DLL void RHI_Device_SetResolution(RHI_DeviceHandle device, unsigned int width, unsigned int height);
extern "C" ENGINE_DLL RHIDeviceLimits RHI_Device_GetDeviceLimits(RHI_DeviceHandle device);

/** @ownership Borrowed - CommandBuffer handle returned to pool after submit by internal logic (caller should release if Pool-based) */
extern "C" ENGINE_DLL unsigned long long RHI_Device_Submit(RHI_DeviceHandle device, RHI_CommandBufferHandle cmd, const struct RHISubmitDescriptor* descriptor);
extern "C" ENGINE_DLL unsigned long long RHI_Device_SubmitCompute(RHI_DeviceHandle device, RHI_CommandBufferHandle cmd, const struct RHISubmitDescriptor* descriptor);
// extern "C" ENGINE_DLL void RHI_Device_Update(RHI_DeviceHandle device); // Removed
extern "C" ENGINE_DLL void RHI_Device_WaitQueueTicket(RHI_DeviceHandle device, unsigned long long ticket);
extern "C" ENGINE_DLL void RHI_Device_WaitComputeQueueTicket(RHI_DeviceHandle device, unsigned long long ticket);

extern "C" ENGINE_DLL void RHI_Device_SetObjectName(RHI_DeviceHandle device, ArisenEngine::RHI::ERHIObjectType type, unsigned long long handle, const char* name);

// Moved to HandlesExports: CreateSampler

