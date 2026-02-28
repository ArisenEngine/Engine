#pragma once

#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")
namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(ECommandBufferLevel)
    typedef enum ECommandBufferLevel
    {
        COMMAND_BUFFER_LEVEL_PRIMARY = 0,
        COMMAND_BUFFER_LEVEL_SECONDARY = 1,
        COMMAND_BUFFER_LEVEL_MAX_ENUM = 0x7FFFFFFF

    } ECommandBufferLevel;
}
