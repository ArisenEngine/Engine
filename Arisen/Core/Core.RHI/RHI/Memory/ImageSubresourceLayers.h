#pragma once
#include "../Enums/Image/EImageAspectFlagBits.h"
#include "Base/PrimitiveTypes.h"

namespace ArisenEngine::RHI
{
    typedef struct ImageSubresourceLayers
    {
        RHI::EImageAspectFlagBits    aspectMask;
        UInt32              mipLevel;
        UInt32              baseArrayLayer;
        UInt32              layerCount;
        
    } ImageSubresourceLayers;
}

