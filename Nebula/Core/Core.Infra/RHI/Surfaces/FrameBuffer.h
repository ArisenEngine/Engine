#pragma once
#include "../../Common/CommandHeaders.h"
#include "RHI/Program/GPURenderPass.h"

namespace NebulaEngine::RHI
{
    class ImageView;

    typedef struct FrameBufferDesc
    {
        GPURenderPass& renderPass;
        UInt32 attachmentCount;
        void* attachments;
        UInt32 width;
        UInt32 height;
        UInt32 layerCount;
    } FrameBufferDesc;

    typedef struct RenderArea
    {
        uint32_t width;
        uint32_t height;
        int32_t offsetX;
        int32_t offsetY;
        
    } RenderArea;
    
    class FrameBuffer
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(FrameBuffer)
        explicit FrameBuffer(UInt32 maxFramesInFlight);;
        VIRTUAL_DECONSTRUCTOR(FrameBuffer)
        virtual void* GetHandle(UInt32 currentFrameIndex) = 0;
        const RenderArea GetRenderArea() const { return m_RenderArea; }
        virtual void SetAttachment(UInt32 frameIndex, ImageView* imageView, GPURenderPass* renderPass) = 0;
        virtual Format GetAttachFormat() = 0;
    protected:
        RenderArea m_RenderArea;
        UInt32 m_MaxFramesInFlight;
    };

    inline FrameBuffer::FrameBuffer(UInt32 maxFramesInFlight): m_RenderArea(), m_MaxFramesInFlight(maxFramesInFlight)
    {
    }
}
