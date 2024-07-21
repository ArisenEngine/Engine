#pragma once
#include "RHI/Surfaces/Attachment.h"

namespace NebulaEngine::RHI
{
    class RHIVkAttachment final : public Attachment
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkAttachment)
        ~RHIVkAttachment() noexcept override;
    };
}
