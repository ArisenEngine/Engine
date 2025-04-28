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

        std::shared_ptr<RHISampler> CreateSampler(Device* device, RHISamplerDesc&& desc) override;
    };
    
}
