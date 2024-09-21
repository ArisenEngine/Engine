#pragma once
#include "MemoryHandle.h"
#include "RHI/Enums/Memory/EBufferUsage.h"


namespace NebulaEngine::RHI
{
    typedef struct BufferAllocDesc
    {
        u32 createFlagBits;
        u64 size;
        EBufferUsageFlagBits usage;
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
    };
}
