#pragma once
#include "Base/PrimitiveTypes.h"
#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

ARISEN_BIND_STRUCT(RHISamplerLimits)
typedef struct RHISamplerLimits
{
    ArisenEngine::Float32 maxSamplerAnisotropy;
    
} RHISamplerLimits;

ARISEN_BIND_STRUCT(RHIDeviceLimits)
typedef struct RHIDeviceLimits
{
    RHISamplerLimits sampler;
    int rayTracingSupported;
} RHIDeviceLimits;

