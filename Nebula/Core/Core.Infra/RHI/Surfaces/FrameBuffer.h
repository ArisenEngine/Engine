#pragma once
#include "../../Common/CommandHeaders.h"
#include "RHI/Program/GPURenderPass.h"

namespace NebulaEngine::RHI
{
    class ImageView;

    typedef struct FrameBufferDesc
    {
        GPURenderPass& renderPass;
        u32 attachmentCount;
        void* attachments;
        u32 width;
        u32 height;
        u32 layerCount;
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
        NO_COPY_NO_MOVE(FrameBuffer)
        FrameBuffer() = default;
        VIRTUAL_DECONSTRUCTOR(FrameBuffer)
        virtual void* GetHandle() = 0;
        const RenderArea GetRenderArea() const { return m_RenderArea; }
        virtual void SetAttachment(ImageView* imageView, GPURenderPass* renderPass) = 0;
        virtual Format GetAttachFormat() = 0;
    protected:
        RenderArea m_RenderArea;
    };
}
