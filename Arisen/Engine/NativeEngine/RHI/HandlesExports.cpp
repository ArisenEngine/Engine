#include "HandlesExports.h"
#include "../../Core/Core.Infra/RHI/Memory/ImageView.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL RHI_BufferHandle RHI_Device_GetBufferHandle(RHI_DeviceHandle device, const char* name)
{
    auto* dev = reinterpret_cast<RHI::Device*>(device);
    if (dev == nullptr) return nullptr;
    std::shared_ptr<RHI::BufferHandle> handle = dev->GetBufferHandle(name != nullptr ? std::string(name) : std::string("Anonymous"));
    return reinterpret_cast<RHI_BufferHandle>(handle.get());
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseBufferHandle(RHI_DeviceHandle device, RHI_BufferHandle buffer)
{
    auto* dev = reinterpret_cast<RHI::Device*>(device);
    auto ptr = reinterpret_cast<RHI::BufferHandle*>(buffer);
    if (dev == nullptr || ptr == nullptr) return;
    // NOTE: API expects shared_ptr management inside Device; we just pass raw ptr
    std::shared_ptr<RHI::BufferHandle> sptr(ptr, [](RHI::BufferHandle*){});
    dev->ReleaseBufferHandle(sptr);
}

extern "C" ENGINE_DLL bool RHI_Buffer_Alloc(RHI_BufferHandle buffer, const RHI::BufferDescriptor* desc)
{
    auto* b = reinterpret_cast<RHI::BufferHandle*>(buffer);
    if (b == nullptr || desc == nullptr) return false;
    RHI::BufferDescriptor copy = *desc;
    return b->AllocBufferHandle(std::move(copy));
}

extern "C" ENGINE_DLL bool RHI_Buffer_AllocDeviceMemory(RHI_BufferHandle buffer, unsigned int memoryPropertiesBits)
{
    auto* b = reinterpret_cast<RHI::BufferHandle*>(buffer);
    if (b == nullptr) return false;
    return b->AllocDeviceMemory(memoryPropertiesBits);
}

extern "C" ENGINE_DLL void RHI_Buffer_Free(RHI_BufferHandle buffer)
{
    auto* b = reinterpret_cast<RHI::BufferHandle*>(buffer);
    if (b == nullptr) return;
    b->FreeBufferHandle();
}

extern "C" ENGINE_DLL void RHI_Buffer_MemoryCopy(RHI_BufferHandle buffer, const void* src, unsigned int offset)
{
    auto* b = reinterpret_cast<RHI::BufferHandle*>(buffer);
    if (b == nullptr || src == nullptr) return;
    b->MemoryCopy(src, offset);
}

extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Size(RHI_BufferHandle buffer)
{
    auto* b = reinterpret_cast<RHI::BufferHandle*>(buffer);
    if (b == nullptr) return 0ULL;
    return b->BufferSize();
}

extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Offset(RHI_BufferHandle buffer)
{
    auto* b = reinterpret_cast<RHI::BufferHandle*>(buffer);
    if (b == nullptr) return 0ULL;
    return b->Offset();
}

extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Range(RHI_BufferHandle buffer)
{
    auto* b = reinterpret_cast<RHI::BufferHandle*>(buffer);
    if (b == nullptr) return 0ULL;
    return b->Range();
}

extern "C" ENGINE_DLL RHI_ImageHandle RHI_Device_GetImageHandle(RHI_DeviceHandle device, const char* name)
{
    auto* dev = reinterpret_cast<RHI::Device*>(device);
    if (dev == nullptr) return nullptr;
    std::shared_ptr<RHI::ImageHandle> handle = dev->GetImageHandle(name != nullptr ? std::string(name) : std::string("Anonymous"));
    return reinterpret_cast<RHI_ImageHandle>(handle.get());
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseImageHandle(RHI_DeviceHandle device, RHI_ImageHandle image)
{
    auto* dev = reinterpret_cast<RHI::Device*>(device);
    auto ptr = reinterpret_cast<RHI::ImageHandle*>(image);
    if (dev == nullptr || ptr == nullptr) return;
    std::shared_ptr<RHI::ImageHandle> sptr(ptr, [](RHI::ImageHandle*){});
    dev->ReleaseImageHandle(sptr);
}

extern "C" ENGINE_DLL void RHI_Image_Alloc(RHI_ImageHandle image, const RHI::ImageDescriptor* desc)
{
    auto* img = reinterpret_cast<RHI::ImageHandle*>(image);
    if (img == nullptr || desc == nullptr) return;
    RHI::ImageDescriptor copy = *desc;
    img->AllocHandle(std::move(copy));
}

extern "C" ENGINE_DLL bool RHI_Image_AllocDeviceMemory(RHI_ImageHandle image, unsigned int memoryPropertiesBits)
{
    auto* img = reinterpret_cast<RHI::ImageHandle*>(image);
    if (img == nullptr) return false;
    return img->AllocDeviceMemory(memoryPropertiesBits);
}

extern "C" ENGINE_DLL void RHI_Image_Free(RHI_ImageHandle image)
{
    auto* img = reinterpret_cast<RHI::ImageHandle*>(image);
    if (img == nullptr) return;
    img->FreeHandle();
}

extern "C" ENGINE_DLL unsigned int RHI_Image_AddImageView(RHI_ImageHandle image, const RHI::ImageViewDesc* desc)
{
    auto* img = reinterpret_cast<RHI::ImageHandle*>(image);
    if (img == nullptr || desc == nullptr) return 0U;
    RHI::ImageViewDesc copy = *desc;
    return img->AddImageView(std::move(copy));
}

extern "C" ENGINE_DLL RHI::ImageView* RHI_Image_GetView(RHI_ImageHandle image)
{
    auto* img = reinterpret_cast<RHI::ImageHandle*>(image);
    if (img == nullptr) return nullptr;
    return reinterpret_cast<RHI::ImageView*>(img->GetMemoryView());
}

extern "C" ENGINE_DLL RHI::EFormat RHI_ImageView_GetFormat(RHI::ImageView* view)
{
    if (view == nullptr) return RHI::EFormat::FORMAT_UNDEFINED;
    return view->GetFormat();
}

extern "C" ENGINE_DLL unsigned int RHI_ImageView_GetWidth(RHI::ImageView* view)
{
    if (view == nullptr) return 0U;
    return view->GetWidth();
}

extern "C" ENGINE_DLL unsigned int RHI_ImageView_GetHeight(RHI::ImageView* view)
{
    if (view == nullptr) return 0U;
    return view->GetHeight();
}


