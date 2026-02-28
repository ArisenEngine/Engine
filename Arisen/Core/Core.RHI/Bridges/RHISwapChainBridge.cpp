#include "RHI/Presentation/RHISwapChain.h"
#include "RHI/Handles/RHIHandle.h"
#include "Base/BindingMacros.h"

using namespace ArisenEngine::RHI;

ARISEN_BIND_BEGIN_BRIDGE("RHISwapChain", "Core.RHI.dll", "Arisen.Native.RHI")

extern "C" {

typedef ArisenEngine::RHI::RHISwapChain SwapChain;
typedef ArisenEngine::RHI::RHIImageHandle ImageHandle;
typedef ArisenEngine::RHI::RHIImageViewHandle ImageViewHandle;

RHI_DLL uint64_t RHISwapChain_BeginFrame(SwapChain* swapChain, uint32_t frameIndex)
{
    if (!swapChain) {
        printf("[DEBUG] RHISwapChain_BeginFrame: swapChain is NULL!\n");
        return 0;
    }
    printf("[DEBUG] RHISwapChain_BeginFrame: swapChain=%p, frameIndex=%u\n", swapChain, frameIndex);
    auto handle = swapChain->BeginFrame(frameIndex);
    return *reinterpret_cast<uint64_t*>(&handle);
}

RHI_DLL void RHISwapChain_EndFrame(SwapChain* swapChain, uint32_t frameIndex)
{
    if (!swapChain) return;
    swapChain->EndFrame(frameIndex);
}

} // extern "C"

ARISEN_BIND_END_BRIDGE()
