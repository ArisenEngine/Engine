#pragma once
#include "../../Common/CommandHeaders.h"
#include "MemoryHandle.h"

namespace NebulaEngine::RHI
{
    class ImageHandle : public MemoryHandle
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(ImageHandle)
        ImageHandle(bool needRecycleMemory) : MemoryHandle(needRecycleMemory)
        {
            
        }
        ~ImageHandle() noexcept override = default;
    };
}
