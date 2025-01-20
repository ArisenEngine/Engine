#pragma once
#include "MemoryHandle.h"
#include "RHI/Enums/Memory/SharingMode.h"


namespace NebulaEngine::RHI
{
    typedef struct BufferAllocDesc
    {
        UInt32 createFlagBits;
        UInt64 size;
        UInt32 usage;
        SharingMode sharingMode;
        UInt32 queueFamilyIndexCount;
        const void* pQueueFamilyIndices;
    } BufferAllocDesc;

    
    class BufferHandle : public MemoryHandle
    {
    public:
        NO_COPY_NO_MOVE(BufferHandle)
        BufferHandle() = default;
        virtual bool AllocBufferHandle(BufferAllocDesc && desc) = 0;
        virtual void FreeBufferHandle() = 0;

        virtual bool AllocBufferMemory(UInt32 memoryPropertiesBits) = 0;
        virtual void FreeBufferMemory() = 0;
        virtual void MemoryCopy(void const* src, UInt32 offset) = 0;
        ~BufferHandle() noexcept override = default;

    public:
        const UInt64 BufferSize() const;
        
    protected:

        UInt64 m_BufferSize {0};
    };

    inline const UInt64 BufferHandle::BufferSize() const
    {
        return m_BufferSize;
    }
}
