#pragma once
#include "RHI/RHIFactory.h"

namespace ArisenEngine::RHI
{
    class RHIVkFactory final: public RHIFactory 
    {
    public:
        NO_COPY_NO_MOVE(RHIVkFactory)
        RHIVkFactory();
        ~RHIVkFactory();

        RHISampler* CreateSampler(RHIDevice* device, RHISamplerDesc&& desc) override;
    };
    
}
