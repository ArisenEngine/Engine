#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/Devices/Device.h"
#include "../../Core/Core.Infra/RHI/DeviceLimits.h"

typedef void* RHI_DeviceHandle;
typedef void* RHI_CommandBufferPoolHandle;
typedef void* RHI_CommandBufferHandle;

extern "C" ENGINE_DLL void RHI_Device_WaitIdle(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_GraphicQueueWaitIdle(RHI_DeviceHandle device);
extern "C" ENGINE_DLL unsigned int RHI_Device_CreateGPUProgram(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_DestroyGPUProgram(RHI_DeviceHandle device, unsigned int programId);
extern "C" ENGINE_DLL bool RHI_Device_AttachProgramByteCode(RHI_DeviceHandle device, unsigned int programId, const ArisenEngine::RHI::GPUProgramDesc* desc);
extern "C" ENGINE_DLL void RHI_Device_SetResolution(RHI_DeviceHandle device, unsigned int width, unsigned int height);
extern "C" ENGINE_DLL RHIDeviceLimits RHI_Device_GetDeviceLimits(RHI_DeviceHandle device);


