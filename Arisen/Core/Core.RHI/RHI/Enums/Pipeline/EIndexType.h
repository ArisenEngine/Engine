#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_PACKAGE("com.arisen.rhi.core")
ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(EIndexType)

    typedef enum EIndexType
    {
        INDEX_TYPE_UINT16 = 0,
        INDEX_TYPE_UINT32 = 1,
        INDEX_TYPE_NONE_KHR = 1000165000,
        INDEX_TYPE_UINT8_KHR = 1000265000,
        INDEX_TYPE_NONE_NV = INDEX_TYPE_NONE_KHR,
        INDEX_TYPE_UINT8_EXT = INDEX_TYPE_UINT8_KHR,
        INDEX_TYPE_MAX_ENUM = 0x7FFFFFFF
    } EIndexType;
}

