#pragma once
#include "Base/BindingMacros.h"
#include "Base/FoundationMinimal.h"

ARISEN_BIND_PACKAGE("com.arisen.rhi.core")
ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(ERHIAccelerationStructureBuildFlagBits)

    enum ERHIAccelerationStructureBuildFlagBits : UInt32
    {
        AS_BUILD_ALLOW_UPDATE_BIT = 0x00000001,
        AS_BUILD_ALLOW_COMPACTION_BIT = 0x00000002,
        AS_BUILD_PREFER_FAST_TRACE_BIT = 0x00000004,
        AS_BUILD_PREFER_FAST_BUILD_BIT = 0x00000008,
        AS_BUILD_MIN_OVERLAP_BIT = 0x00000010,
    };

    typedef UInt32 ERHIAccelerationStructureBuildFlags;
}

