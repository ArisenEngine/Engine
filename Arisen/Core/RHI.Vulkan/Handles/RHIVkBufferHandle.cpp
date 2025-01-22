#include "RHIVkBufferHandle.h"

#include "../Memory/RHIVkDeviceMemory.h"

ArisenEngine::RHI::RHIVkBufferHandle::RHIVkBufferHandle(Device* device)
: BufferHandle() , m_Device(device)
{
    
}

ArisenEngine::RHI::RHIVkBufferHandle::~RHIVkBufferHandle() noexcept
{
    LOG_DEBUG("[RHIVkBufferHandle::~RHIVkBufferHandle]: ~RHIVkBufferHandle");
    FreeBufferHandle();
}

void* ArisenEngine::RHI::RHIVkBufferHandle::GetHandle() const
{
    ASSERT(m_VkBuffer != VK_NULL_HANDLE);
    return m_VkBuffer;
}


bool ArisenEngine::RHI::RHIVkBufferHandle::AllocBufferHandle(BufferAllocDesc && desc)
{
    VkBufferCreateInfo bufferInfo{};
    bufferInfo.sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
    bufferInfo.flags = desc.createFlagBits;
    bufferInfo.size = desc.size;
    bufferInfo.usage = static_cast<VkBufferUsageFlags>(desc.usage);
    bufferInfo.sharingMode = static_cast<VkSharingMode>(desc.sharingMode);

    m_BufferSize = desc.size;
    
    if (desc.sharingMode == SHARING_MODE_CONCURRENT)
    {
        bufferInfo.queueFamilyIndexCount = static_cast<uint32_t>(desc.queueFamilyIndexCount);
        bufferInfo.pQueueFamilyIndices = static_cast<const uint32_t*>(desc.pQueueFamilyIndices);
    }

    if (vkCreateBuffer(static_cast<VkDevice>(m_Device->GetHandle()), &bufferInfo, nullptr, &m_VkBuffer) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkBufferHandle::AllocBuffer]: failed to create vertex buffer!");
    }
    
    return true;
}

void ArisenEngine::RHI::RHIVkBufferHandle::FreeBufferHandle()
{
    if (m_VkBuffer != VK_NULL_HANDLE)
    {
        LOG_DEBUG("## Destroy Vulkan Buffer ##")
        vkDestroyBuffer(static_cast<VkDevice>(m_Device->GetHandle()), m_VkBuffer, nullptr);
    }
}

bool ArisenEngine::RHI::RHIVkBufferHandle::AllocBufferMemory(UInt32 memoryPropertiesBits)
{
    ASSERT(m_VkBuffer != VK_NULL_HANDLE);
    
    if (m_DeviceMemory != nullptr)
    {
        m_DeviceMemory->FreeDeviceMemory();
    }
    else
    {
        m_DeviceMemory = new RHIVkDeviceMemory(m_Device, m_VkBuffer);
    }
    
    return m_DeviceMemory->AllocDeviceMemory(memoryPropertiesBits);
}

void ArisenEngine::RHI::RHIVkBufferHandle::FreeBufferMemory()
{
    ASSERT(m_DeviceMemory != nullptr);
    m_DeviceMemory->FreeDeviceMemory();
}

void ArisenEngine::RHI::RHIVkBufferHandle::MemoryCopy(void const* src, const UInt32 offset)
{
    ASSERT(m_DeviceMemory != nullptr);
    ASSERT(m_VkBuffer != VK_NULL_HANDLE);
    m_DeviceMemory->MemoryCopy(src, offset, m_BufferSize);
}
