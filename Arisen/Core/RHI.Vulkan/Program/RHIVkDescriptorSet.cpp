#include "RHIVkDescriptorSet.h"

ArisenEngine::RHI::RHIVkDescriptorSet::RHIVkDescriptorSet(DescriptorPool* descriptorPool,
    UInt32 layoutIndex, VkDescriptorSet vkDescriptorSet): RHIDescriptorSet(descriptorPool, layoutIndex), m_DescriptorSet(vkDescriptorSet)
{
    
}

ArisenEngine::RHI::RHIVkDescriptorSet::~RHIVkDescriptorSet()
{
    
}

void* ArisenEngine::RHI::RHIVkDescriptorSet::GetHandle()
{
    return m_DescriptorSet;
}
