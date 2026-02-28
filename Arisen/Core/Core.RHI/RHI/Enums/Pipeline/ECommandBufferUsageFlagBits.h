#pragma once

#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")
namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(ECommandBufferUsageFlagBits)
    typedef enum ECommandBufferUsageFlagBits
    {
        COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT = 0x00000001,
        COMMAND_BUFFER_USAGE_RENDER_PASS_CONTINUE_BIT = 0x00000002,
        COMMAND_BUFFER_USAGE_SIMULTANEOUS_USE_BIT = 0x00000004,
        COMMAND_BUFFER_USAGE_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
    } ECommandBufferUsageFlagBits;
}
