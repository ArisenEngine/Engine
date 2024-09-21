#pragma once
#include "../../Common/CommandHeaders.h"

namespace NebulaEngine::RHI
{
    class BufferHandle;
}

namespace NebulaEngine::RHI
{
    class DeviceMemory
    {
    public:
        NO_COPY_NO_MOVE(DeviceMemory)
        DeviceMemory() = default;
        virtual ~DeviceMemory() noexcept;
        virtual void* GetHandle() const = 0;
        virtual bool AllocDeviceMemory(u32 memoryPropertiesBits) = 0;
        virtual bool AllocDeviceMemory(u32 memoryPropertiesBits, Containers::Vector<BufferHandle*> handles) = 0;
        virtual void FreeDeviceMemory() = 0;
        virtual void MemoryCopy(void const* src, const u32 offset, const u32 size) = 0;
        const u32 GetBytes() const;
        const u32 GetAlignment() const;
        const u32 GetMemoryTypeBits() const;

    protected:

        u32 m_TotalBytes { 0 };
        u32 m_Alignment { 0 };
        u32 m_MemoryTypeBits { 0 };
    };
    
    inline DeviceMemory::~DeviceMemory() noexcept
    {
        m_TotalBytes = 0;
    }

    inline const u32 DeviceMemory::GetBytes() const
    {
        return m_TotalBytes;
    }

    inline const u32 DeviceMemory::GetAlignment() const
    {
        return m_Alignment;
    }

    inline const u32 DeviceMemory::GetMemoryTypeBits() const
    {
        return m_MemoryTypeBits;
    }
}
