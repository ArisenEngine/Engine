#include "DeviceExports.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIFactory.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL void RHI_Device_WaitIdle(void* handle)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    if (dev == nullptr) return;
    dev->DeviceWaitIdle();
}

extern "C" ENGINE_DLL void RHI_Device_GraphicQueueWaitIdle(void* handle)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    if (dev == nullptr) return;
    dev->GraphicQueueWaitIdle();
}

// Moved to HandlesExports: CreateGPUProgram, ReleaseGPUProgram, AttachByteCode

extern "C" ENGINE_DLL void RHI_Device_SetResolution(void* handle, unsigned int width, unsigned int height)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    if (dev == nullptr) return;
    dev->SetResolution(width, height);
}

extern "C" ENGINE_DLL RHIDeviceLimits RHI_Device_GetDeviceLimits(void* handle)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    if (dev == nullptr) return {};
    return dev->GetDeviceLimits();
}

extern "C" ENGINE_DLL unsigned long long RHI_Device_Submit(void* handle, void* cmd, unsigned int frameIndex)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (dev == nullptr || c == nullptr) return 0;
    return dev->Submit(c, frameIndex);
}

// Update removed
// extern "C" ENGINE_DLL void RHI_Device_Update(RHI_DeviceHandle device)

extern "C" ENGINE_DLL void RHI_Device_WaitFrameFence(void* handle, unsigned int frameIndex)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    if (dev == nullptr) return;
    dev->WaitFrameFence(frameIndex);
}

extern "C" ENGINE_DLL void RHI_Device_WaitQueueTicket(void* handle, unsigned long long ticket)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    if (dev == nullptr) return;
    dev->WaitQueueTicket(static_cast<RHI::RHIGpuTicket>(ticket));
}

// Moved to HandlesExports: CreateSampler
