#include "RHIVkBufferHandle.h"

#include "../Memory/RHIVkDeviceMemory.h"

NebulaEngine::RHI::RHIVkBufferHandle::RHIVkBufferHandle(Device* device)
: BufferHandle() , m_Device(device)
{
    
}

NebulaEngine::RHI::RHIVkBufferHandle::~RHIVkBufferHandle() noexcept
{
    LOG_DEBUG("[RHIVkBufferHandle::~RHIVkBufferHandle]: ~RHIVkBufferHandle");
    FreeBufferHandle();
}

void* NebulaEngine::RHI::RHIVkBufferHandle::GetHandle() const
{
    ASSERT(m_VkBuffer != VK_NULL_HANDLE);
    return m_VkBuffer;
}


bool NebulaEngine::RHI::RHIVkBufferHandle::AllocBufferHandle(BufferAllocDesc && desc)
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

void NebulaEngine::RHI::RHIVkBufferHandle::FreeBufferHandle()
{
    if (m_VkBuffer != VK_NULL_HANDLE)
    {
        LOG_DEBUG("## Destroy Vulkan Buffer ##")
        vkDestroyBuffer(static_cast<VkDevice>(m_Device->GetHandle()), m_VkBuffer, nullptr);
    }
}

bool NebulaEngine::RHI::RHIVkBufferHandle::AllocBufferMemory(u32 memoryPropertiesBits)
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

void NebulaEngine::RHI::RHIVkBufferHandle::FreeBufferMemory()
{
    ASSERT(m_DeviceMemory != nullptr);
    m_DeviceMemory->FreeDeviceMemory();
}

void NebulaEngine::RHI::RHIVkBufferHandle::MemoryCopy(void const* src, const u32 offset)
{
    ASSERT(m_DeviceMemory != nullptr);
    ASSERT(m_VkBuffer != VK_NULL_HANDLE);
    m_DeviceMemory->MemoryCopy(src, offset, m_BufferSize);
}
