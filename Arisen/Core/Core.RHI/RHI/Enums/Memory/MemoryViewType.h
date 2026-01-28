#pragma once

namespace ArisenEngine::RHI
{
    typedef enum MemoryViewType
    {
        IMAGE_MEMORY_VIEW_TYPE = 0x00000001,
        BUFFER_MEMORY_VIEW_TYPE = 0x00000002,
        MEMORY_VIEW_TYPE_MAX_ENUM = 0x7FFFFFFF
    } MemoryViewType;
}