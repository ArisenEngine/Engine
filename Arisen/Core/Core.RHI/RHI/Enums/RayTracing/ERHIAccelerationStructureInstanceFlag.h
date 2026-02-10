#pragma once
#include "Base/FoundationMinimal.h"

namespace ArisenEngine::RHI
{
    enum ERHIAccelerationStructureInstanceFlagBits : UInt32
    {
        AS_INSTANCE_TRIANGLE_FACING_CULL_DISABLE_BIT = 0x00000001,
        AS_INSTANCE_TRIANGLE_FLIP_FACING_BIT = 0x00000002,
        AS_INSTANCE_FORCE_OPAQUE_BIT = 0x00000004,
        AS_INSTANCE_FORCE_NO_OPAQUE_BIT = 0x00000008,
    };
    typedef UInt32 ERHIAccelerationStructureInstanceFlags;
}
