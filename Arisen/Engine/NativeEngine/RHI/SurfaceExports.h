#pragma once
#include "EngineCommon.h"

#include "../../Core/Core.RHI/RHI/Presentation/RHISwapChain.h"
#include "RHIHandleExports.h"

extern "C" ENGINE_DLL RHI_SurfaceHandle RHI_Instance_GetSurface(RHI_InstanceHandle instance, unsigned int windowId);
extern "C" ENGINE_DLL void RHI_Instance_UpdateSurfaceCapabilities(RHI_InstanceHandle instance, RHI_SurfaceHandle surface);

extern "C" ENGINE_DLL RHI_SwapChainHandle RHI_Surface_GetSwapChain(RHI_SurfaceHandle surface);
extern "C" ENGINE_DLL void RHI_SwapChain_CreateWithDesc(RHI_SwapChainHandle swapchain, ArisenEngine::RHI::RHISwapChainDescriptor* desc);
extern "C" ENGINE_DLL void RHI_SwapChain_Present(RHI_SwapChainHandle swapchain, unsigned int frameIndex);
extern "C" ENGINE_DLL RHI_ImageHandle RHI_SwapChain_AquireCurrentImage(RHI_SwapChainHandle swapchain, unsigned int frameIndex);
extern "C" ENGINE_DLL RHI_SemaphoreHandle RHI_SwapChain_GetImageAvailableSemaphore(RHI_SwapChainHandle swapchain, unsigned int frameIndex);
extern "C" ENGINE_DLL RHI_SemaphoreHandle RHI_SwapChain_GetRenderFinishSemaphore(RHI_SwapChainHandle swapchain, unsigned int frameIndex);
extern "C" ENGINE_DLL RHI_ImageViewHandle RHI_SwapChain_GetImageView(RHI_SwapChainHandle swapchain, unsigned int frameIndex);

extern "C" ENGINE_DLL RHI_FrameBufferHandle RHI_Device_GetFrameBuffer(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_ReleaseFrameBuffer(RHI_DeviceHandle device, RHI_FrameBufferHandle fb);
extern "C" ENGINE_DLL void RHI_FrameBuffer_SetAttachment(RHI_DeviceHandle device, RHI_FrameBufferHandle fb, unsigned int frameIndex, RHI_ImageViewHandle view, RHI_RenderPassHandle rp);

