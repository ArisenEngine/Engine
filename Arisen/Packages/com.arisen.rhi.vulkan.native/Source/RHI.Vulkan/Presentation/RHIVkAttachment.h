#pragma once
#include "RHI/Presentation/RHIAttachment.h"

namespace ArisenEngine::RHI
{
    class RHIVkAttachment final : public RHIAttachment
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkAttachment)
        RHIVkAttachment(RHIAttachmentDesc&& desc);
        ~RHIVkAttachment() noexcept override;
    };
}
