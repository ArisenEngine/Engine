// RHIInstanceBridge.cpp — extern "C" bridge for RHIInstance virtual methods
#include "RHI/Core/RHIInstance.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Definitions/CoreRHICommon.h"
#include "Base/BindingMacros.h"

using namespace ArisenEngine::RHI;

ARISEN_BIND_BEGIN_BRIDGE("RHIInstance", "Core.RHI.dll", "Arisen.Native.RHI")

extern "C" {

RHI_DLL void RHIInstance_PickPhysicalDevice(RHIInstance* inst, int considerSurface)
{
    inst->PickPhysicalDevice(considerSurface != 0);
}

RHI_DLL void RHIInstance_InitLogicDevices(RHIInstance* inst)
{
    inst->InitLogicDevices();
}

RHI_DLL void RHIInstance_CreateSurface(RHIInstance* inst, uint32_t windowId)
{
    inst->CreateSurface(windowId);
}

RHI_DLL void RHIInstance_DestroySurface(RHIInstance* inst, uint32_t windowId)
{
    inst->DestroySurface(windowId);
}

RHI_DLL void RHIInstance_SetResolution(RHIInstance* inst, uint32_t windowId, uint32_t width, uint32_t height)
{
    inst->SetResolution(windowId, width, height);
}

RHI_DLL void* RHIInstance_GetLogicalDevice(RHIInstance* inst, uint32_t windowId)
{
    return static_cast<void*>(inst->GetLogicalDevice(windowId));
}

RHI_DLL void RHIInstance_CreateLogicDevice(RHIInstance* inst, uint32_t windowId)
{
    inst->CreateLogicDevice(windowId);
}

RHI_DLL int RHIInstance_IsPhysicalDeviceAvailable(RHIInstance* inst)
{
    return inst->IsPhysicalDeviceAvailable() ? 1 : 0;
}

RHI_DLL int RHIInstance_IsSurfacesAvailable(RHIInstance* inst)
{
    return inst->IsSurfacesAvailable() ? 1 : 0;
}

RHI_DLL uint32_t RHIInstance_GetMaxFramesInFlight(RHIInstance* inst)
{
    return inst->GetMaxFramesInFlight();
}

RHI_DLL int RHIInstance_IsEnableValidation(RHIInstance* inst)
{
    return inst->IsEnableValidation() ? 1 : 0;
}

RHI_DLL uint32_t RHIInstance_GetExternalIndex(RHIInstance* inst)
{
    return inst->GetExternalIndex();
}

RHI_DLL int RHIInstance_IsSupportLinearColorSpace(RHIInstance* inst, uint32_t windowId)
{
    return inst->IsSupportLinearColorSpace(windowId) ? 1 : 0;
}

RHI_DLL int RHIInstance_PresentModeSupported(RHIInstance* inst, uint32_t windowId, int mode)
{
    return inst->PresentModeSupported(windowId, static_cast<EPresentMode>(mode)) ? 1 : 0;
}

RHI_DLL void RHIInstance_SetCurrentPresentMode(RHIInstance* inst, uint32_t windowId, int mode)
{
    inst->SetCurrentPresentMode(windowId, static_cast<EPresentMode>(mode));
}

RHI_DLL int RHIInstance_GetSuitableSwapChainFormat(RHIInstance* inst, uint32_t windowId)
{
    return static_cast<int>(inst->GetSuitableSwapChainFormat(windowId));
}

RHI_DLL int RHIInstance_GetSuitablePresentMode(RHIInstance* inst, uint32_t windowId)
{
    return static_cast<int>(inst->GetSuitablePresentMode(windowId));
}

} // extern "C"

ARISEN_BIND_END_BRIDGE()
