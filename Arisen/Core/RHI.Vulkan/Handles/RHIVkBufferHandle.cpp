#include "RHIVkBufferHandle.h"
#include "../VkInitializer.h"
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


bool ArisenEngine::RHI::RHIVkBufferHandle::AllocBufferHandle(BufferDescriptor && desc)
{
    VkBufferCreateInfo bufferInfo = BufferCreateInfo(
        desc.createFlagBits,
        desc.size, 
        desc.usage,
        desc.sharingMode,
        desc.queueFamilyIndexCount,
        desc.pQueueFamilyIndices);
    
    m_BufferSize = desc.size;
    
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
        LOG_DEBUG("## Destroy Vulkan Buffer:"+ m_Name +" ##")
        vkDestroyBuffer(static_cast<VkDevice>(m_Device->GetHandle()), m_VkBuffer, nullptr);
    }
}

bool ArisenEngine::RHI::RHIVkBufferHandle::AllocDeviceMemory(UInt32 memoryPropertiesBits)
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

void ArisenEngine::RHI::RHIVkBufferHandle::MemoryCopy(void const* src, const UInt32 offset)
{
    ASSERT(m_DeviceMemory != nullptr);
    ASSERT(m_VkBuffer != VK_NULL_HANDLE);
    m_DeviceMemory->MemoryCopy(src, offset, m_BufferSize);
}
