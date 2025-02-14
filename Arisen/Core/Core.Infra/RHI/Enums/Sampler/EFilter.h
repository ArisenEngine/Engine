#pragma once
namespace ArisenEngine::RHI
{
    typedef enum EFilter {
        FILTER_NEAREST = 0,
        FILTER_LINEAR = 1,
        FILTER_CUBIC_EXT = 1000015000,
        FILTER_CUBIC_IMG = VK_FILTER_CUBIC_EXT,
        FILTER_MAX_ENUM = 0x7FFFFFFF
    } EFilter;
}
