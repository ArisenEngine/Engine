#include "RHIVkDeviceMemory.h"

#include "Logger/Logger.h"
#include "RHI/Devices/Device.h"

ArisenEngine::RHI::RHIVkDeviceMemory::RHIVkDeviceMemory(Device* device, VkBuffer buffer):
m_VkDeviceMemory(VK_NULL_HANDLE), m_Device(device), m_VkBuffer(buffer)
{
            
}

ArisenEngine::RHI::RHIVkDeviceMemory::RHIVkDeviceMemory(Device* device, VkImage image):
m_VkDeviceMemory(VK_NULL_HANDLE), m_Device(device), m_VkImage(image)
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
    
    VkMemoryRequirements memRequirements;
    VkDevice vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
    
    if (m_VkBuffer.has_value())
    {
        vkGetBufferMemoryRequirements(vkDevice, m_VkBuffer.value(), &memRequirements);
    }
    else if (m_VkImage.has_value())
    {
        vkGetImageMemoryRequirements(vkDevice, m_VkImage.value(), &memRequirements);
    }
    else
    {
        LOG_FATAL_AND_THROW("[RHIVkDeviceMemory::AllocDeviceMemory]: Failed to get memory requirements");
    }
    
    m_TotalBytes = memRequirements.size;
    m_Alignment = memRequirements.alignment;
    m_MemoryTypeBits = memRequirements.memoryTypeBits;

    AllocMemory(std::move(memRequirements), memoryPropertiesBits);

    if (m_VkBuffer.has_value())
    {
        vkBindBufferMemory(vkDevice, m_VkBuffer.value(), m_VkDeviceMemory, 0);
    }
    else if (m_VkImage.has_value())
    {
        vkBindImageMemory(vkDevice, m_VkImage.value(), m_VkDeviceMemory, 0);
    }
    else
    {
        LOG_FATAL_AND_THROW("[RHIVkDeviceMemory::AllocDeviceMemory]: Failed to bind memory.");
    }
    
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

void ArisenEngine::RHI::RHIVkDeviceMemory::AllocMemory(VkMemoryRequirements&& memRequirements, UInt32 memoryPropertiesBits)
{
    VkMemoryAllocateInfo allocInfo{};
    allocInfo.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
    allocInfo.allocationSize = memRequirements.size;
    allocInfo.memoryTypeIndex = m_Device->FindMemoryType(memRequirements.memoryTypeBits, memoryPropertiesBits);
    if (vkAllocateMemory(static_cast<VkDevice>(m_Device->GetHandle()),
        &allocInfo, nullptr, &m_VkDeviceMemory) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkDeviceMemory::AllocDeviceMemory]: failed to allocate vertex buffer memory!");
    }
}
