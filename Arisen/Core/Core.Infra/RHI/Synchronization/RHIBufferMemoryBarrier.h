#pragma once
#include "../../Common/PrimitiveTypes.h"
#include "../Enums/Pipeline/EAccessFlag.h"
#include "../Handles/BufferHandle.h"

namespace ArisenEngine::RHI
{
    typedef struct RHIBufferMemoryBarrier
    {
        EAccessFlag      srcAccessMask;
        EAccessFlag      dstAccessMask;
        UInt32               srcQueueFamilyIndex;
        UInt32               dstQueueFamilyIndex;
        BufferHandle*        buffer;
    } RHIBufferMemoryBarrier;
}
