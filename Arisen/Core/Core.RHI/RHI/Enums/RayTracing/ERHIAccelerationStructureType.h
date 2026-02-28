#pragma once

#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")
namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(ERHIAccelerationStructureType)
    enum class ERHIAccelerationStructureType
    {
        TopLevel = 0,
        BottomLevel = 1,
        Generic = 2,
    };
}
