#pragma once
namespace NebulaEngine::RHI
{
    typedef enum ESampleCountFlagBits {
        SAMPLE_COUNT_1_BIT = 0x00000001,
        SAMPLE_COUNT_2_BIT = 0x00000002,
        SAMPLE_COUNT_4_BIT = 0x00000004,
        SAMPLE_COUNT_8_BIT = 0x00000008,
        SAMPLE_COUNT_16_BIT = 0x00000010,
        SAMPLE_COUNT_32_BIT = 0x00000020,
        SAMPLE_COUNT_64_BIT = 0x00000040,
        SAMPLE_COUNT_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
    } ESampleCountFlagBits;
}
