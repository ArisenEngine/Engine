#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/Surfaces/Surface.h"
#include "../../Core/Core.Infra/RHI/Surfaces/SwapChain.h"
#include "../../Core/Core.Infra/RHI/Surfaces/FrameBuffer.h"

typedef void* RHI_InstanceHandle;
typedef void* RHI_DeviceHandle;
typedef void* RHI_SurfaceHandle;
typedef void* RHI_SwapChainHandle;
typedef void* RHI_FrameBufferHandle;

extern "C" ENGINE_DLL RHI_SurfaceHandle RHI_Instance_GetSurface(RHI_InstanceHandle instance, unsigned int windowId);
extern "C" ENGINE_DLL void RHI_Instance_UpdateSurfaceCapabilities(RHI_InstanceHandle instance, RHI_SurfaceHandle surface);

extern "C" ENGINE_DLL RHI_SwapChainHandle RHI_Surface_GetSwapChain(RHI_SurfaceHandle surface);
extern "C" ENGINE_DLL void RHI_SwapChain_CreateWithDesc(RHI_SwapChainHandle swapchain, ArisenEngine::RHI::SwapChainDescriptor* desc);
extern "C" ENGINE_DLL void RHI_SwapChain_Present(RHI_SwapChainHandle swapchain, unsigned int frameIndex);
extern "C" ENGINE_DLL ArisenEngine::RHI::ImageHandle* RHI_SwapChain_AquireCurrentImage(RHI_SwapChainHandle swapchain, unsigned int frameIndex);
extern "C" ENGINE_DLL ArisenEngine::RHI::RHISemaphore* RHI_SwapChain_GetImageAvailableSemaphore(RHI_SwapChainHandle swapchain, unsigned int frameIndex);
extern "C" ENGINE_DLL ArisenEngine::RHI::RHISemaphore* RHI_SwapChain_GetRenderFinishSemaphore(RHI_SwapChainHandle swapchain, unsigned int frameIndex);

extern "C" ENGINE_DLL RHI_FrameBufferHandle RHI_Device_GetFrameBuffer(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_ReleaseFrameBuffer(RHI_DeviceHandle device, RHI_FrameBufferHandle fb);


