#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(ESubpassContents)

    typedef enum ESubpassContents
    {
        SUBPASS_CONTENTS_INLINE = 0,
        SUBPASS_CONTENTS_SECONDARY_COMMAND_BUFFERS = 1,
        SUBPASS_CONTENTS_MAX_ENUM = 0x7FFFFFFF
    } ESubpassContents;
}
