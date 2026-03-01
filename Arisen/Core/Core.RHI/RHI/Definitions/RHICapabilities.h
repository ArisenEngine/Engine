#pragma once
#include "Base/PrimitiveTypes.h"
#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

ARISEN_BIND_STRUCT(RHICapabilities)

typedef struct RHICapabilities
{
    ArisenEngine::Float32 maxSamplerAnisotropy;
    ArisenEngine::UInt32 maxDescriptorSets;
    int rayTracingSupported;
    int supportsDynamicRendering;
    int timestampComputeAndGraphics;
} RHICapabilities;
