#include "Base/BindingMacros.h"

#pragma once

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(CompositeAlphaFlagBits)

    typedef enum CompositeAlphaFlagBits
    {
        COMPOSITE_ALPHA_OPAQUE_BIT = 0x00000001,
        COMPOSITE_ALPHA_PRE_MULTIPLIED_BIT = 0x00000002,
        COMPOSITE_ALPHA_POST_MULTIPLIED_BIT = 0x00000004,
        COMPOSITE_ALPHA_INHERIT_BIT = 0x00000008,
        COMPOSITE_ALPHA_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
    } CompositeAlphaFlagBits;
}
