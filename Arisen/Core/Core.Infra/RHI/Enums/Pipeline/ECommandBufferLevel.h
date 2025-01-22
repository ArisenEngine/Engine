#pragma once

namespace ArisenEngine::RHI
{
    typedef enum ECommandBufferLevel
    {
        COMMAND_BUFFER_LEVEL_PRIMARY = 0,
        COMMAND_BUFFER_LEVEL_SECONDARY = 1,
        COMMAND_BUFFER_LEVEL_MAX_ENUM = 0x7FFFFFFF

    } ECommandBufferLevel;
}
