#include "FactoryExports.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIFactory.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL void RHI_Sampler_Destroy(RHI_DeviceHandle device, RHI_SamplerHandle sampler)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return;
    auto h = *reinterpret_cast<RHI::RHISamplerHandle*>(&sampler);
    dev->GetFactory()->ReleaseSampler(h);
}


