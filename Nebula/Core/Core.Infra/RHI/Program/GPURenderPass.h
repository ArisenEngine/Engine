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

        enum class ERenderPassState : u8
        {
            NotAllocated,
            Allocated,
            AttachDone
        };
        
        NO_COPY_NO_MOVE(GPURenderPass)
        GPURenderPass() = default;
        VIRTUAL_DECONSTRUCTOR(GPURenderPass)
        virtual void* GetHandle() = 0;

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
        virtual void AllocRenderPass() = 0;
        virtual void FreeRenderPass() = 0;
        
    protected:

        ERenderPassState m_State { ERenderPassState::NotAllocated };
    };
    
}
