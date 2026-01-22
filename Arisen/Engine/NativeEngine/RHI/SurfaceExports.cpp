#include "SurfaceExports.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIFactory.h"
#include <unordered_map>
#include <mutex>

using namespace ArisenEngine;



extern "C" ENGINE_DLL RHI_SurfaceHandle RHI_Instance_GetSurface(RHI_InstanceHandle instance, unsigned int windowId)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return nullptr;
    return reinterpret_cast<RHI_SurfaceHandle>(&inst->GetSurface(std::move(windowId)));
}

extern "C" ENGINE_DLL void RHI_Instance_UpdateSurfaceCapabilities(RHI_InstanceHandle instance, RHI_SurfaceHandle surface)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
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

extern "C" ENGINE_DLL RHI_ImageHandle RHI_SwapChain_AquireCurrentImage(RHI_SwapChainHandle swapchain, unsigned int frameIndex)
{
    auto* sc = reinterpret_cast<RHI::SwapChain*>(swapchain);
    if (sc == nullptr) return 0;
    auto val = sc->AquireCurrentImage(frameIndex);
    return *reinterpret_cast<RHI_ImageHandle*>(&val);
}

extern "C" ENGINE_DLL RHI::RHISemaphore* RHI_SwapChain_GetImageAvailableSemaphore(RHI_SwapChainHandle swapchain, unsigned int frameIndex)
{
    auto* sc = reinterpret_cast<RHI::SwapChain*>(swapchain);
    if (sc == nullptr) return nullptr;
    return sc->GetImageAvailableSemaphore(frameIndex);
}

extern "C" ENGINE_DLL RHI::RHISemaphore* RHI_SwapChain_GetRenderFinishSemaphore(RHI_SwapChainHandle swapchain, unsigned int frameIndex)
{
    auto* sc = reinterpret_cast<RHI::SwapChain*>(swapchain);
    if (sc == nullptr) return nullptr;
    return sc->GetRenderFinishSemaphore(frameIndex);
}

extern "C" ENGINE_DLL RHI_FrameBufferHandle RHI_Device_GetFrameBuffer(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return 0;
    auto val = dev->GetFactory()->CreateFrameBuffer();
    return *reinterpret_cast<RHI_FrameBufferHandle*>(&val);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseFrameBuffer(RHI_DeviceHandle device, RHI_FrameBufferHandle fb)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr || fb == 0) return;
    auto h = *reinterpret_cast<RHI::RHIFrameBufferHandle*>(&fb);
    dev->GetFactory()->ReleaseFrameBuffer(h);
}

extern "C" ENGINE_DLL void RHI_FrameBuffer_SetAttachment(RHI_DeviceHandle device, RHI_FrameBufferHandle fb, unsigned int frameIndex, RHI_ImageViewHandle view, RHI_RenderPassHandle rp)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (!dev) return;
    auto hFb = *reinterpret_cast<RHI::RHIFrameBufferHandle*>(&fb);
    auto hView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&view);
    auto hRp = *reinterpret_cast<RHI::RHIRenderPassHandle*>(&rp);

    // TODO: Need to retrieve FrameBuffer object from pool to call SetAttachment.
    // Logic placeholder.
    (void)frameIndex;
    (void)hView;
    (void)hRp;
}
