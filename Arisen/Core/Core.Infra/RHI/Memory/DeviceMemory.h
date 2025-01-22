#pragma once
#include "../../Common/CommandHeaders.h"

namespace ArisenEngine::RHI
{
    class BufferHandle;
}

namespace ArisenEngine::RHI
{
    class DeviceMemory
    {
    public:
        NO_COPY_NO_MOVE(DeviceMemory)
        DeviceMemory() = default;
        virtual ~DeviceMemory() noexcept;
        virtual void* GetHandle() const = 0;
        virtual bool AllocDeviceMemory(UInt32 memoryPropertiesBits) = 0;
        virtual bool AllocDeviceMemory(UInt32 memoryPropertiesBits, Containers::Vector<BufferHandle*> handles) = 0;
        virtual void FreeDeviceMemory() = 0;
        virtual void MemoryCopy(void const* src, const UInt32 offset, const UInt32 size) = 0;
        const UInt64 GetTotalBytes() const;
        const UInt32 GetAlignment() const;
        const UInt32 GetMemoryTypeBits() const;

    protected:
        UInt64 m_TotalBytes { 0 };
        UInt32 m_Alignment { 0 };
        UInt32 m_MemoryTypeBits { 0 };
    };
    
    inline DeviceMemory::~DeviceMemory() noexcept
    {
        m_TotalBytes = 0;
    }

    /// 
    /// @return total bytes (including alignment bit)
    inline const UInt64 DeviceMemory::GetTotalBytes() const
    {
        return m_TotalBytes;
    }

    inline const UInt32 DeviceMemory::GetAlignment() const
    {
        return m_Alignment;
    }

    

    inline const UInt32 DeviceMemory::GetMemoryTypeBits() const
    {
        return m_MemoryTypeBits;
    }
}
