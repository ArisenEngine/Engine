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

        bool IsBindless() const override { return !m_BindlessIndices.empty(); }
        const Containers::Vector<UInt32>& GetBindlessIndices() const override { return m_BindlessIndices; }

        void SetBindlessIndices(const Containers::Vector<UInt32>& indices) { m_BindlessIndices = indices; }

    private:
        VkDescriptorSet m_DescriptorSet;
        Containers::Vector<UInt32> m_BindlessIndices;
    };
}
