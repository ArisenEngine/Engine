#pragma once
#include "RHI/Samplers/RHISampler.h"
#include "vulkan/vulkan_core.h"

namespace ArisenEngine::RHI
{
    class RHIVkSampler final : public RHISampler
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkSampler);
        RHIVkSampler(RHIDevice* device, RHISamplerDesc&& desc);
        virtual ~RHIVkSampler();
        void* GetHandle() const override;

    private:
        VkSampler m_Sampler;
        VkDevice m_VkDevice;
    };
}
