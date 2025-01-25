#pragma once
#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/Attachment/AttachmentDescFlagBits.h"
#include "RHI/Enums/Attachment/AttachmentLoadOp.h"
#include "RHI/Enums/Attachment/AttachmentStoreOp.h"
#include "RHI/Enums/Attachment/ESampleCountFlagBits.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Enums/Image/EImageLayout.h"

namespace ArisenEngine::RHI
{
    struct AttachmentDesc
    {
        AttachmentDescriptionFlagBits flag;
        EFormat format;
        ESampleCountFlagBits sampleCount;
        AttachmentLoadOp loadOp;
        AttachmentStoreOp storeOp;
        AttachmentLoadOp stencilLoadOp;
        AttachmentStoreOp stencilStoreOp;
        EImageLayout initialLayout;
        EImageLayout finalLayout;
    };
    
    class Attachment
    {
    public:
        NO_COPY_NO_MOVE(Attachment)
        Attachment() = default;
        VIRTUAL_DECONSTRUCTOR(Attachment)
        virtual void* GetAttachmentReference() = 0;
    };
}
