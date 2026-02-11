#pragma once
#include "Base/PrimitiveTypes.h"

typedef struct RHISamplerLimits
{
    ArisenEngine::Float32 maxSamplerAnisotropy;
    
} RHISamplerLimits;

typedef struct RHIDeviceLimits
{
    RHISamplerLimits sampler;
    int rayTracingSupported;
} RHIDeviceLimits;

