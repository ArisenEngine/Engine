#pragma once
#include "EngineCommon.h"

#include "../../Core/Core.RHI/RHI/Presentation/RHISwapChain.h"
#include "RHIHandleExports.h"

/** @ownership Borrowed - Managed by Instance */
extern "C" ENGINE_DLL RHI_SurfaceHandle RHI_Instance_GetSurface(RHI_InstanceHandle instance, unsigned int windowId);
extern "C" ENGINE_DLL void RHI_Instance_UpdateSurfaceCapabilities(RHI_InstanceHandle instance, RHI_SurfaceHandle surface);

/** @ownership Borrowed - Managed by Surface */
extern "C" ENGINE_DLL RHI_SwapChainHandle RHI_Surface_GetSwapChain(RHI_SurfaceHandle surface);
extern "C" ENGINE_DLL void RHI_SwapChain_CreateWithDesc(RHI_SwapChainHandle swapchain, ArisenEngine::RHI::RHISwapChainDescriptor* desc);
/** @deprecated Use RHI_SwapChain_EndFrame instead */
extern "C" ENGINE_DLL void RHI_SwapChain_Present(RHI_SwapChainHandle swapchain, unsigned int frameIndex);

/** @ownership Borrowed - Image owned by SwapChain; do NOT release manually */
extern "C" ENGINE_DLL RHI_ImageHandle RHI_SwapChain_BeginFrame(RHI_SwapChainHandle swapchain, unsigned int frameIndex);
extern "C" ENGINE_DLL void RHI_SwapChain_EndFrame(RHI_SwapChainHandle swapchain, unsigned int frameIndex);

/** @deprecated Use RHI_SwapChain_BeginFrame instead */
extern "C" ENGINE_DLL RHI_ImageHandle RHI_SwapChain_AcquireCurrentImage(RHI_SwapChainHandle swapchain, unsigned int frameIndex);

/** @ownership Borrowed - Semaphores managed by SwapChain */
extern "C" ENGINE_DLL RHI_SemaphoreHandle RHI_SwapChain_GetImageAvailableSemaphore(RHI_SwapChainHandle swapchain, unsigned int frameIndex);
extern "C" ENGINE_DLL RHI_SemaphoreHandle RHI_SwapChain_GetRenderFinishSemaphore(RHI_SwapChainHandle swapchain, unsigned int frameIndex);

/** @ownership Borrowed - ImageView managed by SwapChain */
extern "C" ENGINE_DLL RHI_ImageViewHandle RHI_SwapChain_GetImageView(RHI_SwapChainHandle swapchain, unsigned int frameIndex);

/** @ownership Owned - Caller must release via RHI_Device_ReleaseFrameBuffer */
extern "C" ENGINE_DLL RHI_FrameBufferHandle RHI_Device_GetFrameBuffer(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_ReleaseFrameBuffer(RHI_DeviceHandle device, RHI_FrameBufferHandle fb);


