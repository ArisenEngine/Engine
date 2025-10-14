#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/Devices/Device.h"
#include "../../Core/Core.Infra/RHI/Handles/BufferHandle.h"
#include "../../Core/Core.Infra/RHI/Handles/ImageHandle.h"
#include "../../Core/Core.Infra/RHI/Memory/MemoryView.h"

typedef void* RHI_DeviceHandle;
typedef void* RHI_BufferHandle;
typedef void* RHI_ImageHandle;

// BufferHandle lifecycle & ops
extern "C" ENGINE_DLL RHI_BufferHandle RHI_Device_GetBufferHandle(RHI_DeviceHandle device, const char* name);
extern "C" ENGINE_DLL void RHI_Device_ReleaseBufferHandle(RHI_DeviceHandle device, RHI_BufferHandle buffer);
extern "C" ENGINE_DLL bool RHI_Buffer_Alloc(RHI_BufferHandle buffer, const ArisenEngine::RHI::BufferDescriptor* desc);
extern "C" ENGINE_DLL bool RHI_Buffer_AllocDeviceMemory(RHI_BufferHandle buffer, unsigned int memoryPropertiesBits);
extern "C" ENGINE_DLL void RHI_Buffer_Free(RHI_BufferHandle buffer);
extern "C" ENGINE_DLL void RHI_Buffer_MemoryCopy(RHI_BufferHandle buffer, const void* src, unsigned int offset);
extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Size(RHI_BufferHandle buffer);
extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Offset(RHI_BufferHandle buffer);
extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Range(RHI_BufferHandle buffer);

// ImageHandle lifecycle & ops
extern "C" ENGINE_DLL RHI_ImageHandle RHI_Device_GetImageHandle(RHI_DeviceHandle device, const char* name);
extern "C" ENGINE_DLL void RHI_Device_ReleaseImageHandle(RHI_DeviceHandle device, RHI_ImageHandle image);
extern "C" ENGINE_DLL void RHI_Image_Alloc(RHI_ImageHandle image, const ArisenEngine::RHI::ImageDescriptor* desc);
extern "C" ENGINE_DLL bool RHI_Image_AllocDeviceMemory(RHI_ImageHandle image, unsigned int memoryPropertiesBits);
extern "C" ENGINE_DLL void RHI_Image_Free(RHI_ImageHandle image);
extern "C" ENGINE_DLL unsigned int RHI_Image_AddImageView(RHI_ImageHandle image, const ArisenEngine::RHI::ImageViewDesc* desc);


