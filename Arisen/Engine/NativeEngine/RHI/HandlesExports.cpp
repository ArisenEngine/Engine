#include "HandlesExports.h"

#include "../../Core/RHI.Vulkan/Devices/RHIVkDevice.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIFactory.h"
#include "../../../Core/RHI.Vulkan/Handles/RHIVkResourcePools.h"
#include <unordered_map>



using namespace ArisenEngine;

extern "C" ENGINE_DLL RHI_BufferHandle RHI_Device_CreateBuffer(RHI_DeviceHandle device, const ArisenEngine::RHI::BufferDescriptor* desc, const char* name)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || desc == nullptr) return 0;
    RHI::BufferDescriptor copy = *desc;
    auto handle = dev->GetFactory()->CreateBuffer(std::move(copy), name != nullptr ? std::string(name) : std::string("Anonymous"));
    return *reinterpret_cast<unsigned long long*>(&handle);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseBuffer(RHI_DeviceHandle device, RHI_BufferHandle buffer)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || buffer == 0) return;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    dev->GetFactory()->ReleaseBuffer(h);
}

// Internalized or deprecated
extern "C" ENGINE_DLL bool RHI_Buffer_Alloc(RHI_DeviceHandle device, RHI_BufferHandle buffer, const RHI::BufferDescriptor* desc) { return false; }
extern "C" ENGINE_DLL bool RHI_Buffer_AllocDeviceMemory(RHI_DeviceHandle device, RHI_BufferHandle buffer, unsigned int memoryPropertiesBits) { return false; }
extern "C" ENGINE_DLL void RHI_Buffer_Free(RHI_DeviceHandle device, RHI_BufferHandle buffer) { RHI_Device_ReleaseBuffer(device, buffer); }

extern "C" ENGINE_DLL void RHI_Buffer_MemoryCopy(RHI_DeviceHandle device, RHI_BufferHandle buffer, const void* src, unsigned int offset)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || buffer == 0 || src == nullptr) return;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    dev->BufferMemoryCopy(h, src, offset);
}

extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Size(RHI_DeviceHandle device, RHI_BufferHandle buffer)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || buffer == 0) return 0ULL;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    return dev->GetBufferSize(h);
}

extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Offset(RHI_DeviceHandle device, RHI_BufferHandle buffer)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || buffer == 0) return 0ULL;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    return dev->GetBufferOffset(h);
}

extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Range(RHI_DeviceHandle device, RHI_BufferHandle buffer)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || buffer == 0) return 0ULL;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    return dev->GetBufferRange(h);
}

extern "C" ENGINE_DLL RHI_ImageHandle RHI_Device_CreateImage(RHI_DeviceHandle device, const ArisenEngine::RHI::ImageDescriptor* desc, const char* name)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || desc == nullptr) return 0;
    RHI::ImageDescriptor copy = *desc;
    auto handle = dev->GetFactory()->CreateImage(std::move(copy), name != nullptr ? std::string(name) : std::string("Anonymous"));
    return *reinterpret_cast<unsigned long long*>(&handle);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseImage(RHI_DeviceHandle device, RHI_ImageHandle image)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || image == 0) return;
    auto h = *reinterpret_cast<RHI::RHIImageHandle*>(&image);
    dev->GetFactory()->ReleaseImage(h);
}

// Internalized or deprecated
extern "C" ENGINE_DLL void RHI_Image_Alloc(RHI_DeviceHandle device, RHI_ImageHandle image, const RHI::ImageDescriptor* desc) {}
extern "C" ENGINE_DLL bool RHI_Image_AllocDeviceMemory(RHI_DeviceHandle device, RHI_ImageHandle image, unsigned int memoryPropertiesBits) { return false; }
extern "C" ENGINE_DLL void RHI_Image_Free(RHI_DeviceHandle device, RHI_ImageHandle image) { RHI_Device_ReleaseImage(device, image); }

extern "C" ENGINE_DLL RHI_ImageViewHandle RHI_Image_AddImageView(RHI_DeviceHandle device, RHI_ImageHandle image, const RHI::ImageViewDesc* desc)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || image == 0 || desc == nullptr) return 0ULL;
    auto hImg = *reinterpret_cast<RHI::RHIImageHandle*>(&image);
    
    RHI::ImageViewDesc copy = *desc;
    auto ivHandle = dev->GetFactory()->CreateImageView(hImg, std::move(copy));
    
    return *reinterpret_cast<unsigned long long*>(&ivHandle);
}

extern "C" ENGINE_DLL RHI::EFormat RHI_ImageView_GetFormat(RHI_DeviceHandle device, RHI_ImageViewHandle view)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || view == 0) return RHI::EFormat::FORMAT_UNDEFINED;
    auto h = *reinterpret_cast<RHI::RHIImageViewHandle*>(&view);
    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (vkDev) {
        auto* v = vkDev->GetImageViewPool()->Get(h);
        return v ? v->format : RHI::EFormat::FORMAT_UNDEFINED;
    }
    return RHI::EFormat::FORMAT_UNDEFINED;
}

extern "C" ENGINE_DLL unsigned int RHI_ImageView_GetWidth(RHI_DeviceHandle device, RHI_ImageViewHandle view)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || view == 0) return 0;
    auto h = *reinterpret_cast<RHI::RHIImageViewHandle*>(&view);
    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (vkDev) {
        auto* v = vkDev->GetImageViewPool()->Get(h);
        return v ? v->width : 0;
    }
    return 0;
}

extern "C" ENGINE_DLL unsigned int RHI_ImageView_GetHeight(RHI_DeviceHandle device, RHI_ImageViewHandle view)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || view == 0) return 0;
    auto h = *reinterpret_cast<RHI::RHIImageViewHandle*>(&view);
    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (vkDev) {
        auto* v = vkDev->GetImageViewPool()->Get(h);
        return v ? v->height : 0;
    }
    return 0;
}

extern "C" ENGINE_DLL RHI_ImageViewHandle RHI_Image_GetView(RHI_DeviceHandle device, RHI_ImageHandle image)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || image == 0) return 0ULL;
    auto hImg = *reinterpret_cast<RHI::RHIImageHandle*>(&image);
    auto hView = dev->FindImageViewForImage(hImg);
    return *reinterpret_cast<unsigned long long*>(&hView);
}
