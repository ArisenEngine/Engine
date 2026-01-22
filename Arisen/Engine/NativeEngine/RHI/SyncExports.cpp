#include "SyncExports.h"
#include "../../Core/RHI.Vulkan/Devices/RHIVkDevice.h"
#include "../../Core/RHI.Vulkan/Synchronization/RHIVkSemaphore.h"
#include "../../Core/RHI.Vulkan/Synchronization/RHIVkFence.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL void RHI_Semaphore_Lock(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (!dev || semaphore == 0) return;
    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (!vkDev) return;
    
    auto h = *reinterpret_cast<RHI::RHISemaphoreHandle*>(&semaphore);
    auto* s = vkDev->GetSemaphorePool()->Get(h);
    if (s && s->semaphore != VK_NULL_HANDLE) {
        // Logic for wait (Vulkan doesn't have a direct Wait on-binary semaphore from CPU, only timeline)
        // If it was meant for timeline, it would need a value.
    }
}

extern "C" ENGINE_DLL void RHI_Semaphore_Unlock(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore)
{
    // ...
}

extern "C" ENGINE_DLL void RHI_Fence_Lock(RHI_DeviceHandle device, RHI_FenceHandle fence)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (!dev || fence == 0) return;
    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (!vkDev) return;

    auto h = *reinterpret_cast<RHI::RHIFenceHandle*>(&fence);
    auto* f = vkDev->GetFencePool()->Get(h);
    if (f && f->fence != VK_NULL_HANDLE) {
        vkWaitForFences(static_cast<VkDevice>(vkDev->GetHandle()), 1, &f->fence, VK_TRUE, UINT64_MAX);
    }
}

extern "C" ENGINE_DLL void RHI_Fence_Unlock(RHI_DeviceHandle device, RHI_FenceHandle fence)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (!dev || fence == 0) return;
    auto* vkDev = dynamic_cast<RHI::RHIVkDevice*>(dev);
    if (!vkDev) return;

    auto h = *reinterpret_cast<RHI::RHIFenceHandle*>(&fence);
    auto* f = vkDev->GetFencePool()->Get(h);
    if (f && f->fence != VK_NULL_HANDLE) {
        vkResetFences(static_cast<VkDevice>(vkDev->GetHandle()), 1, &f->fence);
    }
}


