#pragma once

#include <vulkan/vulkan_core.h>
#include <vma/vk_mem_alloc.h>
#include "RHI/Enums/Memory/ESharingMode.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Handles/RHIHandle.h"
#include <string>

namespace ArisenEngine::RHI {

class GPUPipeline;
class GPUProgram;
class RHICommandBufferPool;

/**
 * @brief Shared state for a Vulkan Buffer and its memory.
 */
struct RHIVkBufferState {
    VkDevice device{VK_NULL_HANDLE};
    VkBuffer buffer{VK_NULL_HANDLE};
    VmaAllocator allocator{VK_NULL_HANDLE};
    VmaAllocation allocation{VK_NULL_HANDLE};

    ~RHIVkBufferState() {
        if (device != VK_NULL_HANDLE && buffer != VK_NULL_HANDLE) {
            vkDestroyBuffer(device, buffer, nullptr);
        }
        if (allocator != VK_NULL_HANDLE && allocation != VK_NULL_HANDLE) {
            vmaFreeMemory(allocator, allocation);
        }
    }
};

/**
 * @brief Internal Vulkan implementation data for a Buffer.
 */
struct RHIVkBufferPoolItem {
    RHIVkBufferState* state{nullptr};
    VkBuffer buffer{VK_NULL_HANDLE};      // Cached for fast access
    VmaAllocation allocation{VK_NULL_HANDLE}; // Cached for fast access
    UInt64 size{0};
    UInt64 offset{0};
    UInt64 range{0};
    std::string name{"Anonymous"};
    RHIResourceHandle registryHandle; 
};

/**
 * @brief Shared state for a Vulkan Image and its memory.
 */
struct RHIVkImageState {
    VkDevice device{VK_NULL_HANDLE};
    VkImage image{VK_NULL_HANDLE};
    VmaAllocator allocator{VK_NULL_HANDLE};
    VmaAllocation allocation{VK_NULL_HANDLE};

    ~RHIVkImageState() {
        if (device != VK_NULL_HANDLE && image != VK_NULL_HANDLE) {
            vkDestroyImage(device, image, nullptr);
        }
        if (allocator != VK_NULL_HANDLE && allocation != VK_NULL_HANDLE) {
            vmaFreeMemory(allocator, allocation);
        }
    }
};

/**
 * @brief Internal Vulkan implementation data for an Image.
 */
struct RHIVkImagePoolItem {
    RHIVkImageState* state{nullptr};
    VkImage image{VK_NULL_HANDLE};        // Cached for fast access
    VmaAllocation allocation{VK_NULL_HANDLE}; // Cached for fast access
    UInt64 size{0};
    std::string name{"Anonymous"};
    bool needDestroy{false};
    RHIResourceHandle registryHandle;
};

/**
 * @brief Internal Vulkan implementation data for an Image View.
 */
struct RHIVkImageViewPoolItem {
    VkImageView view{VK_NULL_HANDLE};
    RHIImageHandle imageHandle; // The image this view belongs to
    EFormat format{EFormat::FORMAT_UNDEFINED};
    UInt32 width{0};
    UInt32 height{0};
    RHIResourceHandle registryHandle;
};

/**
 * @brief Internal Vulkan implementation data for a Sampler.
 */
struct RHIVkSamplerPoolItem {
    VkSampler sampler{VK_NULL_HANDLE};
    std::string name{"Anonymous"};
    RHIResourceHandle registryHandle;
};

/**
 * @brief Internal Vulkan implementation data for a RenderPass.
 */
struct RHIVkRenderPassPoolItem {
    VkRenderPass renderPass{VK_NULL_HANDLE};
    void* renderPassObj{nullptr}; // Pointer to GPURenderPass if needed
    std::string name{"Anonymous"};
    RHIResourceHandle registryHandle;
};

/**
 * @brief Internal Vulkan implementation data for a FrameBuffer.
 */
struct RHIVkFrameBufferPoolItem {
    VkFramebuffer frameBuffer{VK_NULL_HANDLE};
    UInt32 width{0};
    UInt32 height{0};
    std::string name{"Anonymous"};
    RHIResourceHandle registryHandle;
};

/**
 * @brief Internal Vulkan implementation data for a Semaphore.
 */
struct RHIVkSemaphorePoolItem {
    VkSemaphore semaphore{VK_NULL_HANDLE};
    std::string name{"Anonymous"};
    RHIResourceHandle registryHandle;
};

/**
 * @brief Internal Vulkan implementation data for a Pipeline.
 */
struct RHIVkPipelinePoolItem {
    GPUPipeline* pipeline{nullptr}; 
    std::string name{"Anonymous"};
    RHIResourceHandle registryHandle;
};

/**
 * @brief Internal Vulkan implementation data for a Fence.
 */
struct RHIVkFencePoolItem {
    VkFence fence{VK_NULL_HANDLE};
    std::string name{"Anonymous"};
    RHIResourceHandle registryHandle;
};

/**
 * @brief Internal Vulkan implementation data for a GPUProgram.
 */
struct RHIVkGPUProgramPoolItem {
    GPUProgram* program{nullptr};
    std::string name{"Anonymous"}; // Debug name
    RHIResourceHandle registryHandle;
};

/**
 * @brief Internal Vulkan implementation data for a CommandBufferPool.
 */
struct RHIVkCommandBufferPoolItem {
    RHICommandBufferPool* pool{nullptr};
    std::string name{"Anonymous"};
    RHIResourceHandle registryHandle;
};

/**
 * @brief Internal Vulkan implementation data for an individual CommandBuffer.
 */
struct RHIVkCommandBufferItem {
    class RHIVkCommandBuffer* commandBuffer{nullptr};
    std::string name{"Anonymous"};
    RHIResourceHandle registryHandle;
};

} // namespace ArisenEngine::RHI
