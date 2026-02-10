#pragma once
#include "Base/FoundationMinimal.h"

namespace ArisenEngine::RHI
{
    enum ERHIAccelerationStructureGeometryFlagBits : UInt32
    {
        AS_GEOMETRY_OPAQUE_BIT = 0x00000001,
        AS_GEOMETRY_NO_DUPLICATE_ANY_HIT_INVOCATION_BIT = 0x00000002,
    };
    typedef UInt32 ERHIAccelerationStructureGeometryFlags;
}
