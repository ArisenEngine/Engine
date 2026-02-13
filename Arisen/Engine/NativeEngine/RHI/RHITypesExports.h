#pragma once

// Opaque handle types for C ABI (Pointers)
typedef void* RHI_InstanceHandle;
typedef void* RHI_DeviceHandle;
typedef void* RHI_SurfaceHandle;
typedef void* RHI_SwapChainHandle;
typedef void* RHI_CommandBufferHandle;
typedef void* RHI_DescriptorPoolHandle;
typedef void* RHI_PSOHandle;
typedef void* RHI_PipelineManagerHandle;
typedef void* RHI_SubpassHandle;
typedef void* RHI_DescriptorSetHandle;
typedef void* RHI_FactoryHandle;

// Value handle types for C ABI (64-bit PODs - matches RHIHandle internal layout)
typedef unsigned long long RHI_FrameBufferHandle;
typedef unsigned long long RHI_RenderPassHandle;
typedef unsigned long long RHI_ImageHandle;
typedef unsigned long long RHI_ImageViewHandle;
typedef unsigned long long RHI_SemaphoreHandle;
typedef unsigned long long RHI_SamplerHandle;
typedef unsigned long long RHI_BufferHandle;
typedef unsigned long long RHI_ShaderHandle;
typedef unsigned long long RHI_PipelineHandle;
typedef unsigned long long RHI_FenceHandle;
typedef unsigned long long RHI_GPUProgramHandle;
typedef unsigned long long RHI_CommandBufferPoolHandle;
typedef unsigned long long RHI_AccelerationStructureHandle;
typedef unsigned long long RHI_MemoryPoolHandle;

