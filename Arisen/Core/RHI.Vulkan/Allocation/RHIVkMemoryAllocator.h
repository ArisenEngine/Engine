#pragma once
#include "RHI/Allocation/RHIMemoryAllocator.h"
#include <vma/vk_mem_alloc.h>
#include "vulkan_core.h"

namespace ArisenEngine::RHI
{
    class RHIVkDevice;

    class RHIVkMemoryAllocator final : public RHIMemoryAllocator
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkMemoryAllocator)
        explicit RHIVkMemoryAllocator(RHIVkDevice* device, VkInstance instance, VkPhysicalDevice physicalDevice, VkDevice vkDevice, uint32_t vulkanApiVersion);
        ~RHIVkMemoryAllocator() noexcept override;

        void* GetHandle() const override { return m_VmaAllocator; }
        VmaAllocator GetVmaAllocator() const { return m_VmaAllocator; }
        
        bool AllocateBufferMemory(VkBuffer buffer, VmaMemoryUsage usage, VmaAllocation* outAllocation);
        bool AllocateImageMemory(VkImage image, VmaMemoryUsage usage, VmaAllocation* outAllocation);
        void FreeMemory(VmaAllocation allocation);

    private:
        VmaAllocator m_VmaAllocator{ VK_NULL_HANDLE };
        RHIVkDevice* m_Device;
    };
}




