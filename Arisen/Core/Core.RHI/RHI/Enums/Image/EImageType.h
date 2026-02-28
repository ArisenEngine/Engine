#pragma once

#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(EImageType)

    typedef enum EImageType
    {
        IMAGE_TYPE_1D = 0,
        IMAGE_TYPE_2D = 1,
        IMAGE_TYPE_3D = 2,
        IMAGE_TYPE_MAX_ENUM = 0x7FFFFFFF
    } EImageType;
}
