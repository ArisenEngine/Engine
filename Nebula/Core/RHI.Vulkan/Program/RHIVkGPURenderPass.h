#pragma once
#include <vulkan/vulkan_core.h>

#include "Common/CommandHeaders.h"
#include "Logger/Logger.h"
#include "RHI/Enums/Attachment/AttachmentLoadOp.h"
#include "RHI/Enums/Attachment/AttachmentStoreOp.h"
#include "RHI/Enums/Attachment/ESampleCountFlagBits.h"
#include "RHI/Enums/Image/Format.h"
#include "RHI/Enums/Image/ImageLayout.h"
#include "RHI/Program/GPURenderPass.h"

namespace NebulaEngine::RHI
{
    class RHIVkGPURenderPass final : public GPURenderPass
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPURenderPass)
        RHIVkGPURenderPass(VkDevice device, u32 maxFramesInFlight);
        ~RHIVkGPURenderPass() noexcept override;

        void* GetHandle(u32 frameIndex) override;

        void AddAttachmentAction(
            Format format,
            ESampleCountFlagBits sample,
            AttachmentLoadOp colorLoadOp, AttachmentStoreOp colorStoreOp,
            AttachmentLoadOp stencilLoadOp, AttachmentStoreOp stencilStoreOp,
            ImageLayout initialLayout, ImageLayout finalLayout
            ) override;

        u32 GetAttachmentCount() override;
        
        void AllocRenderPass(u32 frameIndex) override;
        void FreeRenderPass(u32 frameIndex) override;
        void FreeAllRenderPasses() override;
        GPUSubPass* AddSubPass() override;
        u32 GetSubPassCount() override;

    private:
        Containers::Vector<VkRenderPass> m_VkRenderPasses;
        VkDevice m_VkDevice;
        Containers::Vector<VkAttachmentDescription> m_AttachmentDescriptions;
        Containers::Vector<VkSubpassDescription> m_SubpassDescriptions;
        Containers::Vector<VkSubpassDependency> m_Dependencies;

        Containers::Vector<std::shared_ptr<GPUSubPass>> m_SubpassPool;
        Containers::Vector<std::shared_ptr<GPUSubPass>> m_SubpassesToDispatch;
    };
}
