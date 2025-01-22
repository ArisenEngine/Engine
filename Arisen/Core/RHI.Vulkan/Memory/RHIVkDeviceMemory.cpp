#include "RHIVkDeviceMemory.h"

#include "Logger/Logger.h"
#include "RHI/Devices/Device.h"

ArisenEngine::RHI::RHIVkDeviceMemory::RHIVkDeviceMemory(Device* device, VkBuffer buffer):
m_VkDeviceMemory(VK_NULL_HANDLE), m_Device(device), m_VkBuffer(buffer)
{
            
}

ArisenEngine::RHI::RHIVkDeviceMemory::~RHIVkDeviceMemory() noexcept
{
    LOG_DEBUG("RHIVkDeviceMemory::~RHIVkDeviceMemory");
    FreeDeviceMemory();
}

bool ArisenEngine::RHI::RHIVkDeviceMemory::AllocDeviceMemory(UInt32 memoryPropertiesBits)
{
    VkMemoryRequirements memRequirements;
    vkGetBufferMemoryRequirements(static_cast<VkDevice>(m_Device->GetHandle()), m_VkBuffer, &memRequirements);
    m_TotalBytes = memRequirements.size;
    m_Alignment = memRequirements.alignment;
    m_MemoryTypeBits = memRequirements.memoryTypeBits;

    VkMemoryAllocateInfo allocInfo{};
    allocInfo.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
    allocInfo.allocationSize = memRequirements.size;
    allocInfo.memoryTypeIndex = m_Device->FindMemoryType(memRequirements.memoryTypeBits, memoryPropertiesBits);
    if (vkAllocateMemory(static_cast<VkDevice>(m_Device->GetHandle()), &allocInfo, nullptr, &m_VkDeviceMemory) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkDeviceMemory::AllocDeviceMemory]: failed to allocate vertex buffer memory!");
    }

    vkBindBufferMemory(static_cast<VkDevice>(m_Device->GetHandle()), m_VkBuffer, m_VkDeviceMemory, 0);
    
    return true;
}

bool ArisenEngine::RHI::RHIVkDeviceMemory::AllocDeviceMemory(UInt32 memoryPropertiesBits, Containers::Vector<BufferHandle*> handles)
{
    // TODO : support multiple handles binding to same device memory
    throw;
}

void ArisenEngine::RHI::RHIVkDeviceMemory::FreeDeviceMemory()
{
    ASSERT(m_VkDeviceMemory != VK_NULL_HANDLE);
    LOG_DEBUG("## Destroy Vulkan Device Memory ##");
    vkFreeMemory(static_cast<VkDevice>(m_Device->GetHandle()), m_VkDeviceMemory, nullptr);
}

void* ArisenEngine::RHI::RHIVkDeviceMemory::GetHandle() const
{
    ASSERT(m_VkDeviceMemory != VK_NULL_HANDLE);
    return m_VkDeviceMemory;
}

void ArisenEngine::RHI::RHIVkDeviceMemory::MemoryCopy(void const* src, const UInt32 offset, const UInt32 size)
{
    void* data;
    vkMapMemory(static_cast<VkDevice>(m_Device->GetHandle()), m_VkDeviceMemory, offset, size, 0, &data);
    memcpy(data, src, size);
    vkUnmapMemory(static_cast<VkDevice>(m_Device->GetHandle()), m_VkDeviceMemory);
}
