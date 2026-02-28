#define VMA_IMPLEMENTATION
#include "Allocation/RHIVkMemoryAllocator.h"
#include "Profiler.h"
#include "Core/RHIVkDevice.h"
#include "Logger/Logger.h"
#include "../../Core.RHI/RHI/Core/RHIInspector.h"


namespace ArisenEngine::RHI
{
    RHIVkMemoryAllocator::RHIVkMemoryAllocator(RHIVkDevice* device, VkInstance instance,
                                               VkPhysicalDevice physicalDevice, VkDevice vkDevice,
                                               uint32_t vulkanApiVersion, std::atomic<UInt64>* memoryCounter)
        : m_Device(device), m_MemoryCounter(memoryCounter)
    {
        VmaAllocatorCreateInfo allocatorInfo = {};
        allocatorInfo.vulkanApiVersion = vulkanApiVersion;
        allocatorInfo.physicalDevice = physicalDevice;
        allocatorInfo.device = vkDevice;
        allocatorInfo.instance = instance;
        allocatorInfo.flags = VMA_ALLOCATOR_CREATE_BUFFER_DEVICE_ADDRESS_BIT;

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

#include <iostream>

    bool RHIVkMemoryAllocator::AllocateBufferMemory(VkBuffer buffer, VmaMemoryUsage usage, VmaAllocation* outAllocation)
    {
        ARISEN_PROFILE_ZONE("Vk::VMA_AllocBufferMemory");
        std::cout << "AllocateBufferMemory Called. Buffer: " << (void*)buffer << " Usage: " << (int)usage << std::endl;
        VmaAllocationCreateInfo allocInfo = {};
        allocInfo.usage = usage;

        VkResult result = vmaAllocateMemoryForBuffer(m_VmaAllocator, buffer, &allocInfo, outAllocation, nullptr);
        if (result != VK_SUCCESS)
        {
            std::cout << "vmaAllocateMemoryForBuffer failed! Result: " << result << " Usage: " << (int)usage <<
                std::endl;
            LOG_ERRORF("[RHIVkMemoryAllocator]: vmaAllocateMemoryForBuffer failed! Result: {0}, Usage: {1}",
                       (int)result, (int)usage);
            return false;
        }
        if (vmaBindBufferMemory(m_VmaAllocator, *outAllocation, buffer) != VK_SUCCESS)
        {
            std::cout << "vmaBindBufferMemory failed!" << std::endl;
            LOG_ERROR("[RHIVkMemoryAllocator]: vmaBindBufferMemory failed!");
            return false;
        }

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
        ARISEN_PROFILE_ZONE("Vk::VMA_AllocImageMemory");
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

    bool RHIVkMemoryAllocator::AllocateMemory(UInt64 size, VmaMemoryUsage usage, VmaAllocation* outAllocation)
    {
        ARISEN_PROFILE_ZONE("Vk::VMA_AllocMemory");
        VmaAllocationCreateInfo allocInfo = {};
        allocInfo.usage = usage;

        VkMemoryRequirements memReq = {};
        memReq.size = size;
        memReq.alignment = 1; // Minimum alignment
        memReq.memoryTypeBits = 0xFFFFFFFF; // Any type

        if (vmaAllocateMemory(m_VmaAllocator, &memReq, &allocInfo, outAllocation, nullptr) != VK_SUCCESS)
        {
            return false;
        }

#if ARISEN_RHI__RESOURCE_INSPECTOR
        if (m_MemoryCounter)
        {
            m_MemoryCounter->fetch_add(size, std::memory_order_relaxed);
        }
#endif
        return true;
    }

    bool RHIVkMemoryAllocator::BindBufferMemory(VkBuffer buffer, VmaAllocation allocation, UInt64 offset)
    {
        return vmaBindBufferMemory2(m_VmaAllocator, allocation, offset, buffer, nullptr) == VK_SUCCESS;
    }

    bool RHIVkMemoryAllocator::BindImageMemory(VkImage image, VmaAllocation allocation, UInt64 offset)
    {
        return vmaBindImageMemory2(m_VmaAllocator, allocation, offset, image, nullptr) == VK_SUCCESS;
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
