#pragma once
#include "../../Common/CommandHeaders.h"
#include "RHI/Program/GPURenderPass.h"

namespace NebulaEngine::RHI
{
    struct FrameBufferDesc
    {
        GPURenderPass& renderPass;
        u32 attachmentCount;
        void* attachments;
        u32 width;
        u32 height;
        u32 layerCount;
    };
    
    class FrameBuffer
    {
    public:
        NO_COPY_NO_MOVE(FrameBuffer)
        FrameBuffer() = default;
        VIRTUAL_DECONSTRUCTOR(FrameBuffer)
    };
}
