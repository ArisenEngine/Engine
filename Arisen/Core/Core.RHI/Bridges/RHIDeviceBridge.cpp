// RHIDeviceBridge.cpp — extern "C" bridge for RHIDevice virtual methods
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"
#include "RHI/Core/RHIInstance.h"
#include "RHI/Definitions/CoreRHICommon.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "Base/BindingMacros.h"

using namespace ArisenEngine::RHI;

ARISEN_BIND_BEGIN_BRIDGE("RHIDevice", "Core.RHI.dll", "Arisen.Native.RHI")

extern "C" {

RHI_DLL void RHIDevice_DeviceWaitIdle(RHIDevice* dev)
{
    dev->DeviceWaitIdle();
}

RHI_DLL void RHIDevice_GraphicQueueWaitIdle(RHIDevice* dev)
{
    dev->GraphicQueueWaitIdle();
}

RHI_DLL uint32_t RHIDevice_GetMaxFramesInFlight(RHIDevice* dev)
{
    return dev->GetMaxFramesInFlight();
}

RHI_DLL void* RHIDevice_GetFactory(RHIDevice* dev)
{
    return static_cast<void*>(dev->GetFactory());
}

RHI_DLL void* RHIDevice_GetInstance(RHIDevice* dev)
{
    return static_cast<void*>(dev->GetInstance());
}

RHI_DLL void RHIDevice_SetResolution(RHIDevice* dev, uint32_t width, uint32_t height)
{
    dev->SetResolution(width, height);
}

RHI_DLL void RHIDevice_SetObjectName(RHIDevice* dev, int objectType, uint64_t handle, const char* name)
{
    dev->SetObjectName(static_cast<ERHIObjectType>(objectType), handle, name);
}

RHI_DLL void RHIDevice_GetDeviceLimits(RHIDevice* dev, RHIDeviceLimits* outLimits)
{
    *outLimits = dev->GetDeviceLimits();
}

RHI_DLL void* RHIDevice_GetCommandBuffer(RHIDevice* dev, uint32_t index, uint32_t generation)
{
    RHICommandBufferHandle handle;
    handle.index = index;
    handle.generation = generation;
    return static_cast<void*>(dev->GetCommandBuffer(handle));
}

RHI_DLL void* RHIDevice_GetCommandBufferPool(RHIDevice* dev, uint32_t index, uint32_t generation)
{
    RHICommandBufferPoolHandle handle;
    handle.index = index;
    handle.generation = generation;
    return static_cast<void*>(dev->GetCommandBufferPool(handle));
}

RHI_DLL uint64_t RHIDevice_GetCompletedSubmitTicket(RHIDevice* dev)
{
    return dev->GetCompletedSubmitTicket();
}

RHI_DLL void RHIDevice_WaitQueueTicket(RHIDevice* dev, uint64_t ticket)
{
    dev->WaitQueueTicket(ticket);
}

RHI_DLL uint64_t RHIDevice_Submit(RHIDevice* dev, uint32_t index, uint32_t generation)
{
    RHICommandBufferHandle handle;
    handle.index = index;
    handle.generation = generation;
    return dev->Submit(handle);
}

RHI_DLL void* RHIDevice_GetPipelineCache(RHIDevice* dev)
{
    return static_cast<void*>(dev->GetPipelineCache());
}

} // extern "C"

ARISEN_BIND_END_BRIDGE()
