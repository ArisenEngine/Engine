#pragma once

#include "Base/FoundationMinimal.h"

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


