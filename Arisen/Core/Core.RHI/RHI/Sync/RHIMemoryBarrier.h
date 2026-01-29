#pragma once
#include "../Enums/Pipeline/EAccessFlag.h"
#include "../Enums/Pipeline/EPipelineStageFlag.h"

namespace ArisenEngine::RHI
{
    typedef struct RHIMemoryBarrier
    {
        EAccessFlag srcAccessMask;
        EAccessFlag dstAccessMask;
        EPipelineStageFlag srcStageMask;
        EPipelineStageFlag dstStageMask;
        
    } RHIMemoryBarrier;
}
