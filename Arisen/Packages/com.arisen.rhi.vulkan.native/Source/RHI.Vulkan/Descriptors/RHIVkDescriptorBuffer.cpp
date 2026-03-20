#include "RHIVkDescriptorBuffer.h"
#include "../Core/RHIVkDevice.h"
#include "../Core/RHIVkFactory.h"
#include "RHI/Enums/Buffer/EBufferUsage.h"
#include "RHI/Enums/Memory/ERHIMemoryUsage.h"
#include "../Core/RHIVkFactory.h"

namespace ArisenEngine::RHI
{
    RHIVkDescriptorBuffer::RHIVkDescriptorBuffer(RHIVkDevice* device)
        : m_Device(device)
    {
    }

    RHIVkDescriptorBuffer::~RHIVkDescriptorBuffer()
    {
        Shutdown();
    }

    bool RHIVkDescriptorBuffer::Initialize(const RHIVkDescriptorBufferDesc& desc)
    {
        if (desc.size == 0) return false;

        UInt32 usageBits = BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
        if (desc.isSamplerBuffer) usageBits |= BUFFER_USAGE_SAMPLER_DESCRIPTOR_BUFFER_BIT_EXT;
        if (desc.isResourceBuffer) usageBits |= BUFFER_USAGE_RESOURCE_DESCRIPTOR_BUFFER_BIT_EXT;

        RHIBufferDescriptor bufferDesc{};
        bufferDesc.size = desc.size;
        bufferDesc.usage = usageBits;
        bufferDesc.memoryUsage = ERHIMemoryUsage::Upload; // Usually we want host-visible memory for descriptor buffers to update them dynamically

        m_BufferHandle = m_Device->GetFactory()->CreateBuffer(std::move(bufferDesc));
        if (!m_BufferHandle.IsValid())
        {
            LOG_ERROR("[RHIVkDescriptorBuffer::Initialize]: Failed to create backing buffer.");
            return false;
        }

        m_DeviceAddress = m_Device->GetFactory()->GetBufferDeviceAddress(m_BufferHandle);
        m_MappedPointer = m_Device->GetFactory()->MapBuffer(m_BufferHandle);
        m_Size = desc.size;

        return true;
    }

    void RHIVkDescriptorBuffer::Shutdown()
    {
        if (m_BufferHandle.IsValid())
        {
            if (m_MappedPointer)
            {
                m_Device->GetFactory()->UnmapBuffer(m_BufferHandle);
                m_MappedPointer = nullptr;
            }
            m_Device->GetFactory()->ReleaseBuffer(m_BufferHandle);
            m_BufferHandle = RHIBufferHandle{};
            m_DeviceAddress = 0;
            m_Size = 0;
        }
    }
}
