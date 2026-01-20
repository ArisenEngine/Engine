#include "FactoryExports.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIFactory.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL void RHI_Sampler_Destroy(RHI_SamplerHandle sampler)
{
    auto* s = reinterpret_cast<RHI::RHISampler*>(sampler);
    if (s == nullptr) return;

    auto* dev = s->GetDevice();
    if (dev == nullptr)
    {
        delete s;
        return;
    }

    dev->GetFactory()->ReleaseSampler(s);
}


