#pragma once

namespace ArisenEngine::RHI
{
    typedef enum SharingMode {
        SHARING_MODE_EXCLUSIVE = 0,
        SHARING_MODE_CONCURRENT = 1,
        SHARING_MODE_MAX_ENUM = 0x7FFFFFFF
    } SharingMode;
}
