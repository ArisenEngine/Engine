#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIDevice.h"
#include "../../Core/Core.Infra/RHI/DeviceLimits.h"
#include "../../Core/Core.Infra/RHI/Program/RHISampler.h"

typedef void* RHI_DeviceHandle;
typedef void* RHI_CommandBufferPoolHandle;
typedef void* RHI_CommandBufferHandle;
typedef void* RHI_SamplerHandle;
typedef void* RHI_GPUProgramHandle;

extern "C" ENGINE_DLL void RHI_Device_WaitIdle(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_GraphicQueueWaitIdle(RHI_DeviceHandle device);
extern "C" ENGINE_DLL RHI_GPUProgramHandle RHI_Device_CreateGPUProgram(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_ReleaseGPUProgram(RHI_DeviceHandle device, RHI_GPUProgramHandle program);
extern "C" ENGINE_DLL bool RHI_Device_AttachProgramByteCode(RHI_DeviceHandle device, RHI_GPUProgramHandle program, const ArisenEngine::RHI::GPUProgramDesc* desc);
extern "C" ENGINE_DLL void RHI_Device_SetResolution(RHI_DeviceHandle device, unsigned int width, unsigned int height);
extern "C" ENGINE_DLL RHIDeviceLimits RHI_Device_GetDeviceLimits(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_Submit(RHI_DeviceHandle device, RHI_CommandBufferHandle cmd, unsigned int frameIndex);
extern "C" ENGINE_DLL void RHI_Device_Update(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_WaitFrameFence(RHI_DeviceHandle device, unsigned int frameIndex);
extern "C" ENGINE_DLL void RHI_Device_WaitQueueTicket(RHI_DeviceHandle device, unsigned long long ticket);
extern "C" ENGINE_DLL RHI_SamplerHandle RHI_Device_CreateSampler(RHI_DeviceHandle device, const ArisenEngine::RHI::RHISamplerDesc* desc);


