#include "RHIVkBufferHandle.h"
#include "../VkInitializer.h"
#include "../Memory/RHIVkDeviceMemory.h"
#include "../Devices/RHIVkDevice.h"
#include "RHI/Utils/RHIResourceRegistry.h"

namespace ArisenEngine::RHI {
    struct DeferredVkBuffer {
        VkDevice device;
        VkBuffer buffer;
        ~DeferredVkBuffer() {
            if (device != VK_NULL_HANDLE && buffer != VK_NULL_HANDLE) {
                vkDestroyBuffer(device, buffer, nullptr);
            }
        }
    };
}

ArisenEngine::RHI::RHIVkBufferHandle::RHIVkBufferHandle(RHIDevice* device)
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
    if (m_VkBuffer == VK_NULL_HANDLE)
    {
        LOG_FATAL_AND_THROW("[RHIVkBufferHandle::GetHandle] VkBuffer is VK_NULL_HANDLE for buffer: " + m_Name);
    }
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

    auto* vkDevice = static_cast<RHIVkDevice*>(m_Device);
    auto* registry = vkDevice->GetResourceRegistry();
    
    auto* deferred = new DeferredVkBuffer{
        static_cast<VkDevice>(vkDevice->GetHandle()),
        m_VkBuffer
    };

    m_RHIHandle = registry->Create(MakeDeferredDeleteItem(deferred));

    return true;
}

void ArisenEngine::RHI::RHIVkBufferHandle::FreeBufferHandle()
{
    if (m_VkBuffer != VK_NULL_HANDLE)
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(m_Device);
        auto* registry = vkDevice->GetResourceRegistry();
        
        // Use a generic graphics queue ticket for manual release.
        // In real usage, the CommandBuffer will Retain this.
        registry->Release(m_RHIHandle, RHIQueueType::Graphics, vkDevice->GetCompletedSubmitId());
        
        m_VkBuffer = VK_NULL_HANDLE;
        m_RHIHandle = RHIResourceHandle::Invalid();
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
