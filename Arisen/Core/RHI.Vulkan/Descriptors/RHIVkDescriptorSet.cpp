#include "Descriptors/RHIVkDescriptorSet.h"

ArisenEngine::RHI::RHIVkDescriptorSet::RHIVkDescriptorSet(RHIDescriptorPool* RHIDescriptorPool,
                                                          UInt32 layoutIndex,
                                                          VkDescriptorSet vkDescriptorSet): RHIDescriptorSet(
    RHIDescriptorPool, layoutIndex), m_DescriptorSet(vkDescriptorSet)
{
}

ArisenEngine::RHI::RHIVkDescriptorSet::~RHIVkDescriptorSet()
{
}

void* ArisenEngine::RHI::RHIVkDescriptorSet::GetHandle()
{
    return m_DescriptorSet;
}
