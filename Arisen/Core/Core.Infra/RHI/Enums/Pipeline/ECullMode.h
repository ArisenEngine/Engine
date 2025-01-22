#pragma once
namespace ArisenEngine::RHI
{
    typedef enum ECullModeFlagBits {
        CULL_MODE_NONE = 0,
        CULL_MODE_FRONT_BIT = 0x00000001,
        CULL_MODE_BACK_BIT = 0x00000002,
        CULL_MODE_FRONT_AND_BACK = 0x00000003,
        CULL_MODE_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
    } ECullModeFlagBits;
}
