#pragma once
#include "RHI/Allocation/RHIMemoryAllocator.h"
#include <vma/vk_mem_alloc.h>
#include "vulkan_core.h"
#include <atomic>

namespace ArisenEngine::RHI

{
    class RHIVkDevice;

    class RHIVkMemoryAllocator final : public RHIMemoryAllocator
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkMemoryAllocator)
        explicit RHIVkMemoryAllocator(RHIVkDevice* device, VkInstance instance, VkPhysicalDevice physicalDevice,
                                      VkDevice vkDevice, uint32_t vulkanApiVersion,
                                      std::atomic<UInt64>* memoryCounter = nullptr);
        ~RHIVkMemoryAllocator() noexcept override;


        void* GetHandle() const override { return m_VmaAllocator; }
        VmaAllocator GetVmaAllocator() const { return m_VmaAllocator; }

        bool AllocateBufferMemory(VkBuffer buffer, VmaMemoryUsage usage, VmaAllocation* outAllocation);
        bool AllocateImageMemory(VkImage image, VmaMemoryUsage usage, VmaAllocation* outAllocation);
        bool AllocateMemory(UInt64 size, VmaMemoryUsage usage, VmaAllocation* outAllocation);
        bool BindBufferMemory(VkBuffer buffer, VmaAllocation allocation, UInt64 offset = 0);
        bool BindImageMemory(VkImage image, VmaAllocation allocation, UInt64 offset = 0);
        void FreeMemory(VmaAllocation allocation);
        UInt64 GetDeviceAddress(VkBuffer buffer);

    private:
        VmaAllocator m_VmaAllocator{VK_NULL_HANDLE};
        RHIVkDevice* m_Device;
        std::atomic<UInt64>* m_MemoryCounter{nullptr};
    };
}
