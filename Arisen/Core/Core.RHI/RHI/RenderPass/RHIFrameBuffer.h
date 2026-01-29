#pragma once
#include "Base/FoundationMinimal.h"
#include "RHIRenderPass.h"
#include "../Handles/RHIHandle.h"

namespace ArisenEngine::RHI
{

    typedef struct RHIFrameBufferDesc
    {
        RHIRenderPass& renderPass;
        UInt32 attachmentCount;
        void* attachments;
        UInt32 width;
        UInt32 height;
        UInt32 layerCount;
    } RHIFrameBufferDesc;

    typedef struct RHIRenderArea
    {
        uint32_t width;
        uint32_t height;
        int32_t offsetX;
        int32_t offsetY;
        
    } RHIRenderArea;
    
    class RHIFrameBuffer
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIFrameBuffer)
        explicit RHIFrameBuffer(UInt32 maxFramesInFlight);;
        VIRTUAL_DECONSTRUCTOR(RHIFrameBuffer)
        virtual void* GetHandle(UInt32 currentFrameIndex) = 0;
        const RHIRenderArea GetRenderArea() const { return m_RenderArea; }
        virtual void SetAttachment(UInt32 frameIndex, RHIImageViewHandle imageView, RHIRenderPass* renderPass) = 0;
        virtual void SetAttachments(UInt32 frameIndex, const Containers::Vector<RHIImageViewHandle>& imageViews, RHIRenderPass* renderPass) = 0;
        virtual EFormat GetAttachFormat() = 0;
    protected:
        RHIRenderArea m_RenderArea;
        UInt32 m_MaxFramesInFlight;
    };

    inline RHIFrameBuffer::RHIFrameBuffer(UInt32 maxFramesInFlight): m_RenderArea(), m_MaxFramesInFlight(maxFramesInFlight)
    {
    }
}

