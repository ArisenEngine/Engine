#pragma once

#include "Base/BindingMacros.h"

ARISEN_BIND_PACKAGE("com.arisen.rhi.core")
ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(MemoryViewType)

    typedef enum MemoryViewType
    {
        IMAGE_MEMORY_VIEW_TYPE = 0x00000001,
        BUFFER_MEMORY_VIEW_TYPE = 0x00000002,
        MEMORY_VIEW_TYPE_MAX_ENUM = 0x7FFFFFFF
    } MemoryViewType;
}

