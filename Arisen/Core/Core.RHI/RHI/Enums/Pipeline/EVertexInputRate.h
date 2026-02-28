#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(EVertexInputRate)

    typedef enum EVertexInputRate
    {
        VERTEX_INPUT_RATE_VERTEX = 0,
        VERTEX_INPUT_RATE_INSTANCE = 1,
        VERTEX_INPUT_RATE_MAX_ENUM = 0x7FFFFFFF
    } EVertexInputRate;
}
