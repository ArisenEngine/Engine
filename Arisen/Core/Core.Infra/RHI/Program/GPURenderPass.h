#pragma once
#include "RHI/Enums/Attachment/AttachmentLoadOp.h"
#include "RHI/Enums/Attachment/AttachmentStoreOp.h"
#include "RHI/Enums/Image/ESampleCountFlagBits.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Enums/Image/EImageLayout.h"

namespace ArisenEngine::RHI
{
    class GPUSubPass;

    class GPURenderPass
    {
        
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(GPURenderPass)
        GPURenderPass(UInt32 maxFramesInFlight);
        VIRTUAL_DECONSTRUCTOR(GPURenderPass)
        virtual void* GetHandle(UInt32 frameIndex) = 0;

        virtual void AddAttachmentAction(
            EFormat format,
            ESampleCountFlagBits sample,
            AttachmentLoadOp colorLoadOp, AttachmentStoreOp colorStoreOp,
            AttachmentLoadOp stencilLoadOp, AttachmentStoreOp stencilStoreOp,
            EImageLayout initialLayout, EImageLayout finalLayout
        ) = 0;

        virtual UInt32 GetAttachmentCount() = 0;

        virtual GPUSubPass* AddSubPass() = 0;
        virtual UInt32 GetSubPassCount() = 0;
        virtual void AllocRenderPass(UInt32 frameIndex) = 0;
        virtual void FreeRenderPass(UInt32 frameIndex) = 0;
        virtual void FreeAllRenderPasses() = 0;
        
    protected:
        UInt32 m_MaxFramesInFlight;
    };

    inline GPURenderPass::GPURenderPass(UInt32 maxFramesInFlight):m_MaxFramesInFlight(maxFramesInFlight)
    {
            
    }
}
