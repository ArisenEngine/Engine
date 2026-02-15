#include "DeviceExports.h"
#include "../../Core/Core.RHI/RHI/Core/RHIFactory.h"
#include "../../Core/Core.RHI/RHI/Core/RHIDevice.h"
#include "../../Core/Core.RHI/RHI/Queues/RHIQueue.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL void RHI_Device_WaitIdle(RHI_DeviceHandle handle)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    if (dev == nullptr) return;
    dev->DeviceWaitIdle();
}

extern "C" ENGINE_DLL void RHI_Device_GraphicQueueWaitIdle(RHI_DeviceHandle handle)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    if (dev == nullptr) return;
    dev->GraphicQueueWaitIdle();
}

// Moved to HandlesExports: CreateGPUProgram, ReleaseGPUProgram, AttachByteCode

extern "C" ENGINE_DLL void RHI_Device_SetResolution(RHI_DeviceHandle handle, unsigned int width, unsigned int height)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    if (dev == nullptr) return;
    dev->SetResolution(width, height);
}

extern "C" ENGINE_DLL RHIDeviceLimits RHI_Device_GetDeviceLimits(RHI_DeviceHandle handle)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    if (dev == nullptr) return {};
    return dev->GetDeviceLimits();
}

extern "C" ENGINE_DLL unsigned long long RHI_Device_Submit(RHI_DeviceHandle handle, RHI_CommandBufferHandle cmd, const struct RHISubmitDescriptor* descriptor)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (dev == nullptr || c == nullptr) return 0;
    return dev->Submit(c, reinterpret_cast<const RHI::RHISubmitDescriptor*>(descriptor));
}

extern "C" ENGINE_DLL unsigned long long RHI_Device_SubmitCompute(RHI_DeviceHandle handle, RHI_CommandBufferHandle cmd, const struct RHISubmitDescriptor* descriptor)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (dev == nullptr || c == nullptr) return 0;
    
    auto* queue = dev->GetQueue(RHI::RHIQueueType::Compute);
    if (queue)
    {
        return queue->Submit(c, reinterpret_cast<const RHI::RHISubmitDescriptor*>(descriptor));
    }
    
    return dev->Submit(c, reinterpret_cast<const RHI::RHISubmitDescriptor*>(descriptor));
}

// Update removed
// extern "C" ENGINE_DLL void RHI_Device_Update(RHI_DeviceHandle device)


extern "C" ENGINE_DLL void RHI_Device_WaitQueueTicket(RHI_DeviceHandle handle, unsigned long long ticket)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    if (dev == nullptr) return;
    dev->WaitQueueTicket(static_cast<RHI::RHIGpuTicket>(ticket));
}

extern "C" ENGINE_DLL void RHI_Device_WaitComputeQueueTicket(RHI_DeviceHandle handle, unsigned long long ticket)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    if (dev == nullptr) return;
    auto* queue = dev->GetQueue(RHI::RHIQueueType::Compute);
    if (queue)
    {
        queue->WaitForTicket(static_cast<RHI::RHIGpuTicket>(ticket));
    }
    else
    {
        dev->WaitQueueTicket(static_cast<RHI::RHIGpuTicket>(ticket));
    }
}

extern "C" ENGINE_DLL void RHI_Device_SetObjectName(RHI_DeviceHandle handle, RHI::ERHIObjectType type, unsigned long long resourceHandle, const char* name)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(handle);
    if (dev == nullptr || name == nullptr) return;
    dev->SetObjectName(type, resourceHandle, name);
}

// Moved to HandlesExports: CreateSampler

