#pragma once

#include "Base/BindingMacros.h"

ARISEN_BIND_PACKAGE("com.arisen.rhi.core")
ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(ERHIAccelerationStructureGeometryType)

    enum class ERHIAccelerationStructureGeometryType
    {
        Triangles = 0,
        AABBs = 1,
        Instances = 2,
    };
}

