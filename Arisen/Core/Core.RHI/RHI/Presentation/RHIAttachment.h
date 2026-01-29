#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Enums/Attachment/EAttachmentDescFlagBits.h"
#include "RHI/Enums/Attachment/EAttachmentLoadOp.h"
#include "RHI/Enums/Attachment/EAttachmentStoreOp.h"
#include "RHI/Enums/Image/ESampleCountFlagBits.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Enums/Image/EImageLayout.h"

namespace ArisenEngine::RHI
{
    struct RHIAttachmentDesc
    {
        EAttachmentDescriptionFlagBits flag;
        EFormat format;
        ESampleCountFlagBits sampleCount;
        EAttachmentLoadOp loadOp;
        EAttachmentStoreOp storeOp;
        EAttachmentLoadOp stencilLoadOp;
        EAttachmentStoreOp stencilStoreOp;
        EImageLayout initialLayout;
        EImageLayout finalLayout;
    };
    
    class RHIAttachment
    {
    public:
        NO_COPY_NO_MOVE(RHIAttachment)
        RHIAttachment() = default;
        VIRTUAL_DECONSTRUCTOR(RHIAttachment)
        virtual void* GetAttachmentReference() = 0;
    };
}

