#pragma once

namespace ArisenEngine::RHI
{
    typedef enum PresentMode
    {
        PRESENT_MODE_IMMEDIATE = 0,
        PRESENT_MODE_MAILBOX = 1,
        PRESENT_MODE_FIFO = 2,
        PRESENT_MODE_FIFO_RELAXED = 3,
        PRESENT_MODE_SHARED_DEMAND_REFRESH = 1000111000,
        PRESENT_MODE_SHARED_CONTINUOUS_REFRESH = 1000111001,
        PRESENT_MODE_MAX_ENUM = 0x7FFFFFFF
    } PresentMode;
}