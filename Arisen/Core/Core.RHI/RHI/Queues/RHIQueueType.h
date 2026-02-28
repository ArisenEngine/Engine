#pragma once

#include "Base/FoundationMinimal.h"
#include "Base/BindingMacros.h"

ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(RHIQueueType)
    enum class RHIQueueType : UInt8
    {
        Graphics,
        Compute,
        Transfer,
        Present,
    };
}


