#pragma once
#include "RHIVkDescriptorPool.h"
#include "Common/CommandHeaders.h"
#include "RHI/Program/RHIDescriptorSet.h"

namespace ArisenEngine::RHI
{
    class RHIVkDescriptorSet : public RHIDescriptorSet
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDescriptorSet)
        RHIVkDescriptorSet(DescriptorPool* descriptorPool, UInt32 layoutIndex, VkDescriptorSet vkDescriptorSet);
        virtual ~RHIVkDescriptorSet() override;
        void* GetHandle() override;
    private:
        VkDescriptorSet m_DescriptorSet;
    };
}
