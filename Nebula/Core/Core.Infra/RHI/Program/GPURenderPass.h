#pragma once
#include "RHI/Enums/Attachment/AttachmentLoadOp.h"
#include "RHI/Enums/Attachment/AttachmentStoreOp.h"
#include "RHI/Enums/Attachment/ESampleCountFlagBits.h"
#include "RHI/Enums/Image/Format.h"
#include "RHI/Enums/Image/ImageLayout.h"

namespace NebulaEngine::RHI
{
    class GPUSubPass;

    class GPURenderPass
    {
        
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(GPURenderPass)
        GPURenderPass(u32 maxFramesInFlight);
        VIRTUAL_DECONSTRUCTOR(GPURenderPass)
        virtual void* GetHandle(u32 frameIndex) = 0;

        virtual void AddAttachmentAction(
            Format format,
            ESampleCountFlagBits sample,
            AttachmentLoadOp colorLoadOp, AttachmentStoreOp colorStoreOp,
            AttachmentLoadOp stencilLoadOp, AttachmentStoreOp stencilStoreOp,
            ImageLayout initialLayout, ImageLayout finalLayout
        ) = 0;

        virtual u32 GetAttachmentCount() = 0;

        virtual GPUSubPass* AddSubPass() = 0;
        virtual u32 GetSubPassCount() = 0;
        virtual void AllocRenderPass(u32 frameIndex) = 0;
        virtual void FreeRenderPass(u32 frameIndex) = 0;
        virtual void FreeAllRenderPasses() = 0;
        
    protected:
        u32 m_MaxFramesInFlight;
    };

    inline GPURenderPass::GPURenderPass(u32 maxFramesInFlight):m_MaxFramesInFlight(maxFramesInFlight)
    {
            
    }
}
