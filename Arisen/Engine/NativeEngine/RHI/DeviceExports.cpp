#include "DeviceExports.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIFactory.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL void RHI_Device_WaitIdle(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return;
    dev->DeviceWaitIdle();
}

extern "C" ENGINE_DLL void RHI_Device_GraphicQueueWaitIdle(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return;
    dev->GraphicQueueWaitIdle();
}

extern "C" ENGINE_DLL RHI_GPUProgramHandle RHI_Device_CreateGPUProgram(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return 0;
    auto handle = dev->GetFactory()->CreateGPUProgram();
    return *reinterpret_cast<unsigned long long*>(&handle);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseGPUProgram(RHI_DeviceHandle device, RHI_GPUProgramHandle program)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return;
    auto h = *reinterpret_cast<RHI::RHIGPUProgramHandle*>(&program);
    dev->GetFactory()->ReleaseGPUProgram(h);
}

extern "C" ENGINE_DLL bool RHI_Device_AttachProgramByteCode(RHI_DeviceHandle device, RHI_GPUProgramHandle program, const RHI::GPUProgramDesc* desc)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || desc == nullptr || program == 0) return false;
    auto h = *reinterpret_cast<RHI::RHIGPUProgramHandle*>(&program);
    RHI::GPUProgramDesc copy = *desc;
    return dev->GetFactory()->AttachProgramByteCode(h, std::move(copy));
}

extern "C" ENGINE_DLL void RHI_Device_SetResolution(RHI_DeviceHandle device, unsigned int width, unsigned int height)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return;
    dev->SetResolution(width, height);
}

extern "C" ENGINE_DLL RHIDeviceLimits RHI_Device_GetDeviceLimits(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return {};
    return dev->GetDeviceLimits();
}

extern "C" ENGINE_DLL void RHI_Device_Submit(RHI_DeviceHandle device, RHI_CommandBufferHandle cmd, unsigned int frameIndex)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    auto* c = reinterpret_cast<RHI::RHICommandBuffer*>(cmd);
    if (dev == nullptr || c == nullptr) return;
    dev->Submit(c, frameIndex);
}

extern "C" ENGINE_DLL void RHI_Device_Update(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return;
    dev->Update();
}

extern "C" ENGINE_DLL void RHI_Device_WaitFrameFence(RHI_DeviceHandle device, unsigned int frameIndex)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return;
    dev->WaitFrameFence(frameIndex);
}

extern "C" ENGINE_DLL void RHI_Device_WaitQueueTicket(RHI_DeviceHandle device, unsigned long long ticket)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return;
    dev->WaitQueueTicket(static_cast<RHI::RHIGpuTicket>(ticket));
}

extern "C" ENGINE_DLL RHI_SamplerHandle RHI_Device_CreateSampler(RHI_DeviceHandle device, const RHI::RHISamplerDesc* desc)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || desc == nullptr) return 0;
    RHI::RHISamplerDesc copy = *desc;
    auto handle = dev->GetFactory()->CreateSampler(std::move(copy));
    return *reinterpret_cast<RHI_SamplerHandle*>(&handle);
}


