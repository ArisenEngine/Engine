#pragma once
#include "../Allocation/RHIImageSubresourceLayers.h"
#include "../Core/RHICommon.h"

namespace ArisenEngine::RHI
{
    typedef struct RHIImageCopy
    {
        RHIImageSubresourceLayers srcSubresource;
        RHIOffset3D srcOffset;
        RHIImageSubresourceLayers dstSubresource;
        RHIOffset3D dstOffset;
        RHIExtent3D extent;
    } RHIImageCopy;
}
