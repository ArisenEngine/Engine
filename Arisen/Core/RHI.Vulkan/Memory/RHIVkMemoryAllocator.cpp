#define VMA_IMPLEMENTATION
#include "RHIVkMemoryAllocator.h"
#include "../Devices/RHIVkDevice.h"
#include "Logger/Logger.h"

namespace ArisenEngine::RHI
{
    RHIVkMemoryAllocator::RHIVkMemoryAllocator(RHIVkDevice* device, VkInstance instance, VkPhysicalDevice physicalDevice, VkDevice vkDevice, uint32_t vulkanApiVersion)
        : m_Device(device)
    {
        VmaAllocatorCreateInfo allocatorInfo = {};
        allocatorInfo.vulkanApiVersion = vulkanApiVersion;
        allocatorInfo.physicalDevice = physicalDevice;
        allocatorInfo.device = vkDevice;
        allocatorInfo.instance = instance;

        if (vmaCreateAllocator(&allocatorInfo, &m_VmaAllocator) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkMemoryAllocator]: Failed to create VMA allocator!");
        }
    }

    RHIVkMemoryAllocator::~RHIVkMemoryAllocator() noexcept
    {
        if (m_VmaAllocator != VK_NULL_HANDLE)
        {
            vmaDestroyAllocator(m_VmaAllocator);
            m_VmaAllocator = VK_NULL_HANDLE;
        }
    }

    bool RHIVkMemoryAllocator::AllocateBufferMemory(VkBuffer buffer, VmaMemoryUsage usage, VmaAllocation* outAllocation)
    {
        VkMemoryRequirements memReq;
        vkGetBufferMemoryRequirements(static_cast<VkDevice>(m_Device->GetHandle()), buffer, &memReq);

        VmaAllocationCreateInfo allocInfo = {};
        allocInfo.usage = usage;
        
        if (vmaAllocateMemory(m_VmaAllocator, &memReq, &allocInfo, outAllocation, nullptr) != VK_SUCCESS)
        {
            return false;
        }
        return vmaBindBufferMemory(m_VmaAllocator, *outAllocation, buffer) == VK_SUCCESS;
    }

    bool RHIVkMemoryAllocator::AllocateImageMemory(VkImage image, VmaMemoryUsage usage, VmaAllocation* outAllocation)
    {
        VkMemoryRequirements memReq;
        vkGetImageMemoryRequirements(static_cast<VkDevice>(m_Device->GetHandle()), image, &memReq);

        VmaAllocationCreateInfo allocInfo = {};
        allocInfo.usage = usage;
        
        if (vmaAllocateMemory(m_VmaAllocator, &memReq, &allocInfo, outAllocation, nullptr) != VK_SUCCESS)
        {
            return false;
        }
        return vmaBindImageMemory(m_VmaAllocator, *outAllocation, image) == VK_SUCCESS;
    }

    void RHIVkMemoryAllocator::FreeMemory(VmaAllocation allocation)
    {
        if (allocation != VK_NULL_HANDLE)
        {
            vmaFreeMemory(m_VmaAllocator, allocation);
        }
    }
}
