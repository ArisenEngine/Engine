#include "FactoryExports.h"

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

    // Schedule deletion after everything submitted so far on the graphics queue.
    // Actual Vulkan cleanup lives in RHIVkSampler::~RHIVkSampler().
    auto* q = dev->GetQueue(RHI::RHIQueueType::Graphics);
    const auto ticket = q ? q->GetLatestTicket() : 0;
    dev->DeferredDelete(RHI::RHIQueueType::Graphics, ticket, RHI::MakeDeferredDeleteItem<RHI::RHISampler>(s));
}


