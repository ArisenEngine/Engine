#include "InstanceExports.h"
#include "RHILoader.h"
#include "Logger/Logger.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL RHI_InstanceHandle RHI_CreateInstance(const RHI::RHIInstanceInfo* info)
{
    if (info == nullptr)
    {
        LOG_FATAL("[RHI_CreateInstance] info is null");
        return nullptr;
    }
    RHI::RHIInstanceInfo copy = *info;
    auto* instance = Graphics::RHILoader::CreateInstance(std::move(copy));
    return reinterpret_cast<RHI_InstanceHandle>(instance);
}

extern "C" ENGINE_DLL void RHI_Instance_Release(RHI_InstanceHandle instance)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return;
    delete inst;
}

extern "C" ENGINE_DLL void RHI_Instance_InitLogicDevices(RHI_InstanceHandle instance)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return;
    inst->InitLogicDevices();
}

extern "C" ENGINE_DLL void RHI_Instance_PickPhysicalDevice(RHI_InstanceHandle instance, bool considerSurface)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return;
    inst->PickPhysicalDevice(considerSurface);
}

extern "C" ENGINE_DLL void RHI_Instance_CreateSurface(RHI_InstanceHandle instance, unsigned int windowId)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return;
    inst->CreateSurface(std::move(windowId));
}

extern "C" ENGINE_DLL void RHI_Instance_ReleaseSurface(RHI_InstanceHandle instance, unsigned int windowId)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return;
    inst->DestroySurface(std::move(windowId));
}

extern "C" ENGINE_DLL void RHI_Instance_SetResolution(RHI_InstanceHandle instance, unsigned int windowId, unsigned int width, unsigned int height)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return;
    inst->SetResolution(std::move(windowId), std::move(width), std::move(height));
}

extern "C" ENGINE_DLL unsigned int RHI_Instance_GetMaxFramesInFlight(RHI_InstanceHandle instance)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return 0;
    return inst->GetMaxFramesInFlight();
}

extern "C" ENGINE_DLL bool RHI_Instance_IsPhysicalDeviceAvailable(RHI_InstanceHandle instance)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return false;
    return inst->IsPhysicalDeviceAvailable();
}

extern "C" ENGINE_DLL bool RHI_Instance_IsSurfacesAvailable(RHI_InstanceHandle instance)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return false;
    return inst->IsSurfacesAvailable();
}

extern "C" ENGINE_DLL bool RHI_Instance_PresentModeSupported(RHI_InstanceHandle instance, unsigned int windowId, RHI::EPresentMode mode)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return false;
    return inst->PresentModeSupported(std::move(windowId), mode);
}

extern "C" ENGINE_DLL void RHI_Instance_SetCurrentPresentMode(RHI_InstanceHandle instance, unsigned int windowId, RHI::EPresentMode mode)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return;
    inst->SetCurrentPresentMode(std::move(windowId), mode);
}

extern "C" ENGINE_DLL RHI::EFormat RHI_Instance_GetSuitableSwapChainFormat(RHI_InstanceHandle instance, unsigned int windowId)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return static_cast<RHI::EFormat>(0);
    return inst->GetSuitableSwapChainFormat(std::move(windowId));
}

extern "C" ENGINE_DLL RHI::EPresentMode RHI_Instance_GetSuitablePresentMode(RHI_InstanceHandle instance, unsigned int windowId)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return static_cast<RHI::EPresentMode>(0);
    return inst->GetSuitablePresentMode(std::move(windowId));
}

extern "C" ENGINE_DLL unsigned int RHI_Instance_GetExternalIndex(RHI_InstanceHandle instance)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return 0;
    return inst->GetExternalIndex();
}

extern "C" ENGINE_DLL unsigned int RHI_Instance_GetEnvStringW(RHI_InstanceHandle instance, wchar_t* buffer, unsigned int bufferLen)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return 0;
    auto w = inst->GetEnvString();
    unsigned int needed = static_cast<unsigned int>(w.size() + 1);
    if (buffer == nullptr || bufferLen == 0)
    {
        return needed;
    }
    unsigned int copyLen = (needed <= bufferLen) ? needed : bufferLen;
    wcsncpy_s(buffer, bufferLen, w.c_str(), copyLen - 1);
    buffer[copyLen - 1] = L'\0';
    return needed;
}

extern "C" ENGINE_DLL void RHI_Instance_CreateLogicDevice(RHI_InstanceHandle instance, unsigned int windowId)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return;
    inst->CreateLogicDevice(windowId);
}

extern "C" ENGINE_DLL RHI_DeviceHandle RHI_Instance_GetLogicalDevice(RHI_InstanceHandle instance, unsigned int windowId)
{
    auto* inst = reinterpret_cast<RHI::RHIInstance*>(instance);
    if (inst == nullptr) return nullptr;
    return reinterpret_cast<RHI_DeviceHandle>(inst->GetLogicalDevice(windowId));
}



