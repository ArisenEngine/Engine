#pragma once
#include "../../Common/PrimitiveTypes.h"
#include "../Enums/Pipeline/EAccessFlag.h"
#include "../Enums/Pipeline/EPipelineStageFlag.h"
#include "../Enums/Pipeline/EPipelineStageFlag.h"
#include "../Handles/RHIHandle.h"

namespace ArisenEngine::RHI
{
    typedef struct RHIBufferMemoryBarrier
    {
        EAccessFlag          srcAccessMask;
        EAccessFlag          dstAccessMask;
        UInt32               srcQueueFamilyIndex;
        UInt32               dstQueueFamilyIndex;
        RHIBufferHandle      buffer;
        EPipelineStageFlag   srcStageMask;
        EPipelineStageFlag   dstStageMask;
    } RHIBufferMemoryBarrier;
}
