#include "RHIVkDeviceMemory.h"
#include "RHIVkMemoryAllocator.h"

#include "Logger/Logger.h"
#include "../Devices/RHIVkDevice.h"

namespace ArisenEngine::RHI {
    struct DeferredVmaAllocation {
        VmaAllocator allocator;
        VmaAllocation allocation;
        ~DeferredVmaAllocation() {
            if (allocator != VK_NULL_HANDLE && allocation != VK_NULL_HANDLE) {
                vmaFreeMemory(allocator, allocation);
            }
        }
    };
}

ArisenEngine::RHI::RHIVkDeviceMemory::RHIVkDeviceMemory(RHIDevice* device, VkBuffer buffer):
m_Allocation(VK_NULL_HANDLE), m_VkDeviceMemory(VK_NULL_HANDLE), m_Device(device), m_VkBuffer(buffer)
{
            
}

ArisenEngine::RHI::RHIVkDeviceMemory::RHIVkDeviceMemory(RHIDevice* device, VkImage image):
m_Allocation(VK_NULL_HANDLE), m_VkDeviceMemory(VK_NULL_HANDLE), m_Device(device), m_VkImage(image)
{
}

ArisenEngine::RHI::RHIVkDeviceMemory::~RHIVkDeviceMemory() noexcept
{
    LOG_DEBUG("RHIVkDeviceMemory::~RHIVkDeviceMemory");
    FreeDeviceMemory();
}

bool ArisenEngine::RHI::RHIVkDeviceMemory::AllocDeviceMemory(UInt32 memoryPropertiesBits)
{
    ASSERT((m_VkBuffer.has_value() && !m_VkImage.has_value()) || (!m_VkBuffer.has_value() && m_VkImage.has_value()));
    
    auto* vkDevice = static_cast<RHIVkDevice*>(m_Device);
    auto* allocator = static_cast<RHIVkMemoryAllocator*>(vkDevice->GetMemoryAllocator());
    VmaAllocator vmaAllocator = allocator->GetVmaAllocator();

    VmaAllocationCreateInfo allocCreateInfo = {};
    // Map RHI memory properties to VMA usage/flags if needed. 
    // For now, use a simple mapping or let VMA decide based on properties.
    allocCreateInfo.requiredFlags = memoryPropertiesBits;

    if (m_VkBuffer.has_value())
    {
        if (vmaAllocateMemoryForBuffer(vmaAllocator, m_VkBuffer.value(), &allocCreateInfo, &m_Allocation, nullptr) != VK_SUCCESS)
        {
             LOG_FATAL_AND_THROW("[RHIVkDeviceMemory::AllocDeviceMemory]: Failed to allocate VMA memory for buffer");
        }
        vmaBindBufferMemory(vmaAllocator, m_Allocation, m_VkBuffer.value());
    }
    else if (m_VkImage.has_value())
    {
        if (vmaAllocateMemoryForImage(vmaAllocator, m_VkImage.value(), &allocCreateInfo, &m_Allocation, nullptr) != VK_SUCCESS)
        {
             LOG_FATAL_AND_THROW("[RHIVkDeviceMemory::AllocDeviceMemory]: Failed to allocate VMA memory for image");
        }
        vmaBindImageMemory(vmaAllocator, m_Allocation, m_VkImage.value());
    }

    VmaAllocationInfo allocInfo;
    vmaGetAllocationInfo(vmaAllocator, m_Allocation, &allocInfo);
    
    m_TotalBytes = allocInfo.size;
    m_VkDeviceMemory = allocInfo.deviceMemory;
    // VMA handles alignment and memory type internally efficiently.

    auto* registry = vkDevice->GetResourceRegistry();
    auto* deferred = new DeferredVmaAllocation{
        vmaAllocator,
        m_Allocation
    };

    m_RHIHandle = registry->Create(MakeDeferredDeleteItem(deferred));
    
    return true;
}

bool ArisenEngine::RHI::RHIVkDeviceMemory::AllocDeviceMemory(UInt32 memoryPropertiesBits,
    Containers::Vector<BufferHandle*> handles)
{
    // TODO : support multiple handles binding to same device memory
    throw;
}

void ArisenEngine::RHI::RHIVkDeviceMemory::FreeDeviceMemory()
{
    if (m_Allocation != VK_NULL_HANDLE)
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(m_Device);
        auto* registry = vkDevice->GetResourceRegistry();
        
        LOG_DEBUG("## Release Vulkan VMA Allocation ##");
        registry->Release(m_RHIHandle, RHIQueueType::Graphics, vkDevice->GetCompletedSubmitId());
        
        m_Allocation = VK_NULL_HANDLE;
        m_VkDeviceMemory = VK_NULL_HANDLE;
        m_RHIHandle = RHIResourceHandle::Invalid();
    }
}

void* ArisenEngine::RHI::RHIVkDeviceMemory::GetHandle() const
{
    ASSERT(m_VkDeviceMemory != VK_NULL_HANDLE);
    return m_VkDeviceMemory;
}

void ArisenEngine::RHI::RHIVkDeviceMemory::MemoryCopy(void const* src, const UInt32 offset, const UInt32 size)
{
    auto* vkDevice = static_cast<RHIVkDevice*>(m_Device);
    auto* allocator = static_cast<RHIVkMemoryAllocator*>(vkDevice->GetMemoryAllocator());
    VmaAllocator vmaAllocator = allocator->GetVmaAllocator();

    void* data;
    if (vmaMapMemory(vmaAllocator, m_Allocation, &data) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkDeviceMemory::MemoryCopy]: Failed to map VMA memory");
    }
    memcpy(static_cast<uint8_t*>(data) + offset, src, size);
    vmaUnmapMemory(vmaAllocator, m_Allocation);
}

// AllocMemory is no longer used, as VMA combines allocation and requirements handling.
