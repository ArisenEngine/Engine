#pragma once
namespace ArisenEngine::RHI
{
    typedef enum EColorComponentFlagBits {
        COLOR_COMPONENT_R_BIT = 0x00000001,
        COLOR_COMPONENT_G_BIT = 0x00000002,
        COLOR_COMPONENT_B_BIT = 0x00000004,
        COLOR_COMPONENT_A_BIT = 0x00000008,
        COLOR_COMPONENT_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
    } EColorComponentFlagBits;
}
