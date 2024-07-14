#pragma once
#include "../../Common/CommandHeaders.h"

namespace NebulaEngine::RHI
{
    class RawMemory
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RawMemory)
        VIRTUAL_DECONSTRUCTOR(RawMemory)
        virtual void* GetHandle() const = 0;
    };
}
