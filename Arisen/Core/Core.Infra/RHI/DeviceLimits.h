#pragma once
#include "Common/PrimitiveTypes.h"

typedef struct RHISamplerLimits
{
    ArisenEngine::Float32 maxSamplerAnisotropy;
} RHISamplerLimits;

typedef struct RHIDeviceLimits
{
    RHISamplerLimits sampler;
    
} RHIDeviceLimits;
