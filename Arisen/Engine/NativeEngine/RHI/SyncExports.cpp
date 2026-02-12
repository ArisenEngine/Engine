#include "SyncExports.h"
#include "../../Core/RHI.Vulkan/Core/RHIVkDevice.h"
#include "../../Core/RHI.Vulkan/Sync/RHIVkSemaphore.h"
#include "../../Core/RHI.Vulkan/Sync/RHIVkFence.h"
#include "RHINativeBridge.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL void RHI_Semaphore_Wait(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore)
{
    auto* dev = reinterpret_cast<RHI::RHIVkDevice*>(device);
    if (!dev || semaphore == 0) return;
    auto h = *reinterpret_cast<RHI::RHISemaphoreHandle*>(&semaphore);
    auto* item = RHI::RHINativeBridge::GetSemaphoreItem(dev, h);
    if (item) {
        // CPU-side wait for binary semaphores is not standard in Vulkan, 
        // but for timeline semaphores we use WaitValue.
    }
}

extern "C" ENGINE_DLL void RHI_Semaphore_Signal(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore)
{
    // Similar to Wait, binary semaphores are usually GPU-side.
}

extern "C" ENGINE_DLL void RHI_Semaphore_WaitValue(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore, unsigned long long value)
{
    auto* dev = reinterpret_cast<RHI::RHIVkDevice*>(device);
    if (!dev || semaphore == 0) return;
    auto h = *reinterpret_cast<RHI::RHISemaphoreHandle*>(&semaphore);
    auto* item = RHI::RHINativeBridge::GetSemaphoreItem(dev, h);
    if (item) {
        VkSemaphoreWaitInfo waitInfo{};
        waitInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_WAIT_INFO;
        waitInfo.semaphoreCount = 1;
        waitInfo.pSemaphores = &item->semaphore;
        waitInfo.pValues = &value;
        vkWaitSemaphores(static_cast<VkDevice>(dev->GetHandle()), &waitInfo, UINT64_MAX);
    }
}

extern "C" ENGINE_DLL void RHI_Semaphore_SignalValue(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore, unsigned long long value)
{
    auto* dev = reinterpret_cast<RHI::RHIVkDevice*>(device);
    if (!dev || semaphore == 0) return;
    auto h = *reinterpret_cast<RHI::RHISemaphoreHandle*>(&semaphore);
    auto* item = RHI::RHINativeBridge::GetSemaphoreItem(dev, h);
    if (item) {
        VkSemaphoreSignalInfo signalInfo{};
        signalInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_SIGNAL_INFO;
        signalInfo.semaphore = item->semaphore;
        signalInfo.value = value;
        vkSignalSemaphore(static_cast<VkDevice>(dev->GetHandle()), &signalInfo);
    }
}

extern "C" ENGINE_DLL unsigned long long RHI_Semaphore_GetValue(RHI_DeviceHandle device, RHI_SemaphoreHandle semaphore)
{
    auto* dev = reinterpret_cast<RHI::RHIVkDevice*>(device);
    if (!dev || semaphore == 0) return 0;
    auto h = *reinterpret_cast<RHI::RHISemaphoreHandle*>(&semaphore);
    auto* item = RHI::RHINativeBridge::GetSemaphoreItem(dev, h);
    if (item) {
        uint64_t val = 0;
        vkGetSemaphoreCounterValue(static_cast<VkDevice>(dev->GetHandle()), item->semaphore, &val);
        return val;
    }
    return 0;
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



