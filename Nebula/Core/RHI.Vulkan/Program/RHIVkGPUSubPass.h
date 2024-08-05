#pragma once
#include <vulkan/vulkan_core.h>

#include "RHIVkGPURenderPass.h"
#include "RHI/Program/GPUSubPass.h"

namespace NebulaEngine::RHI
{
    class RHIVkGPUSubPass final : public GPUSubPass
    {
    public:
        NO_COPY_NO_MOVE(RHIVkGPUSubPass)
        RHIVkGPUSubPass(RHIVkGPURenderPass* renderPass, u32 index);
        ~RHIVkGPUSubPass() override;

        void AddInputReference(u32 index, ImageLayout layout) override;
        void AddColorReference(u32 index, ImageLayout layout) override;
        void SetResolveReference(u32 index, ImageLayout layout) override;
        void SetDepthStencilReference(u32 index, ImageLayout layout) override;
        
        void ClearAll() override;
        const u32 GetIndex() const override { return m_Index; }

        SubpassDescription GetDescriptions() override;
    
    private:

        friend RHIVkGPURenderPass;
        void Bind(u32 index) override;
        void RemovePreserve(u32 index);
        void ResizePreserve();
        bool IsInsidePreserve(u32 index);
        u32 m_Index;
        Containers::Vector<VkAttachmentReference> m_InputReferences {};
        Containers::Vector<VkAttachmentReference> m_ColorReferences {};
        VkAttachmentReference m_ResolveReference;
        VkAttachmentReference m_DepthStencilReference;
        Containers::Vector<u32> m_PreserveAttachments;
        
    };
}
