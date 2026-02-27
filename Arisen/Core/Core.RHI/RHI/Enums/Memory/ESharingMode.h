#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")
namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(ESharingMode)
    typedef enum ESharingMode {
        SHARING_MODE_EXCLUSIVE = 0,
        SHARING_MODE_CONCURRENT = 1,
        SHARING_MODE_MAX_ENUM = 0x7FFFFFFF
    } ESharingMode;
}
