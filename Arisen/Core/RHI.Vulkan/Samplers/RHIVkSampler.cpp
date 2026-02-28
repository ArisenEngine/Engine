#include "Samplers/RHIVkSampler.h"
#include "Utils/RHIVkInitializer.h"
#include "Logger/Logger.h"

ArisenEngine::RHI::RHIVkSampler::RHIVkSampler(RHIDevice* device, RHISamplerDesc&& desc) : RHISampler(device)
{
    m_VkDevice = static_cast<VkDevice>(device->GetHandle());
    auto samplerInfo = RHI::SamplerCreateInfo(std::move(desc));
    if (vkCreateSampler(m_VkDevice, &samplerInfo, nullptr, &m_Sampler) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkSampler::RHIVkSampler]: failed to create texture sampler!");
    }
}

ArisenEngine::RHI::RHIVkSampler::~RHIVkSampler()
{
    vkDestroySampler(m_VkDevice, m_Sampler, nullptr);
}

void* ArisenEngine::RHI::RHIVkSampler::GetHandle() const
{
    return m_Sampler;
}
