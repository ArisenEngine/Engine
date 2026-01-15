#include "RHIVkFactory.h"

#include "Program/RHIVkSampler.h"

ArisenEngine::RHI::RHIVkFactory::RHIVkFactory() : RHIFactory()
{
}

ArisenEngine::RHI::RHIVkFactory::~RHIVkFactory()
{
    
}

ArisenEngine::RHI::RHISampler* ArisenEngine::RHI::RHIVkFactory::CreateSampler(
    RHIDevice* device,
    RHISamplerDesc&& desc)
{
    return new RHIVkSampler(device, std::move(desc));
}
