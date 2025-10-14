#include "SurfaceExports.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL RHI_SurfaceHandle RHI_Instance_GetSurface(RHI_InstanceHandle instance, unsigned int windowId)
{
    auto* inst = reinterpret_cast<RHI::Instance*>(instance);
    if (inst == nullptr) return nullptr;
    return reinterpret_cast<RHI_SurfaceHandle>(&inst->GetSurface(std::move(windowId)));
}

extern "C" ENGINE_DLL void RHI_Instance_UpdateSurfaceCapabilities(RHI_InstanceHandle instance, RHI_SurfaceHandle surface)
{
    auto* inst = reinterpret_cast<RHI::Instance*>(instance);
    auto* surf = reinterpret_cast<RHI::Surface*>(surface);
    if (inst == nullptr || surf == nullptr) return;
    inst->UpdateSurfaceCapabilities(surf);
}

extern "C" ENGINE_DLL RHI_SwapChainHandle RHI_Surface_GetSwapChain(RHI_SurfaceHandle surface)
{
    auto* surf = reinterpret_cast<RHI::Surface*>(surface);
    if (surf == nullptr) return nullptr;
    return reinterpret_cast<RHI_SwapChainHandle>(surf->GetSwapChain());
}

extern "C" ENGINE_DLL void RHI_SwapChain_CreateWithDesc(RHI_SwapChainHandle swapchain, RHI::SwapChainDescriptor* desc)
{
    auto* sc = reinterpret_cast<RHI::SwapChain*>(swapchain);
    if (sc == nullptr || desc == nullptr) return;
    RHI::SwapChainDescriptor copy = *desc;
    sc->CreateSwapChainWithDesc(std::move(copy));
}

extern "C" ENGINE_DLL void RHI_SwapChain_Present(RHI_SwapChainHandle swapchain, unsigned int frameIndex)
{
    auto* sc = reinterpret_cast<RHI::SwapChain*>(swapchain);
    if (sc == nullptr) return;
    sc->Present(frameIndex);
}

extern "C" ENGINE_DLL RHI_FrameBufferHandle RHI_Device_GetFrameBuffer(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::Device*>(device);
    if (dev == nullptr) return nullptr;
    auto sp = dev->GetFrameBuffer();
    return reinterpret_cast<RHI_FrameBufferHandle>(sp.get());
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseFrameBuffer(RHI_DeviceHandle device, RHI_FrameBufferHandle fb)
{
    auto* dev = reinterpret_cast<RHI::Device*>(device);
    auto* f = reinterpret_cast<RHI::FrameBuffer*>(fb);
    if (dev == nullptr || f == nullptr) return;
    std::shared_ptr<RHI::FrameBuffer> sp(f, [](RHI::FrameBuffer*){});
    dev->ReleaseFrameBuffer(sp);
}


