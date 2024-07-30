#pragma once
#include <vulkan/vulkan_core.h>

#include "Common/CommandHeaders.h"
#include "Logger/Logger.h"
#include "RHI/Enums/Attachment/AttachmentLoadOp.h"
#include "RHI/Enums/Attachment/AttachmentStoreOp.h"
#include "RHI/Enums/Attachment/SampleCountFlagBits.h"
#include "RHI/Enums/Image/Format.h"
#include "RHI/Enums/Image/ImageLayout.h"
#include "RHI/Enums/Pipeline/EPipelineBindPoint.h"
#include "RHI/Program/GPURenderPass.h"

namespace NebulaEngine::RHI
{
    class RHIVkGPURenderPass final : public GPURenderPass
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPURenderPass)
        RHIVkGPURenderPass(VkDevice device);
        ~RHIVkGPURenderPass() noexcept override;

        void* GetHandle() override { ASSERT(m_VkRenderPass != VK_NULL_HANDLE); return m_VkRenderPass; }

        void AddAttachmentAction(
            Format format,
            SampleCountFlagBits sample,
            AttachmentLoadOp colorLoadOp, AttachmentStoreOp colorStoreOp,
            AttachmentLoadOp stencilLoadOp, AttachmentStoreOp stencilStoreOp,
            ImageLayout initialLayout, ImageLayout finalLayout
            ) override;

        u32 GetAttachmentCount() override;
        
        void AllocRenderPass() override;
        void FreeRenderPass() override;

        GPUSubPass* AddSubPass(EPipelineBindPoint bindPoint) override;

    private:
        VkRenderPass m_VkRenderPass { VK_NULL_HANDLE };
        VkDevice m_VkDevice;
        Containers::Vector<VkAttachmentDescription> m_AttachmentDescriptions;
        Containers::Vector<VkSubpassDescription> m_SubpassDescriptions;
        Containers::Vector<VkSubpassDependency> m_Dependencies;

        Containers::Vector<std::shared_ptr<GPUSubPass>> m_SubpassPool;
        Containers::Vector<std::shared_ptr<GPUSubPass>> m_SubpassesToDispatch;
    };
}
