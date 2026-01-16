#pragma once

#include "Common/CommandHeaders.h"

namespace ArisenEngine::RHI
{
    enum class RHIQueueType : UInt8
    {
        Graphics,
        Compute,
        Transfer,
        Present,
    };
}

