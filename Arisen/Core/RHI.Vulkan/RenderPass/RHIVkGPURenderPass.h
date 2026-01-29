#pragma once
#include <vulkan/vulkan_core.h>
#include <map>
#include <vector>

#include "Base/FoundationMinimal.h"
#include "Logger/Logger.h"
#include "RHI/Enums/Attachment/EAttachmentLoadOp.h"
#include "RHI/Enums/Attachment/EAttachmentStoreOp.h"
#include "RHI/Enums/Image/ESampleCountFlagBits.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Enums/Image/EImageLayout.h"
#include "RHI/RenderPass/RHIRenderPass.h"

namespace ArisenEngine::RHI
{
    class RHIVkDevice;
    class RHIVkGPURenderPass final : public RHIRenderPass
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPURenderPass)
        RHIVkGPURenderPass(RHIVkDevice* device, UInt32 maxFramesInFlight);
        ~RHIVkGPURenderPass() noexcept override;

        inline void* GetHandle(UInt32 frameIndex) override
        {
            ASSERT(m_VkRenderPasses[frameIndex % m_MaxFramesInFlight] != VK_NULL_HANDLE);
            return m_VkRenderPasses[frameIndex % m_MaxFramesInFlight];
        }

        void AddAttachmentAction(
            EFormat format,
            ESampleCountFlagBits sample,
            EAttachmentLoadOp colorLoadOp, EAttachmentStoreOp colorStoreOp,
            EAttachmentLoadOp stencilLoadOp, EAttachmentStoreOp stencilStoreOp,
            EImageLayout initialLayout, EImageLayout finalLayout
            ) override;

        UInt32 GetAttachmentCount() override;
        
        void AllocRenderPass(UInt32 frameIndex) override;
        void FreeRenderPass(UInt32 frameIndex) override;
        void FreeAllRenderPasses() override;
        RHISubPass* AddSubPass() override;
        UInt32 GetSubPassCount() override;

    private:
        struct RenderPassCacheKey {
            Containers::Vector<VkAttachmentDescription> attachments;
            Containers::Vector<VkSubpassDescription> subpasses;
            Containers::Vector<VkSubpassDependency> dependencies;

            bool operator<(const RenderPassCacheKey& other) const {
                if (attachments.size() != other.attachments.size()) return attachments.size() < other.attachments.size();
                if (subpasses.size() != other.subpasses.size()) return subpasses.size() < other.subpasses.size();
                return dependencies.size() < other.dependencies.size();
            }
        };

        RHIVkDevice* m_Device;
        Containers::Vector<VkAttachmentDescription> m_AttachmentDescriptions;
        Containers::Vector<VkSubpassDescription> m_SubpassDescriptions;
        Containers::Vector<VkSubpassDependency> m_Dependencies;
        
        Containers::Vector<VkRenderPass> m_VkRenderPasses;
        std::map<RenderPassCacheKey, VkRenderPass> m_RenderPassCache;
        Containers::Vector<std::shared_ptr<RHISubPass>> m_SubpassPool;
        Containers::Vector<std::shared_ptr<RHISubPass>> m_SubpassesToDispatch;
    };
}





