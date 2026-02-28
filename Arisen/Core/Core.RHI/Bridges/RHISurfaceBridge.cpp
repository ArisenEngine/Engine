// RHISurfaceBridge.cpp — extern "C" bridge for RHISurface
#include "RHI/Presentation/RHISurface.h"
#include "RHI/Presentation/RHISwapChain.h"
#include "Base/BindingMacros.h"

using namespace ArisenEngine::RHI;

ARISEN_BIND_BEGIN_BRIDGE("RHISurface", "Core.RHI.dll", "Arisen.Native.RHI")

extern "C" {
RHI_DLL void RHISurface_InitSwapChain(RHISurface* surface)
{
    if (!surface) return;
    surface->InitSwapChain();
}

RHI_DLL void* RHISurface_GetSwapChain(RHISurface* surface)
{
    if (!surface)
    {
        printf("[DEBUG] RHISurface_GetSwapChain: surface is NULL!\n");
        return nullptr;
    }
    void* sc = static_cast<void*>(surface->GetSwapChain());
    printf("[DEBUG] RHISurface_GetSwapChain: surface=%p, swapchain=%p\n", surface, sc);
    return sc;
}
} // extern "C"

ARISEN_BIND_END_BRIDGE()
