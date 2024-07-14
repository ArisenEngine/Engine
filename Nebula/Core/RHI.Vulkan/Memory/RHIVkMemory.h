#pragma once
#include "RHI/Memory/RawMemory.h"

namespace NebulaEngine::RHI
{
    class RHIVkMemory final : RawMemory
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkMemory)
        VIRTUAL_DECONSTRUCTOR(RHIVkMemory)
        
    };
}
