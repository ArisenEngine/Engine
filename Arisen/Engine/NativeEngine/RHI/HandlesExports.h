#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIDevice.h"
#include "../../Core/Core.Infra/RHI/Handles/RHIHandle.h"

#include "RHIHandleExports.h"
namespace ArisenEngine { namespace RHI { class ImageView; } }

// BufferHandle lifecycle & ops
// BufferHandle lifecycle & ops
extern "C" ENGINE_DLL RHI_BufferHandle RHI_Device_GetBufferHandle(RHI_DeviceHandle device, const char* name);
extern "C" ENGINE_DLL void RHI_Device_ReleaseBufferHandle(RHI_DeviceHandle device, RHI_BufferHandle buffer);
extern "C" ENGINE_DLL bool RHI_Buffer_Alloc(RHI_DeviceHandle device, RHI_BufferHandle buffer, const ArisenEngine::RHI::BufferDescriptor* desc);
extern "C" ENGINE_DLL bool RHI_Buffer_AllocDeviceMemory(RHI_DeviceHandle device, RHI_BufferHandle buffer, unsigned int memoryPropertiesBits);
extern "C" ENGINE_DLL void RHI_Buffer_Free(RHI_DeviceHandle device, RHI_BufferHandle buffer);
extern "C" ENGINE_DLL void RHI_Buffer_MemoryCopy(RHI_DeviceHandle device, RHI_BufferHandle buffer, const void* src, unsigned int offset);
extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Size(RHI_DeviceHandle device, RHI_BufferHandle buffer);
extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Offset(RHI_DeviceHandle device, RHI_BufferHandle buffer);
extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Range(RHI_DeviceHandle device, RHI_BufferHandle buffer);

// ImageHandle lifecycle & ops
// ImageHandle lifecycle & ops
extern "C" ENGINE_DLL RHI_ImageHandle RHI_Device_GetImageHandle(RHI_DeviceHandle device, const char* name);
extern "C" ENGINE_DLL void RHI_Device_ReleaseImageHandle(RHI_DeviceHandle device, RHI_ImageHandle image);
extern "C" ENGINE_DLL void RHI_Image_Alloc(RHI_DeviceHandle device, RHI_ImageHandle image, const ArisenEngine::RHI::ImageDescriptor* desc);
extern "C" ENGINE_DLL bool RHI_Image_AllocDeviceMemory(RHI_DeviceHandle device, RHI_ImageHandle image, unsigned int memoryPropertiesBits);
extern "C" ENGINE_DLL void RHI_Image_Free(RHI_DeviceHandle device, RHI_ImageHandle image);
extern "C" ENGINE_DLL RHI_ImageViewHandle RHI_Image_AddImageView(RHI_DeviceHandle device, RHI_ImageHandle image, const ArisenEngine::RHI::ImageViewDesc* desc);
extern "C" ENGINE_DLL ArisenEngine::RHI::EFormat RHI_ImageView_GetFormat(RHI_DeviceHandle device, RHI_ImageViewHandle view);
extern "C" ENGINE_DLL unsigned int RHI_ImageView_GetWidth(RHI_DeviceHandle device, RHI_ImageViewHandle view);
extern "C" ENGINE_DLL unsigned int RHI_ImageView_GetHeight(RHI_DeviceHandle device, RHI_ImageViewHandle view);

extern "C" ENGINE_DLL RHI_ImageViewHandle RHI_Image_GetView(RHI_DeviceHandle device, RHI_ImageHandle image);
