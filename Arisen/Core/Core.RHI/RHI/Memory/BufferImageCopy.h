#pragma once
#include "./ImageSubresourceLayers.h"

namespace ArisenEngine::RHI
{
    typedef struct BufferImageCopy
    {
        UInt64 bufferOffset;
          UInt32 bufferRowLength;
          UInt32 bufferImageHeight;
          ImageSubresourceLayers imageSubresource;
      SInt32 offsetX; SInt32 offsetY; SInt32 offsetZ;
      UInt32 width; UInt32 height; UInt32 depth;  
    } BufferImageCopy;
}
