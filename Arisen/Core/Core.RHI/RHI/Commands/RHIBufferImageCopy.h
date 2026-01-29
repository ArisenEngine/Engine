#pragma once
#include "../Allocation/RHIImageSubresourceLayers.h"

namespace ArisenEngine::RHI
{
    typedef struct RHIBufferImageCopy
    {
        UInt64 bufferOffset;
          UInt32 bufferRowLength;
          UInt32 bufferImageHeight;
          RHIImageSubresourceLayers imageSubresource;
      SInt32 offsetX; SInt32 offsetY; SInt32 offsetZ;
      UInt32 width; UInt32 height; UInt32 depth;  
    } RHIBufferImageCopy;
}
