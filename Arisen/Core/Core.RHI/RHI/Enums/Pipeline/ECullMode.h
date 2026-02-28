#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(ECullModeFlagBits)

    typedef enum ECullModeFlagBits
    {
        CULL_MODE_NONE = 0,
        CULL_MODE_FRONT_BIT = 0x00000001,
        CULL_MODE_BACK_BIT = 0x00000002,
        CULL_MODE_FRONT_AND_BACK = 0x00000003,
        CULL_MODE_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
    } ECullModeFlagBits;
}
