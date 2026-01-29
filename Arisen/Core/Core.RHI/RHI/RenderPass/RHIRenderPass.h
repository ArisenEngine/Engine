#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Enums/Attachment/EAttachmentLoadOp.h"
#include "RHI/Enums/Attachment/EAttachmentStoreOp.h"
#include "RHI/Enums/Image/ESampleCountFlagBits.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Enums/Image/EImageLayout.h"

namespace ArisenEngine::RHI
{
    class RHISubPass;

    class RHIRenderPass
    {
        
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIRenderPass)
        RHIRenderPass(UInt32 maxFramesInFlight);
        VIRTUAL_DECONSTRUCTOR(RHIRenderPass)
        virtual void* GetHandle(UInt32 frameIndex) = 0;

        virtual void AddAttachmentAction(
            EFormat format,
            ESampleCountFlagBits sample,
            EAttachmentLoadOp colorLoadOp, EAttachmentStoreOp colorStoreOp,
            EAttachmentLoadOp stencilLoadOp, EAttachmentStoreOp stencilStoreOp,
            EImageLayout initialLayout, EImageLayout finalLayout
        ) = 0;

        virtual UInt32 GetAttachmentCount() = 0;

        virtual RHISubPass* AddSubPass() = 0;
        virtual UInt32 GetSubPassCount() = 0;
        virtual void AllocRenderPass(UInt32 frameIndex) = 0;
        virtual void FreeRenderPass(UInt32 frameIndex) = 0;
        virtual void FreeAllRenderPasses() = 0;
        
    protected:
        UInt32 m_MaxFramesInFlight;
    };

    inline RHIRenderPass::RHIRenderPass(UInt32 maxFramesInFlight):m_MaxFramesInFlight(maxFramesInFlight)
    {
            
    }
}

