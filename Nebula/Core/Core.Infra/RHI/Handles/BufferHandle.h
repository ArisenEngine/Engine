#pragma once
#include "MemoryHandle.h"
#include "RHI/Enums/Memory/SharingMode.h"


namespace NebulaEngine::RHI
{
    typedef struct BufferAllocDesc
    {
        u32 createFlagBits;
        u64 size;
        u32 usage;
        SharingMode sharingMode;
        u32 queueFamilyIndexCount;
        const void* pQueueFamilyIndices;
    } BufferAllocDesc;

    
    class BufferHandle : public MemoryHandle
    {
    public:
        NO_COPY_NO_MOVE(BufferHandle)
        BufferHandle() = default;
        virtual bool AllocBufferHandle(BufferAllocDesc && desc) = 0;
        virtual void FreeBufferHandle() = 0;

        virtual bool AllocBufferMemory(u32 memoryPropertiesBits) = 0;
        virtual void FreeBufferMemory() = 0;
        virtual void MemoryCopy(void const* src, u32 offset) = 0;
        ~BufferHandle() noexcept override = default;

    public:
        const u64 BufferSize() const;
        
    protected:

        u64 m_BufferSize {0};
    };

    inline const u64 BufferHandle::BufferSize() const
    {
        return m_BufferSize;
    }
}
