#pragma once
#include <vulkan/vulkan_core.h>

#include "Common/CommandHeaders.h"
#include "Logger/Logger.h"
#include "RHI/Enums/Attachment/AttachmentLoadOp.h"
#include "RHI/Enums/Attachment/AttachmentStoreOp.h"
#include "RHI/Enums/Image/ESampleCountFlagBits.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Enums/Image/EImageLayout.h"
#include "RHI/Program/GPURenderPass.h"

namespace ArisenEngine::RHI
{
    class RHIVkGPURenderPass final : public GPURenderPass
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPURenderPass)
        RHIVkGPURenderPass(VkDevice device, UInt32 maxFramesInFlight);
        ~RHIVkGPURenderPass() noexcept override;

        void* GetHandle(UInt32 frameIndex) override;

        void AddAttachmentAction(
            EFormat format,
            ESampleCountFlagBits sample,
            AttachmentLoadOp colorLoadOp, AttachmentStoreOp colorStoreOp,
            AttachmentLoadOp stencilLoadOp, AttachmentStoreOp stencilStoreOp,
            EImageLayout initialLayout, EImageLayout finalLayout
            ) override;

        UInt32 GetAttachmentCount() override;
        
        void AllocRenderPass(UInt32 frameIndex) override;
        void FreeRenderPass(UInt32 frameIndex) override;
        void FreeAllRenderPasses() override;
        GPUSubPass* AddSubPass() override;
        UInt32 GetSubPassCount() override;

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
