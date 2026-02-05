#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.RHI/RHI/Core/RHIInstance.h"
#include "../../Core/Core.RHI/RHI/Enums/Swapchain/EPresentMode.h"
#include "../../Core/Core.RHI/RHI/Enums/Image/EFormat.h"

// Opaque handle types for C ABI
typedef void* RHI_InstanceHandle;
typedef void* RHI_DeviceHandle;

extern "C" ENGINE_DLL RHI_InstanceHandle RHI_CreateInstance(const ArisenEngine::RHI::RHIInstanceInfo* info);
extern "C" ENGINE_DLL void RHI_Instance_Release(RHI_InstanceHandle instance);

extern "C" ENGINE_DLL void RHI_Instance_InitLogicDevices(RHI_InstanceHandle instance);
extern "C" ENGINE_DLL void RHI_Instance_PickPhysicalDevice(RHI_InstanceHandle instance, bool considerSurface);
extern "C" ENGINE_DLL void RHI_Instance_CreateSurface(RHI_InstanceHandle instance, unsigned int windowId);
extern "C" ENGINE_DLL void RHI_Instance_ReleaseSurface(RHI_InstanceHandle instance, unsigned int windowId);
extern "C" ENGINE_DLL void RHI_Instance_SetResolution(RHI_InstanceHandle instance, unsigned int windowId, unsigned int width, unsigned int height);
extern "C" ENGINE_DLL unsigned int RHI_Instance_GetMaxFramesInFlight(RHI_InstanceHandle instance);
extern "C" ENGINE_DLL bool RHI_Instance_IsPhysicalDeviceAvailable(RHI_InstanceHandle instance);
extern "C" ENGINE_DLL bool RHI_Instance_IsSurfacesAvailable(RHI_InstanceHandle instance);
extern "C" ENGINE_DLL bool RHI_Instance_PresentModeSupported(RHI_InstanceHandle instance, unsigned int windowId, ArisenEngine::RHI::EPresentMode mode);
extern "C" ENGINE_DLL void RHI_Instance_SetCurrentPresentMode(RHI_InstanceHandle instance, unsigned int windowId, ArisenEngine::RHI::EPresentMode mode);
extern "C" ENGINE_DLL ArisenEngine::RHI::EFormat RHI_Instance_GetSuitableSwapChainFormat(RHI_InstanceHandle instance, unsigned int windowId);
extern "C" ENGINE_DLL ArisenEngine::RHI::EPresentMode RHI_Instance_GetSuitablePresentMode(RHI_InstanceHandle instance, unsigned int windowId);
extern "C" ENGINE_DLL unsigned int RHI_Instance_GetExternalIndex(RHI_InstanceHandle instance);
extern "C" ENGINE_DLL unsigned int RHI_Instance_GetEnvStringW(RHI_InstanceHandle instance, wchar_t* buffer, unsigned int bufferLen);

extern "C" ENGINE_DLL void RHI_Instance_CreateLogicDevice(RHI_InstanceHandle instance, unsigned int windowId);
extern "C" ENGINE_DLL RHI_DeviceHandle RHI_Instance_GetLogicalDevice(RHI_InstanceHandle instance, unsigned int windowId);


