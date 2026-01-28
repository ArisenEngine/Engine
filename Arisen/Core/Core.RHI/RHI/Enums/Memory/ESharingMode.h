#pragma once

namespace ArisenEngine::RHI
{
    typedef enum ESharingMode {
        SHARING_MODE_EXCLUSIVE = 0,
        SHARING_MODE_CONCURRENT = 1,
        SHARING_MODE_MAX_ENUM = 0x7FFFFFFF
    } ESharingMode;
}
