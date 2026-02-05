#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.RHI/RHI/Core/RHIDevice.h"
#include "../../Core/Core.RHI/RHI/Handles/RHIHandle.h"

#include "RHITypesExports.h"
namespace ArisenEngine { namespace RHI { class ImageView; } }

/**
 * @file HandlesExports.h
 * @brief RHI Resource Handle Operations
 * 
 * Ownership conventions:
 * - @ownership Owned: Caller must release via corresponding Release function
 * - @ownership Borrowed: Caller does NOT need to release; managed by parent
 */

// ============================================================================
// Buffer Handles
// ============================================================================

/** @ownership Owned - Caller must release via RHI_Device_ReleaseBuffer */
extern "C" ENGINE_DLL RHI_BufferHandle RHI_Device_CreateBuffer(RHI_DeviceHandle device, const ArisenEngine::RHI::RHIBufferDescriptor* desc, const char* name);
extern "C" ENGINE_DLL void RHI_Device_BatchCreateBuffers(RHI_DeviceHandle device, unsigned int count, const ArisenEngine::RHI::RHIBufferDescriptor* descs, const char** names, RHI_BufferHandle* outHandles);
extern "C" ENGINE_DLL void RHI_Device_ReleaseBuffer(RHI_DeviceHandle device, RHI_BufferHandle buffer);

extern "C" ENGINE_DLL void RHI_Buffer_MemoryCopy(RHI_DeviceHandle device, RHI_BufferHandle buffer, const void* src, unsigned long long size, unsigned long long offset);
extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Size(RHI_DeviceHandle device, RHI_BufferHandle buffer);
extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Offset(RHI_DeviceHandle device, RHI_BufferHandle buffer);
extern "C" ENGINE_DLL unsigned long long RHI_Buffer_Range(RHI_DeviceHandle device, RHI_BufferHandle buffer);

// ============================================================================
// Image Handles
// ============================================================================

/** @ownership Owned - Caller must release via RHI_Device_ReleaseImage */
extern "C" ENGINE_DLL RHI_ImageHandle RHI_Device_CreateImage(RHI_DeviceHandle device, const ArisenEngine::RHI::RHIImageDescriptor* desc, const char* name);
extern "C" ENGINE_DLL void RHI_Device_ReleaseImage(RHI_DeviceHandle device, RHI_ImageHandle image);

/** @ownership Borrowed - View lifetime managed by parent image */
extern "C" ENGINE_DLL RHI_ImageViewHandle RHI_Image_AddImageView(RHI_DeviceHandle device, RHI_ImageHandle image, const ArisenEngine::RHI::RHIImageViewDesc* desc);
extern "C" ENGINE_DLL ArisenEngine::RHI::EFormat RHI_ImageView_GetFormat(RHI_DeviceHandle device, RHI_ImageViewHandle view);
extern "C" ENGINE_DLL unsigned int RHI_ImageView_GetWidth(RHI_DeviceHandle device, RHI_ImageViewHandle view);
extern "C" ENGINE_DLL unsigned int RHI_ImageView_GetHeight(RHI_DeviceHandle device, RHI_ImageViewHandle view);

/** @ownership Borrowed - View lifetime managed by parent image */
extern "C" ENGINE_DLL RHI_ImageViewHandle RHI_Image_GetView(RHI_DeviceHandle device, RHI_ImageHandle image);

// ============================================================================
// Sampler Handles
// ============================================================================

/** @ownership Owned - Caller must release via RHI_Device_ReleaseSampler */
extern "C" ENGINE_DLL RHI_SamplerHandle RHI_Device_CreateSampler(RHI_DeviceHandle device, const ArisenEngine::RHI::RHISamplerDesc* desc);
extern "C" ENGINE_DLL void RHI_Device_ReleaseSampler(RHI_DeviceHandle device, RHI_SamplerHandle sampler);

// ============================================================================
// Shader Program Handles
// ============================================================================

/** @ownership Owned - Caller must release via RHI_Device_ReleaseGPUProgram */
extern "C" ENGINE_DLL RHI_GPUProgramHandle RHI_Device_CreateGPUProgram(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_ReleaseGPUProgram(RHI_DeviceHandle device, RHI_GPUProgramHandle program);
extern "C" ENGINE_DLL bool RHI_Device_AttachProgramByteCode(RHI_DeviceHandle device, RHI_GPUProgramHandle program, const ArisenEngine::RHI::RHIShaderProgramDesc* desc);
extern "C" ENGINE_DLL void RHI_GPUProgram_SetSpecializationConstant(RHI_DeviceHandle device, RHI_GPUProgramHandle program, unsigned int constantID, unsigned int size, const void* data);

// ============================================================================
// RenderPass Handles
// ============================================================================

/** @ownership Owned - Caller must release via RHI_Device_ReleaseRenderPass */
extern "C" ENGINE_DLL RHI_RenderPassHandle RHI_Device_CreateRenderPass(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_ReleaseRenderPass(RHI_DeviceHandle device, RHI_RenderPassHandle rp);

