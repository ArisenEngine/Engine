#include "SyncExports.h"
#include "../../Core/RHI.Vulkan/Devices/RHIVkDevice.h"
#include "../../Core/RHI.Vulkan/Synchronization/RHIVkSemaphore.h"
#include "../../Core/RHI.Vulkan/Synchronization/RHIVkFence.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL void RHI_Semaphore_Lock(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore)
{
    // Deprecated direct access or logic needs review.
    // Vulkan Semaphores cannot be waited on CPU easily unless Timeline.
    // auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    // if (!dev || semaphore == 0) return;
    // ...
}

extern "C" ENGINE_DLL void RHI_Semaphore_Unlock(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore)
{
    // ...
}

extern "C" ENGINE_DLL void RHI_Fence_Lock(RHI_DeviceHandle device, RHI_FenceHandle fence)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (!dev || fence == 0) return;
    
    auto h = *reinterpret_cast<RHI::RHIFenceHandle*>(&fence);
    dev->WaitFence(h);
}

extern "C" ENGINE_DLL void RHI_Fence_Unlock(RHI_DeviceHandle device, RHI_FenceHandle fence)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (!dev || fence == 0) return;

    auto h = *reinterpret_cast<RHI::RHIFenceHandle*>(&fence);
    dev->ResetFence(h);
}


