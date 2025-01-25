#pragma once
#include "MemoryHandle.h"
#include "RHI/Enums/Memory/ESharingMode.h"


namespace ArisenEngine::RHI
{
    typedef struct BufferDescriptor
    {
        UInt32 createFlagBits;
        UInt64 size;
        UInt32 usage;
        ESharingMode sharingMode;
        UInt32 queueFamilyIndexCount;
        const void* pQueueFamilyIndices;
    } BufferDescriptor;

    
    class BufferHandle : public MemoryHandle
    {
    public:
        NO_COPY_NO_MOVE(BufferHandle)
        BufferHandle() = default;
        virtual bool AllocBufferHandle(BufferDescriptor && desc) = 0;
        virtual void FreeBufferHandle() = 0;
        bool AllocDeviceMemory(UInt32 memoryPropertiesBits) override = 0;
        virtual void MemoryCopy(void const* src, UInt32 offset) = 0;
        ~BufferHandle() noexcept override = default;

        
    public:
        const UInt64 BufferSize() const;

        void SetBufferOffsetRange(UInt64&& offset, UInt64&& range)
        {
            m_BufferOffset = offset;
            m_BufferRange = range;
        }
        const UInt64 Offset() const;
        const UInt64 Range() const;

    protected:

        UInt64 m_BufferSize {0};
        UInt64 m_BufferOffset{0};
        UInt64 m_BufferRange{0};
    };

    inline const UInt64 BufferHandle::BufferSize() const
    {
        return m_BufferSize;
    }

    inline const UInt64 BufferHandle::Offset() const
    {
        return m_BufferOffset;
    }
    
    inline const UInt64 BufferHandle::Range() const
    {
        return m_BufferRange;
    }
}
