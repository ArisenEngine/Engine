#pragma once
#include "MemoryHandle.h"


namespace NebulaEngine::RHI
{
    typedef struct BufferAllocDesc
    {
        u32 createFlagBits;
        u32 size;
    } BufferAllocDesc;

    
    class BufferHandle : public MemoryHandle
    {
    public:
        NO_COPY_NO_MOVE(BufferHandle)
        BufferHandle() : MemoryHandle(true)
        {
            
        }
        virtual bool AllocBuffer(BufferAllocDesc && desc) = 0;
        ~BufferHandle() noexcept override = default;
    };
}
