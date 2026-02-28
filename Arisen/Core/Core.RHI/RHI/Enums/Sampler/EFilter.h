#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(EFilter)

    typedef enum EFilter
    {
        FILTER_NEAREST = 0,
        FILTER_LINEAR = 1,
        FILTER_CUBIC_EXT = 1000015000,
        FILTER_CUBIC_IMG = FILTER_CUBIC_EXT,
        FILTER_MAX_ENUM = 0x7FFFFFFF
    } EFilter;
}
