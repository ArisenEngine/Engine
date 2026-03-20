#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_PACKAGE("com.arisen.rhi.core")
ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(EPresentMode)

    typedef enum EPresentMode
    {
        PRESENT_MODE_IMMEDIATE = 0,
        PRESENT_MODE_MAILBOX = 1,
        PRESENT_MODE_FIFO = 2,
        PRESENT_MODE_FIFO_RELAXED = 3,
        PRESENT_MODE_SHARED_DEMAND_REFRESH = 1000111000,
        PRESENT_MODE_SHARED_CONTINUOUS_REFRESH = 1000111001,
        PRESENT_MODE_MAX_ENUM = 0x7FFFFFFF
    } EPresentMode;
}

