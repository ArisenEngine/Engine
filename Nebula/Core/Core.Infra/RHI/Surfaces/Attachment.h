#pragma once
#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/AttachmentDescFlagBits.h"
#include "RHI/Enums/AttachmentLoadOp.h"
#include "RHI/Enums/AttachmentStoreOp.h"
#include "RHI/Enums/ImageLayout.h"
#include "RHI/Enums/SampleCountFlagBits.h"

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
        NO_COPY_NO_MOVE_NO_DEFAULT(Attachment)
        VIRTUAL_DECONSTRUCTOR(Attachment)
        virtual void* GetAttachmentReference() = 0;
    };
}
