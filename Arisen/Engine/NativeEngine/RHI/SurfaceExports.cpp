#include "SurfaceExports.h"
#include <unordered_map>
#include <mutex>

using namespace ArisenEngine;

// Keep framebuffers alive across FFI by retaining shared_ptrs keyed by raw pointer.
static std::unordered_map<RHI::FrameBuffer*, std::shared_ptr<RHI::FrameBuffer>> g_FrameBufferKeepAlive;
static std::mutex g_FrameBufferKeepAliveMutex;

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

extern "C" ENGINE_DLL RHI::ImageHandle* RHI_SwapChain_AquireCurrentImage(RHI_SwapChainHandle swapchain, unsigned int frameIndex)
{
    auto* sc = reinterpret_cast<RHI::SwapChain*>(swapchain);
    if (sc == nullptr) return nullptr;
    return sc->AquireCurrentImage(frameIndex);
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
    if (dev == nullptr) return nullptr;
    auto sp = dev->GetFrameBuffer();
    auto* raw = sp.get();
    {
        std::lock_guard<std::mutex> lock(g_FrameBufferKeepAliveMutex);
        g_FrameBufferKeepAlive[raw] = sp;
    }
    return reinterpret_cast<RHI_FrameBufferHandle>(raw);
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseFrameBuffer(RHI_DeviceHandle device, RHI_FrameBufferHandle fb)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    auto* f = reinterpret_cast<RHI::FrameBuffer*>(fb);
    if (dev == nullptr || f == nullptr) return;
    std::shared_ptr<RHI::FrameBuffer> sp;
    {
        std::lock_guard<std::mutex> lock(g_FrameBufferKeepAliveMutex);
        auto it = g_FrameBufferKeepAlive.find(f);
        if (it != g_FrameBufferKeepAlive.end())
        {
            sp = it->second;
            g_FrameBufferKeepAlive.erase(it);
        }
    }
    if (sp)
    {
        dev->ReleaseFrameBuffer(sp);
    }
}

extern "C" ENGINE_DLL void RHI_FrameBuffer_SetAttachment(RHI_FrameBufferHandle fb, unsigned int frameIndex, RHI::ImageView* view, RHI_RenderPassHandle rp)
{
    auto* f = reinterpret_cast<RHI::FrameBuffer*>(fb);
    auto* r = reinterpret_cast<RHI::GPURenderPass*>(rp);
    if (f == nullptr || r == nullptr || view == nullptr) return;
    f->SetAttachment(frameIndex, view, r);
}


