#include "SyncExports.h"
#include "../../Core/Core.RHI/RHI/Core/RHIDevice.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL void RHI_Semaphore_Wait(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (!dev || semaphore == 0) return;
    // Binary semaphore wait on CPU is not supported.
}

extern "C" ENGINE_DLL void RHI_Semaphore_Signal(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore)
{
    // Similar to Wait, binary semaphores are usually GPU-side.
}

extern "C" ENGINE_DLL void RHI_Semaphore_WaitValue(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore, unsigned long long value)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (!dev || semaphore == 0) return;
    auto h = *reinterpret_cast<RHI::RHISemaphoreHandle*>(&semaphore);
    dev->WaitSemaphoreValue(h, value);
}

extern "C" ENGINE_DLL void RHI_Semaphore_SignalValue(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore, unsigned long long value)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (!dev || semaphore == 0) return;
    auto h = *reinterpret_cast<RHI::RHISemaphoreHandle*>(&semaphore);
    dev->SignalSemaphoreValue(h, value);
}

extern "C" ENGINE_DLL unsigned long long RHI_Semaphore_GetValue(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (!dev || semaphore == 0) return 0;
    auto h = *reinterpret_cast<RHI::RHISemaphoreHandle*>(&semaphore);
    return dev->GetSemaphoreValue(h);
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



