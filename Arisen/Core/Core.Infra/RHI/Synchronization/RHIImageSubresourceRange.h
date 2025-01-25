#pragma once
#include "../../Common/PrimitiveTypes.h"
#include "../Enums/Image/EImageAspectFlagBits.h"

namespace ArisenEngine::RHI
{
    typedef struct RHIImageSubresourceRange {
        EImageAspectFlagBits    aspectMask;
        UInt32              baseMipLevel;
        UInt32              levelCount;
        UInt32              baseArrayLayer;
        UInt32              layerCount;
    } RHIImageSubresourceRange;
}
