#pragma once
#include "Base/FoundationMinimal.h"
#include "RHIRenderPass.h"
#include "../Handles/RHIHandle.h"
#include "RHI/Definitions/CoreRHICommon.h"

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

    class RHI_DLL RHIFrameBuffer
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIFrameBuffer)
        explicit RHIFrameBuffer(UInt32 maxFramesInFlight);;
        VIRTUAL_DECONSTRUCTOR(RHIFrameBuffer)
        virtual void* GetHandle(UInt32 currentFrameIndex) = 0;
        const RHIRenderArea GetRenderArea() const { return m_RenderArea; }
        virtual EFormat GetAttachFormat() = 0;

    protected:
        RHIRenderArea m_RenderArea;
        UInt32 m_MaxFramesInFlight;
    };
}
