#include "SurfaceExports.h"
#include "RHIErrorInternal.h"
#include "../../Core/Core.RHI/RHI/Core/RHIFactory.h"
#include "../../Core/Core.RHI/RHI/Core/RHIDevice.h"
#include "../../Core/Core.RHI/RHI/Presentation/RHISurface.h"
#include "../../Core/Core.RHI/RHI/Presentation/RHISwapChain.h"
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
    auto* surf = reinterpret_cast<RHI::RHISurface*>(surface);
    if (inst == nullptr || surf == nullptr) return;
    inst->UpdateSurfaceCapabilities(surf);
}

extern "C" ENGINE_DLL RHI_SwapChainHandle RHI_Surface_GetSwapChain(RHI_SurfaceHandle surface)
{
    auto* surf = reinterpret_cast<RHI::RHISurface*>(surface);
    if (surf == nullptr) return nullptr;
    return reinterpret_cast<RHI_SwapChainHandle>(surf->GetSwapChain());
}

extern "C" ENGINE_DLL void RHI_SwapChain_CreateWithDesc(RHI_SwapChainHandle swapchain, ArisenEngine::RHI::RHISwapChainDescriptor* desc)
{
    auto* sc = reinterpret_cast<RHI::RHISwapChain*>(swapchain);
    if (sc == nullptr || desc == nullptr) return;
    RHI::RHISwapChainDescriptor copy = *desc;
    sc->CreateSwapChainWithDesc(std::move(copy));
}

extern "C" ENGINE_DLL RHI_ImageHandle RHI_SwapChain_BeginFrame(RHI_SwapChainHandle swapchain, unsigned int frameIndex)
{
    auto* sc = reinterpret_cast<RHI::RHISwapChain*>(swapchain);
    if (sc == nullptr) 
    {
        RHI::SetLastError(RHI_ERROR_INVALID_HANDLE, "SwapChain handle is null");
        return 0;
    }
    auto val = sc->BeginFrame(frameIndex);
    if (!val.IsValid())
    {
        RHI::SetLastError(RHI_ERROR_DEVICE_LOST, "Failed to begin frame from swapchain");
    }
    return *reinterpret_cast<RHI_ImageHandle*>(&val);
}

extern "C" ENGINE_DLL void RHI_SwapChain_EndFrame(RHI_SwapChainHandle swapchain, unsigned int frameIndex)
{
    auto* sc = reinterpret_cast<RHI::RHISwapChain*>(swapchain);
    if (sc)
    {
        sc->EndFrame(frameIndex);
    }
}

extern "C" ENGINE_DLL RHI_ImageHandle RHI_SwapChain_AcquireCurrentImage(RHI_SwapChainHandle swapchain, unsigned int frameIndex)
{
    return RHI_SwapChain_BeginFrame(swapchain, frameIndex);
}

extern "C" ENGINE_DLL void RHI_SwapChain_Present(RHI_SwapChainHandle swapchain, unsigned int frameIndex)
{
    RHI_SwapChain_EndFrame(swapchain, frameIndex);
}

extern "C" ENGINE_DLL RHI_SemaphoreHandle RHI_SwapChain_GetImageAvailableSemaphore(RHI_SwapChainHandle swapchain, unsigned int frameIndex)
{
    auto* sc = reinterpret_cast<RHI::RHISwapChain*>(swapchain);
    if (sc == nullptr) return 0ULL;
    auto h = sc->GetImageAvailableSemaphore(frameIndex);
    return *reinterpret_cast<RHI_SemaphoreHandle*>(&h);
}

extern "C" ENGINE_DLL RHI_SemaphoreHandle RHI_SwapChain_GetRenderFinishSemaphore(RHI_SwapChainHandle swapchain, unsigned int frameIndex)
{
    auto* sc = reinterpret_cast<RHI::RHISwapChain*>(swapchain);
    if (sc == nullptr) return 0ULL;
    auto h = sc->GetRenderFinishSemaphore(frameIndex);
    return *reinterpret_cast<RHI_SemaphoreHandle*>(&h);
}

extern "C" ENGINE_DLL RHI_ImageViewHandle RHI_SwapChain_GetImageView(RHI_SwapChainHandle swapchain, unsigned int frameIndex)
{
    auto* sc = reinterpret_cast<RHI::RHISwapChain*>(swapchain);
    if (sc == nullptr) return 0ULL;
    auto hView = sc->GetImageView(frameIndex);
    return *reinterpret_cast<unsigned long long*>(&hView);
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

