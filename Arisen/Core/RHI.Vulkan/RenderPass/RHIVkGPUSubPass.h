#pragma once
#include <vulkan/vulkan_core.h>

#include "RenderPass/RHIVkGPURenderPass.h"
#include "RHI/RenderPass/RHISubPass.h"

namespace ArisenEngine::RHI
{
    class RHIVkGPUSubPass final : public RHISubPass
    {
    public:
        NO_COPY_NO_MOVE(RHIVkGPUSubPass)
        RHIVkGPUSubPass(RHIVkGPURenderPass* renderPass, UInt32 index);
        ~RHIVkGPUSubPass() override;

        void AddInputReference(UInt32 index, EImageLayout layout) override;
        void AddColorReference(UInt32 index, EImageLayout layout) override;
        void SetResolveReference(UInt32 index, EImageLayout layout) override;
        void SetDepthStencilReference(UInt32 index, EImageLayout layout) override;

        void ClearAll() override;
        const UInt32 GetIndex() const override { return m_Index; }

        RHISubpassDescription GetDescriptions() override;

    private:
        friend RHIVkGPURenderPass;
        void Bind(UInt32 index) override;
        void RemovePreserve(UInt32 index);
        void ResizePreserve();
        bool IsInsidePreserve(UInt32 index);
        UInt32 m_Index;
        Containers::Vector<VkAttachmentReference> m_InputReferences{};
        Containers::Vector<VkAttachmentReference> m_ColorReferences{};
        VkAttachmentReference m_ResolveReference;
        VkAttachmentReference m_DepthStencilReference;
        Containers::Vector<UInt32> m_PreserveAttachments;
    };
}
