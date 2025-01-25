#pragma once
#include "RHIImageSubresourceRange.h"
#include "../Handles/ImageHandle.h"
#include "../Enums/Pipeline/EAccessFlag.h"
#include "../Enums/Image/EImageLayout.h"
namespace ArisenEngine::RHI
{
    typedef struct RHIImageMemoryBarrier {
        EAccessFlagBits              srcAccessMask;
        EAccessFlagBits              dstAccessMask;
        EImageLayout              oldLayout;
        EImageLayout              newLayout;
        UInt32                       srcQueueFamilyIndex;
        UInt32                      dstQueueFamilyIndex;
        ImageHandle*               image;
        RHIImageSubresourceRange    subresourceRange;
    } RHIImageMemoryBarrier;
}
