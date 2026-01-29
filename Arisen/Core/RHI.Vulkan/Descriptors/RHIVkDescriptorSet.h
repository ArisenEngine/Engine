#pragma once
#include "Descriptors/RHIVkDescriptorPool.h"
#include "Base/FoundationMinimal.h"
#include "RHI/Descriptors/RHIDescriptorSet.h"

namespace ArisenEngine::RHI
{
    class RHIVkDescriptorSet : public RHIDescriptorSet
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDescriptorSet)
        RHIVkDescriptorSet(RHIDescriptorPool* RHIDescriptorPool, UInt32 layoutIndex, VkDescriptorSet vkDescriptorSet);
        virtual ~RHIVkDescriptorSet() override;
        void* GetHandle() override;
    private:
        VkDescriptorSet m_DescriptorSet;
    };
}





