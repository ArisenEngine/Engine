#include "FactoryExports.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL RHI_SamplerHandle RHI_Factory_CreateSampler(RHI_FactoryHandle factory, RHI_DeviceHandle device, const RHI::RHISamplerDesc* desc)
{
    auto* f = reinterpret_cast<RHI::RHIFactory*>(factory);
    auto* d = reinterpret_cast<RHI::RHIDevice*>(device);
    if (f == nullptr || d == nullptr || desc == nullptr) return nullptr;
    RHI::RHISamplerDesc copy = *desc;
    auto sampler = f->CreateSampler(d, std::move(copy));
    return reinterpret_cast<RHI_SamplerHandle>(sampler);
}

extern "C" ENGINE_DLL void RHI_Factory_Destroy(RHI_FactoryHandle factory)
{
    auto* f = reinterpret_cast<RHI::RHIFactory*>(factory);
    delete f;
}

extern "C" ENGINE_DLL void RHI_Sampler_Destroy(RHI_SamplerHandle sampler)
{
    auto* s = reinterpret_cast<RHI::RHISampler*>(sampler);
    delete s;
}


