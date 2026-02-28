#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(ESamplerMipmapMode)

    typedef enum ESamplerMipmapMode
    {
        SAMPLER_MIPMAP_MODE_NEAREST = 0,
        SAMPLER_MIPMAP_MODE_LINEAR = 1,
        SAMPLER_MIPMAP_MODE_MAX_ENUM = 0x7FFFFFFF
    } ESamplerMipmapMode;
}
