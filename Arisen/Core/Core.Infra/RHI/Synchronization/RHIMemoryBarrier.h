#pragma once
#include "../Enums/Pipeline/EAccessFlag.h"

namespace ArisenEngine::RHI
{
    typedef struct RHIMemoryBarrier
    {
        EAccessFlagBits srcAccessMask;
        EAccessFlagBits dstAccessMask;
        
    } RHIMemoryBarrier;
}
