#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIDevice.h"
#include "../../Core/Core.Infra/RHI/Handles/RHIHandle.h"

#include "RHIHandleExports.h"
namespace ArisenEngine { namespace RHI { class ImageView; } }

// BufferHandle lifecycle & ops
// BufferHandle lifecycle & ops
extern "C" ENGINE_DLL RHI_BufferHandle RHI_Device_CreateBuffer(RHI_DeviceHandle device, const ArisenEngine::RHI::BufferDescriptor* desc, const char* name);
extern "C" ENGINE_DLL void RHI_Device_ReleaseBuffer(RHI_DeviceHandle device, RHI_BufferHandle buffer);

extern "C" ENGINE_DLL void RHI_Buffer_MemoryCopy(RHI_DeviceHandle device, RHI_BufferHandle buffer, const void* src, unsigned int offset);
extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Size(RHI_DeviceHandle device, RHI_BufferHandle buffer);
extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Offset(RHI_DeviceHandle device, RHI_BufferHandle buffer);
extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Range(RHI_DeviceHandle device, RHI_BufferHandle buffer);

// ImageHandle lifecycle & ops
// ImageHandle lifecycle & ops
extern "C" ENGINE_DLL RHI_ImageHandle RHI_Device_CreateImage(RHI_DeviceHandle device, const ArisenEngine::RHI::ImageDescriptor* desc, const char* name);
extern "C" ENGINE_DLL void RHI_Device_ReleaseImage(RHI_DeviceHandle device, RHI_ImageHandle image);

extern "C" ENGINE_DLL RHI_ImageViewHandle RHI_Image_AddImageView(RHI_DeviceHandle device, RHI_ImageHandle image, const ArisenEngine::RHI::ImageViewDesc* desc);
extern "C" ENGINE_DLL ArisenEngine::RHI::EFormat RHI_ImageView_GetFormat(RHI_DeviceHandle device, RHI_ImageViewHandle view);
extern "C" ENGINE_DLL unsigned int RHI_ImageView_GetWidth(RHI_DeviceHandle device, RHI_ImageViewHandle view);
extern "C" ENGINE_DLL unsigned int RHI_ImageView_GetHeight(RHI_DeviceHandle device, RHI_ImageViewHandle view);

extern "C" ENGINE_DLL RHI_ImageViewHandle RHI_Image_GetView(RHI_DeviceHandle device, RHI_ImageHandle image);

// Sampler ops
extern "C" ENGINE_DLL RHI_SamplerHandle RHI_Device_CreateSampler(RHI_DeviceHandle device, const ArisenEngine::RHI::RHISamplerDesc* desc);
extern "C" ENGINE_DLL void RHI_Device_ReleaseSampler(RHI_DeviceHandle device, RHI_SamplerHandle sampler);

// GPUProgram ops
extern "C" ENGINE_DLL RHI_GPUProgramHandle RHI_Device_CreateGPUProgram(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_ReleaseGPUProgram(RHI_DeviceHandle device, RHI_GPUProgramHandle program);
extern "C" ENGINE_DLL bool RHI_Device_AttachProgramByteCode(RHI_DeviceHandle device, RHI_GPUProgramHandle program, const ArisenEngine::RHI::GPUProgramDesc* desc);

// RenderPass ops
extern "C" ENGINE_DLL RHI_RenderPassHandle RHI_Device_CreateRenderPass(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_ReleaseRenderPass(RHI_DeviceHandle device, RHI_RenderPassHandle rp);

