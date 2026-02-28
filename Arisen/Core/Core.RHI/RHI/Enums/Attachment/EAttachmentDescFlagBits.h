#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")
namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(EAttachmentDescriptionFlagBits)
    typedef enum EAttachmentDescriptionFlagBits {
        ATTACHMENT_DESCRIPTION_MAY_ALIAS_BIT = 0x00000001,
        ATTACHMENT_DESCRIPTION_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
    } EAttachmentDescriptionFlagBits;
}
