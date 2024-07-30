#pragma once
#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/Attachment/AttachmentDescFlagBits.h"
#include "RHI/Enums/Attachment/AttachmentLoadOp.h"
#include "RHI/Enums/Attachment/AttachmentStoreOp.h"
#include "RHI/Enums/Attachment/SampleCountFlagBits.h"
#include "RHI/Enums/Image/Format.h"
#include "RHI/Enums/Image/ImageLayout.h"

namespace NebulaEngine::RHI
{
    struct AttachmentDesc
    {
        AttachmentDescriptionFlagBits flag;
        Format format;
        SampleCountFlagBits sampleCount;
        AttachmentLoadOp loadOp;
        AttachmentStoreOp storeOp;
        AttachmentLoadOp stencilLoadOp;
        AttachmentStoreOp stencilStoreOp;
        ImageLayout initialLayout;
        ImageLayout finalLayout;
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
