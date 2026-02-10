#define VMA_IMPLEMENTATION
#include "Allocation/RHIVkMemoryAllocator.h"
#include "Core/RHIVkDevice.h"
#define VMA_IMPLEMENTATION
#include "Allocation/RHIVkMemoryAllocator.h"
#include "Core/RHIVkDevice.h"
#include "Logger/Logger.h"
#include "../../Core.RHI/RHI/Core/RHIInspector.h"


namespace ArisenEngine::RHI
{
    RHIVkMemoryAllocator::RHIVkMemoryAllocator(RHIVkDevice* device, VkInstance instance, VkPhysicalDevice physicalDevice, VkDevice vkDevice, uint32_t vulkanApiVersion, std::atomic<UInt64>* memoryCounter)
        : m_Device(device), m_MemoryCounter(memoryCounter)
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
        VmaAllocationCreateInfo allocInfo = {};
        allocInfo.usage = usage;
        
        if (vmaAllocateMemoryForBuffer(m_VmaAllocator, buffer, &allocInfo, outAllocation, nullptr) != VK_SUCCESS)
        {
            return false;
        }
        if (vmaBindBufferMemory(m_VmaAllocator, *outAllocation, buffer) != VK_SUCCESS) return false;

#if ARISEN_RHI__RESOURCE_INSPECTOR
        if (m_MemoryCounter)
        {
            VmaAllocationInfo info;
            vmaGetAllocationInfo(m_VmaAllocator, *outAllocation, &info);
            m_MemoryCounter->fetch_add(info.size, std::memory_order_relaxed);
        }
#endif

        return true;
    }


    bool RHIVkMemoryAllocator::AllocateImageMemory(VkImage image, VmaMemoryUsage usage, VmaAllocation* outAllocation)
    {
        VmaAllocationCreateInfo allocInfo = {};
        allocInfo.usage = usage;
        
        if (vmaAllocateMemoryForImage(m_VmaAllocator, image, &allocInfo, outAllocation, nullptr) != VK_SUCCESS)
        {
            return false;
        }
        if (vmaBindImageMemory(m_VmaAllocator, *outAllocation, image) != VK_SUCCESS) return false;

#if ARISEN_RHI__RESOURCE_INSPECTOR
        if (m_MemoryCounter)
        {
            VmaAllocationInfo info;
            vmaGetAllocationInfo(m_VmaAllocator, *outAllocation, &info);
            m_MemoryCounter->fetch_add(info.size, std::memory_order_relaxed);
        }
#endif

        return true;
    }


    void RHIVkMemoryAllocator::FreeMemory(VmaAllocation allocation)
    {
        if (allocation != VK_NULL_HANDLE)
        {
#if ARISEN_RHI__RESOURCE_INSPECTOR
            if (m_MemoryCounter)
            {
                VmaAllocationInfo info;
                vmaGetAllocationInfo(m_VmaAllocator, allocation, &info);
                m_MemoryCounter->fetch_sub(info.size, std::memory_order_relaxed);
            }
#endif

            vmaFreeMemory(m_VmaAllocator, allocation);
        }

    }

    UInt64 RHIVkMemoryAllocator::GetDeviceAddress(VkBuffer buffer)
    {
        VkBufferDeviceAddressInfo info{};
        info.sType = VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO;
        info.buffer = buffer;
        return vkGetBufferDeviceAddress((VkDevice)m_Device->GetHandle(), &info);
    }
}




