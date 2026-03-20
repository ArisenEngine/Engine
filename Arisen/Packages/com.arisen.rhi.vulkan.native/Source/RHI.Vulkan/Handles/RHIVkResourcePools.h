#pragma once

#include <vulkan/vulkan_core.h>
#include <vma/vk_mem_alloc.h>
#include "RHI/Enums/Memory/ESharingMode.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Enums/Memory/ERHIMemoryUsage.h"
#include "RHI/Handles/RHIHandle.h"
#include "Base/FoundationMinimal.h"

namespace ArisenEngine::RHI
{
    class RHIPipeline;
    class RHIShaderProgram;
    class RHICommandBufferPool;
    class RHIDescriptorPool;

    /**
     * @brief Shared state for a Vulkan Buffer and its memory.
     */
    struct RHIVkBufferState
    {
        VkDevice device{VK_NULL_HANDLE};
        VkBuffer buffer{VK_NULL_HANDLE};
        VmaAllocator allocator{VK_NULL_HANDLE};
        VmaAllocation allocation{VK_NULL_HANDLE};

        ~RHIVkBufferState()
        {
            if (device != VK_NULL_HANDLE && buffer != VK_NULL_HANDLE)
            {
                vkDestroyBuffer(device, buffer, nullptr);
            }
            if (allocator != VK_NULL_HANDLE && allocation != VK_NULL_HANDLE)
            {
                vmaFreeMemory(allocator, allocation);
            }
        }
    };

    /**
     * @brief Internal Vulkan implementation data for a Buffer.
     */
    struct RHIVkBufferPoolItem
    {
        RHIVkBufferState* state{nullptr};
        VkBuffer buffer{VK_NULL_HANDLE}; // Cached for fast access
        VmaAllocation allocation{VK_NULL_HANDLE}; // Cached for fast access
        UInt64 size{0};
        UInt64 offset{0};
        UInt64 range{0};
        String name{"Anonymous"};
        ERHIMemoryUsage memoryUsage;
        UInt32 usage{0};
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Shared state for a Vulkan Image and its memory.
     */
    struct RHIVkImageState
    {
        VkDevice device{VK_NULL_HANDLE};
        VkImage image{VK_NULL_HANDLE};
        VmaAllocator allocator{VK_NULL_HANDLE};
        VmaAllocation allocation{VK_NULL_HANDLE};

        ~RHIVkImageState()
        {
            if (device != VK_NULL_HANDLE && image != VK_NULL_HANDLE)
            {
                vkDestroyImage(device, image, nullptr);
            }
            if (allocator != VK_NULL_HANDLE && allocation != VK_NULL_HANDLE)
            {
                vmaFreeMemory(allocator, allocation);
            }
        }
    };

    /**
     * @brief Shared state for a Vulkan Memory Pool (VMA Allocation used as a pool).
     */
    struct RHIVkMemoryPoolState
    {
        VmaAllocator allocator{VK_NULL_HANDLE};
        VmaAllocation allocation{VK_NULL_HANDLE};
        UInt64 size{0};

        ~RHIVkMemoryPoolState()
        {
            if (allocator != VK_NULL_HANDLE && allocation != VK_NULL_HANDLE)
            {
                vmaFreeMemory(allocator, allocation);
            }
        }
    };

    /**
     * @brief Internal Vulkan implementation data for a Memory Pool.
     */
    struct RHIVkMemoryPoolPoolItem
    {
        RHIVkMemoryPoolState* state{nullptr};
        VmaAllocation allocation{VK_NULL_HANDLE};
        UInt64 size{0};
        String name{"Anonymous"};
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Internal Vulkan implementation data for an Image.
     */
    struct RHIVkImagePoolItem
    {
        RHIVkImageState* state{nullptr};
        VkImage image{VK_NULL_HANDLE}; // Cached for fast access
        VmaAllocation allocation{VK_NULL_HANDLE}; // Cached for fast access
        UInt64 size{0};
        UInt32 width{0};
        UInt32 height{0};
        UInt32 mipLevels{1};
        String name{"Anonymous"};
        bool needDestroy{false};
        VkImageLayout currentLayout{VK_IMAGE_LAYOUT_UNDEFINED};
        ERHIMemoryUsage memoryUsage;
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Internal Vulkan implementation data for an Image View.
     */
    struct RHIVkImageViewPoolItem
    {
        VkImageView view{VK_NULL_HANDLE};
        RHIImageHandle imageHandle; // The image this view belongs to
        EFormat format{EFormat::FORMAT_UNDEFINED};
        UInt32 width{0};
        UInt32 height{0};
        String name{"Anonymous"};
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Internal Vulkan implementation data for a Sampler.
     */
    struct RHIVkSamplerPoolItem
    {
        VkSampler sampler{VK_NULL_HANDLE};
        String name{"Anonymous"};
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Internal Vulkan implementation data for a RenderPass.
     */
    struct RHIVkRenderPassPoolItem
    {
        VkRenderPass renderPass{VK_NULL_HANDLE};
        void* renderPassObj{nullptr}; // Pointer to RHIRenderPass if needed
        String name{"Anonymous"};
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Internal Vulkan implementation data for a RHIFrameBuffer.
     */
    struct RHIVkFrameBufferPoolItem
    {
        VkFramebuffer framebuffer{VK_NULL_HANDLE};
        void* frameBufferObj{nullptr};
        UInt32 width{0};
        UInt32 height{0};
        String name{"Anonymous"};
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Internal Vulkan implementation data for a Semaphore.
     */
    struct RHIVkSemaphorePoolItem
    {
        VkSemaphore semaphore{VK_NULL_HANDLE};
        String name{"Anonymous"};
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Internal Vulkan implementation data for a Pipeline.
     */
    struct RHIVkPipelinePoolItem
    {
        RHIPipeline* pipeline{nullptr};
        String name{"Anonymous"};
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Internal Vulkan implementation data for a Fence.
     */
    struct RHIVkFencePoolItem
    {
        VkFence fence{VK_NULL_HANDLE};
        String name{"Anonymous"};
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Internal Vulkan implementation data for a RHIShaderProgram.
     */
    struct RHIVkGPUProgramPoolItem
    {
        RHIShaderProgram* program{nullptr};
        std::string name{"Anonymous"}; // Debug name
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Internal Vulkan implementation data for a CommandBufferPool.
     */
    struct RHIVkCommandBufferPoolItem
    {
        RHICommandBufferPool* pool{nullptr};
        String name{"Anonymous"};
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Internal Vulkan implementation data for an individual CommandBuffer.
     */
    struct RHIVkCommandBufferItem
    {
        class RHIVkCommandBuffer* commandBuffer{nullptr};
        String name{"Anonymous"};
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Internal Vulkan implementation data for an Acceleration Structure.
     */
    struct RHIVkAccelerationStructurePoolItem
    {
        VkAccelerationStructureKHR accelerationStructure{VK_NULL_HANDLE};
        RHIBufferHandle bufferHandle; // The buffer backing this AS
        UInt64 deviceAddress{0};
        UInt64 size{0};
        String name{"Anonymous"};
        RHIResourceHandle registryHandle;
    };

    /**
     * @brief Internal Vulkan implementation data for a Descriptor Pool (handle-based).
     */
    struct RHIVkDescriptorPoolPoolItem
    {
        RHIDescriptorPool* pool{nullptr};
        String name{"Anonymous"};
        RHIResourceHandle registryHandle;
    };
} // namespace ArisenEngine::RHI
