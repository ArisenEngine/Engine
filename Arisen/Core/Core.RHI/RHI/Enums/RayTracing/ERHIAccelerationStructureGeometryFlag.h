#pragma once
#include "Base/BindingMacros.h"
#include "Base/FoundationMinimal.h"

ARISEN_BIND_PACKAGE("com.arisen.rhi.core")
ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(ERHIAccelerationStructureGeometryFlagBits)

    enum ERHIAccelerationStructureGeometryFlagBits : UInt32
    {
        AS_GEOMETRY_OPAQUE_BIT = 0x00000001,
        AS_GEOMETRY_NO_DUPLICATE_ANY_HIT_INVOCATION_BIT = 0x00000002,
    };

    typedef UInt32 ERHIAccelerationStructureGeometryFlags;
}

