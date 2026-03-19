#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_PACKAGE("com.arisen.rhi.core")
ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(EColorComponentFlagBits)

    typedef enum EColorComponentFlagBits
    {
        COLOR_COMPONENT_R_BIT = 0x00000001,
        COLOR_COMPONENT_G_BIT = 0x00000002,
        COLOR_COMPONENT_B_BIT = 0x00000004,
        COLOR_COMPONENT_A_BIT = 0x00000008,
        COLOR_COMPONENT_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
    } EColorComponentFlagBits;
}

