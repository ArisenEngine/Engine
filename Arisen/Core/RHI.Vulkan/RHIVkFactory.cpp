#include "RHIVkFactory.h"

#include "Program/RHIVkSampler.h"

ArisenEngine::RHI::RHIVkFactory::RHIVkFactory() : RHIFactory()
{
}

ArisenEngine::RHI::RHIVkFactory::~RHIVkFactory()
{
    
}

std::shared_ptr<ArisenEngine::RHI::RHISampler> ArisenEngine::RHI::RHIVkFactory::CreateSampler(Device* device,
    RHISamplerDesc&& desc)
{
    return std::make_shared<RHIVkSampler>(device, std::move(desc));
}
