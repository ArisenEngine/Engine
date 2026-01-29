#pragma once
#include "../../Core/RHI.Vulkan/Devices/RHIVkDevice.h"
#include "../../Core/RHI.Vulkan/Handles/RHIVkResourcePools.h"

namespace ArisenEngine::RHI
{
    // Bridge to access private internals of RHIVkDevice for Native Exports
    class RHINativeBridge
    {
    public:
        // RenderPass
        static RHIVkRenderPassPoolItem* GetRenderPassItem(RHIVkDevice* device, RHIRenderPassHandle handle)
        {
            return device->GetRenderPassPool()->Get(handle);
        }

        // Pipeline
        static RHIVkPipelinePoolItem* GetPipelineItem(RHIVkDevice* device, RHIPipelineHandle handle)
        {
            return device->GetPipelinePool()->Get(handle);
        }

        // ImageView
        static RHIVkImageViewPoolItem* GetImageViewItem(RHIVkDevice* device, RHIImageViewHandle handle)
        {
            return device->GetImageViewPool()->Get(handle);
        }

        // CommandBufferPool
        static RHIVkCommandBufferPoolItem* GetCommandBufferPoolItem(RHIVkDevice* device, RHICommandBufferPoolHandle handle)
        {
            return device->GetCommandBufferPoolPool()->Get(handle);
        }

        // RHIShaderProgram
        static RHIVkGPUProgramPoolItem* GetGPUProgramItem(RHIVkDevice* device, RHIShaderProgramHandle handle)
        {
            return device->GetGPUProgramPool()->Get(handle);
        }

        // Buffer (if needed, though GetBufferSize is now public)
        static RHIVkBufferPoolItem* GetBufferItem(RHIVkDevice* device, RHIBufferHandle handle)
        {
            return device->GetBufferPool()->Get(handle);
        }

        // Image (if needed)
        static RHIVkImagePoolItem* GetImageItem(RHIVkDevice* device, RHIImageHandle handle)
        {
            return device->GetImagePool()->Get(handle);
        }

        // Semaphore (if needed)
        static RHIVkSemaphorePoolItem* GetSemaphoreItem(RHIVkDevice* device, RHISemaphoreHandle handle)
        {
            return device->GetSemaphorePool()->Get(handle);
        }

        // Fence (if needed)
        static RHIVkFencePoolItem* GetFenceItem(RHIVkDevice* device, RHIFenceHandle handle)
        {
            return device->GetFencePool()->Get(handle);
        }
    };
}
