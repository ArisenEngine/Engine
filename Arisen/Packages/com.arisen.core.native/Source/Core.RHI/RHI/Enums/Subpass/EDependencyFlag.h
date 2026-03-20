#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_PACKAGE("com.arisen.rhi.core")
ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(EDependencyFlagBits)

    // can be multiple bits
    typedef enum EDependencyFlagBits
    {
        DEPENDENCY_BY_REGION_BIT = 0x00000001,
        DEPENDENCY_DEVICE_GROUP_BIT = 0x00000004,
        DEPENDENCY_VIEW_LOCAL_BIT = 0x00000002,
        DEPENDENCY_FEEDBACK_LOOP_BIT_EXT = 0x00000008,
        DEPENDENCY_VIEW_LOCAL_BIT_KHR = DEPENDENCY_VIEW_LOCAL_BIT,
        DEPENDENCY_DEVICE_GROUP_BIT_KHR = DEPENDENCY_DEVICE_GROUP_BIT,
        DEPENDENCY_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
    } EDependencyFlagBits;
}

