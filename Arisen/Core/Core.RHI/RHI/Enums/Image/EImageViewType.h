#pragma once

#include "Base/BindingMacros.h"

ARISEN_BIND_PACKAGE("com.arisen.rhi.core")
ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(EImageViewType)

    typedef enum EImageViewType
    {
        IMAGE_VIEW_TYPE_1D = 0,
        IMAGE_VIEW_TYPE_2D = 1,
        IMAGE_VIEW_TYPE_3D = 2,
        IMAGE_VIEW_TYPE_CUBE = 3,
        IMAGE_VIEW_TYPE_1D_ARRAY = 4,
        IMAGE_VIEW_TYPE_2D_ARRAY = 5,
        IMAGE_VIEW_TYPE_CUBE_ARRAY = 6,
        IMAGE_VIEW_TYPE_MAX_ENUM = 0x7FFFFFFF
    } EImageViewType;
}

