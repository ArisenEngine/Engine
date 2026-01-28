#pragma once

namespace ArisenEngine::RHI
{
    typedef enum ComponentSwizzle {
        COMPONENT_SWIZZLE_IDENTITY = 0,
        COMPONENT_SWIZZLE_ZERO = 1,
        COMPONENT_SWIZZLE_ONE = 2,
        COMPONENT_SWIZZLE_R = 3,
        COMPONENT_SWIZZLE_G = 4,
        COMPONENT_SWIZZLE_B = 5,
        COMPONENT_SWIZZLE_A = 6,
        COMPONENT_SWIZZLE_MAX_ENUM = 0x7FFFFFFF
    } ComponentSwizzle;
}
