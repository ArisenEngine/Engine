#include "HandlesExports.h"

#include "../../Core/RHI.Vulkan/Core/RHIVkDevice.h"
#include "../../Core/Core.RHI/RHI/Core/RHIFactory.h"
#include "../../Core/Core.RHI/RHI/Core/RHIFactory.h"
#include "../../../Core/RHI.Vulkan/Handles/RHIVkResourcePools.h"
#include "RHINativeBridge.h"
#include "RHIErrorInternal.h"
#include <unordered_map>



using namespace ArisenEngine;

extern "C" ENGINE_DLL RHI_BufferHandle RHI_Device_CreateBuffer(RHI_DeviceHandle device, const ArisenEngine::RHI::RHIBufferDescriptor* desc, const char* name)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr)
    {
        RHI::SetLastError(RHI_ERROR_INVALID_HANDLE, "Device handle is null");
        return 0;
    }
    if (desc == nullptr)
    {
        RHI::SetLastError(RHI_ERROR_INVALID_PARAMETER, "RHIBufferDescriptor is null");
        return 0;
    }
    RHI::RHIBufferDescriptor copy = *desc;
    auto handle = dev->GetFactory()->CreateBuffer(std::move(copy), name != nullptr ? name : "Anonymous");
    if (!handle.IsValid())
    {
        RHI::SetLastError(RHI_ERROR_OUT_OF_MEMORY, "Failed to create RHI Buffer");
    }
    return *reinterpret_cast<unsigned long long*>(&handle);
}

extern "C" ENGINE_DLL void RHI_Device_BatchCreateBuffers(RHI_DeviceHandle device, unsigned int count, const ArisenEngine::RHI::RHIBufferDescriptor* descs, const char** names, RHI_BufferHandle* outHandles)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || descs == nullptr || outHandles == nullptr)
    {
        if (dev == nullptr) RHI::SetLastError(RHI_ERROR_INVALID_HANDLE, "Device handle is null");
        else RHI::SetLastError(RHI_ERROR_INVALID_PARAMETER, "Invalid parameters for batch buffer creation");
        return;
    }

    for (unsigned int i = 0; i < count; ++i)
    {
        RHI::RHIBufferDescriptor copy = descs[i];
        const char* name = (names != nullptr && names[i] != nullptr) ? names[i] : "Anonymous";
        auto handle = dev->GetFactory()->CreateBuffer(std::move(copy), name);
        outHandles[i] = *reinterpret_cast<unsigned long long*>(&handle);
        
        if (!handle.IsValid())
        {
             RHI::SetLastError(RHI_ERROR_OUT_OF_MEMORY, "Failed to create RHI Buffer in batch");
        }
    }
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseBuffer(RHI_DeviceHandle device, RHI_BufferHandle buffer)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || buffer == 0) return;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    dev->GetFactory()->ReleaseBuffer(h);
}

extern "C" ENGINE_DLL void* RHI_Buffer_Map(RHI_DeviceHandle device, RHI_BufferHandle buffer)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || buffer == 0) return nullptr;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    return dev->MapBuffer(h);
}

extern "C" ENGINE_DLL void RHI_Buffer_Unmap(RHI_DeviceHandle device, RHI_BufferHandle buffer)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || buffer == 0) return;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    dev->UnmapBuffer(h);
}

// Internalized or deprecated


extern "C" ENGINE_DLL void RHI_Buffer_MemoryCopy(RHI_DeviceHandle device, RHI_BufferHandle buffer, const void* src, unsigned long long size, unsigned long long offset)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || buffer == 0 || src == nullptr) return;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    dev->BufferMemoryCopy(h, src, size, offset);
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

extern "C" ENGINE_DLL unsigned long long RHI_Buffer_GetDeviceAddress(RHI_DeviceHandle device, RHI_BufferHandle buffer)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || buffer == 0) return 0ULL;
    auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&buffer);
    return dev->GetBufferDeviceAddress(h);
}

extern "C" ENGINE_DLL RHI_ImageHandle RHI_Device_CreateImage(RHI_DeviceHandle device, const ArisenEngine::RHI::RHIImageDescriptor* desc, const char* name)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr)
    {
        RHI::SetLastError(RHI_ERROR_INVALID_HANDLE, "Device handle is null");
        return 0;
    }
    if (desc == nullptr)
    {
        RHI::SetLastError(RHI_ERROR_INVALID_PARAMETER, "RHIImageDescriptor is null");
        return 0;
    }
    RHI::RHIImageDescriptor copy = *desc;
    auto handle = dev->GetFactory()->CreateImage(std::move(copy), name != nullptr ? name : "Anonymous");
    if (!handle.IsValid())
    {
        RHI::SetLastError(RHI_ERROR_OUT_OF_MEMORY, "Failed to create RHI Image");
    }
    return *reinterpret_cast<unsigned long long*>(&handle);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseImage(RHI_DeviceHandle device, RHI_ImageHandle image)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || image == 0) return;
    auto h = *reinterpret_cast<RHI::RHIImageHandle*>(&image);
    dev->GetFactory()->ReleaseImage(h);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseImageView(RHI_DeviceHandle device, RHI_ImageViewHandle view)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || view == 0) return;
    auto h = *reinterpret_cast<RHI::RHIImageViewHandle*>(&view);
    dev->GetFactory()->ReleaseImageView(h);
}

// Internalized or deprecated


extern "C" ENGINE_DLL RHI_ImageViewHandle RHI_Image_AddImageView(RHI_DeviceHandle device, RHI_ImageHandle image, const RHI::RHIImageViewDesc* desc)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || image == 0 || desc == nullptr) return 0ULL;
    auto hImg = *reinterpret_cast<RHI::RHIImageHandle*>(&image);
    
    RHI::RHIImageViewDesc copy = *desc;
    auto ivHandle = dev->GetFactory()->CreateImageView(hImg, std::move(copy));
    
    return *reinterpret_cast<unsigned long long*>(&ivHandle);
}

extern "C" ENGINE_DLL RHI::EFormat RHI_ImageView_GetFormat(RHI_DeviceHandle device, RHI_ImageViewHandle view)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || view == 0) return RHI::EFormat::FORMAT_UNDEFINED;
    auto h = *reinterpret_cast<RHI::RHIImageViewHandle*>(&view);
    return dev->GetImageViewFormat(h);
}

extern "C" ENGINE_DLL unsigned int RHI_ImageView_GetWidth(RHI_DeviceHandle device, RHI_ImageViewHandle view)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || view == 0) return 0;
    auto h = *reinterpret_cast<RHI::RHIImageViewHandle*>(&view);
    return dev->GetImageViewWidth(h);
}

extern "C" ENGINE_DLL unsigned int RHI_ImageView_GetHeight(RHI_DeviceHandle device, RHI_ImageViewHandle view)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || view == 0) return 0;
    auto h = *reinterpret_cast<RHI::RHIImageViewHandle*>(&view);
    return dev->GetImageViewHeight(h);
}

extern "C" ENGINE_DLL RHI_ImageViewHandle RHI_Image_GetView(RHI_DeviceHandle device, RHI_ImageHandle image)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || image == 0) return 0ULL;
    auto hImg = *reinterpret_cast<RHI::RHIImageHandle*>(&image);
    auto hView = dev->FindImageViewForImage(hImg);
    return *reinterpret_cast<unsigned long long*>(&hView);
}

extern "C" ENGINE_DLL RHI_SamplerHandle RHI_Device_CreateSampler(RHI_DeviceHandle device, const ArisenEngine::RHI::RHISamplerDesc* desc)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || desc == nullptr) return 0ULL;
    RHI::RHISamplerDesc copy = *desc;
    auto h = dev->GetFactory()->CreateSampler(std::move(copy));
    return *reinterpret_cast<unsigned long long*>(&h);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseSampler(RHI_DeviceHandle device, RHI_SamplerHandle sampler)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || sampler == 0) return;
    auto h = *reinterpret_cast<RHI::RHISamplerHandle*>(&sampler);
    dev->GetFactory()->ReleaseSampler(h);
}

extern "C" ENGINE_DLL RHI_GPUProgramHandle RHI_Device_CreateGPUProgram(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return 0ULL;
    auto h = dev->GetFactory()->CreateGPUProgram();
    return *reinterpret_cast<unsigned long long*>(&h);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseGPUProgram(RHI_DeviceHandle device, RHI_GPUProgramHandle program)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || program == 0) return;
    auto h = *reinterpret_cast<RHI::RHIShaderProgramHandle*>(&program);
    dev->GetFactory()->ReleaseGPUProgram(h);
}

extern "C" ENGINE_DLL bool RHI_Device_AttachProgramByteCode(RHI_DeviceHandle device, RHI_GPUProgramHandle program, const ArisenEngine::RHI::RHIShaderProgramDesc* desc)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || program == 0 || desc == nullptr) return false;
    auto h = *reinterpret_cast<RHI::RHIShaderProgramHandle*>(&program);
    RHI::RHIShaderProgramDesc copy = *desc;
    return dev->GetFactory()->AttachProgramByteCode(h, std::move(copy));
}

extern "C" ENGINE_DLL void RHI_GPUProgram_SetSpecializationConstant(RHI_DeviceHandle device, RHI_GPUProgramHandle program, unsigned int constantID, unsigned int size, const void* data)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || program == 0 || data == nullptr) return;
    auto h = *reinterpret_cast<RHI::RHIShaderProgramHandle*>(&program);
    dev->SetGPUProgramSpecializationConstant(h, constantID, size, data);
}

extern "C" ENGINE_DLL RHI_RenderPassHandle RHI_Device_CreateRenderPass(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return 0ULL;
    auto h = dev->GetFactory()->CreateRenderPass();
    return *reinterpret_cast<unsigned long long*>(&h);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseRenderPass(RHI_DeviceHandle device, RHI_RenderPassHandle rp)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || rp == 0) return;
    auto h = *reinterpret_cast<RHI::RHIRenderPassHandle*>(&rp);
    dev->GetFactory()->ReleaseRenderPass(h);
}

extern "C" ENGINE_DLL RHI_SemaphoreHandle RHI_Device_CreateSemaphore(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return 0ULL;
    auto h = dev->GetFactory()->CreateSemaphore();
    return *reinterpret_cast<unsigned long long*>(&h);
}

extern "C" ENGINE_DLL RHI_SemaphoreHandle RHI_Device_CreateTimelineSemaphore(RHI_DeviceHandle device, unsigned long long initialValue)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return 0ULL;
    auto h = dev->GetFactory()->CreateTimelineSemaphore(initialValue);
    return *reinterpret_cast<unsigned long long*>(&h);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseSemaphore(RHI_DeviceHandle device, RHI_SemaphoreHandle sem)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || sem == 0) return;
    auto h = *reinterpret_cast<RHI::RHISemaphoreHandle*>(&sem);
    dev->GetFactory()->ReleaseSemaphore(h);
}

extern "C" ENGINE_DLL RHI_MemoryPoolHandle RHI_Device_CreateMemoryPool(RHI_DeviceHandle device, unsigned long long size, unsigned int usageBits)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return 0ULL;
    auto h = dev->GetFactory()->CreateMemoryPool(size, usageBits);
    return *reinterpret_cast<unsigned long long*>(&h);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseMemoryPool(RHI_DeviceHandle device, RHI_MemoryPoolHandle pool)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || pool == 0) return;
    auto h = *reinterpret_cast<RHI::RHIMemoryPoolHandle*>(&pool);
    dev->GetFactory()->ReleaseMemoryPool(h);
}

extern "C" ENGINE_DLL RHI_BufferHandle RHI_Device_CreateBufferAliased(RHI_DeviceHandle device, const ArisenEngine::RHI::RHIBufferDescriptor* desc, RHI_MemoryPoolHandle pool, unsigned long long offset, const char* name)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || desc == nullptr || pool == 0) return 0ULL;
    auto hPool = *reinterpret_cast<RHI::RHIMemoryPoolHandle*>(&pool);
    
    RHI::RHIBufferDescriptor copy = *desc;
    auto handle = dev->GetFactory()->CreateBufferAliased(std::move(copy), hPool, offset, name != nullptr ? name : "Anonymous");
    return *reinterpret_cast<unsigned long long*>(&handle);
}

extern "C" ENGINE_DLL RHI_ImageHandle RHI_Device_CreateImageAliased(RHI_DeviceHandle device, const ArisenEngine::RHI::RHIImageDescriptor* desc, RHI_MemoryPoolHandle pool, unsigned long long offset, const char* name)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || desc == nullptr || pool == 0) return 0ULL;
    auto hPool = *reinterpret_cast<RHI::RHIMemoryPoolHandle*>(&pool);
    
    RHI::RHIImageDescriptor copy = *desc;
    auto handle = dev->GetFactory()->CreateImageAliased(std::move(copy), hPool, offset, name != nullptr ? name : "Anonymous");
    return *reinterpret_cast<unsigned long long*>(&handle);
}

