#pragma once
#include <vulkan/vulkan_core.h>

#include "Logger/Logger.h"
#include "RHI/RenderPass/RHIFrameBuffer.h"
#include <map>
#include <vector>
#include <tuple>

namespace ArisenEngine::RHI
{
    class RHIVkDevice;
    class RHIVkFrameBuffer final : public RHIFrameBuffer
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkFrameBuffer)
        RHIVkFrameBuffer(RHIVkDevice* device, UInt32 maxFramesInFlight);
        ~RHIVkFrameBuffer() noexcept override;

        void* GetHandle(UInt32 currentFrameIndex) override;
        void SetAttachment(UInt32 frameIndex, RHIImageViewHandle imageView, RHIRenderPass* renderPass) override;
        
        // Extended for multi-attachment caching
        void SetAttachments(UInt32 frameIndex, const Containers::Vector<RHIImageViewHandle>& imageViews, RHIRenderPass* renderPass) override;

        EFormat GetAttachFormat() override;
    private:
        void FreeFrameBuffer(UInt32 currentFrameIndex);
        void FreeAllFrameBuffers();

    private:
        struct FramebufferCacheKey {
            VkRenderPass renderPass;
            Containers::Vector<VkImageView> attachments;
            UInt32 width;
            UInt32 height;
            UInt32 layers;

            bool operator<(const FramebufferCacheKey& other) const {
                return std::tie(renderPass, attachments, width, height, layers) <
                       std::tie(other.renderPass, other.attachments, other.width, other.height, other.layers);
            }
        };

        Containers::Vector<VkFramebuffer> m_VkFrameBuffers;
        RHIVkDevice* m_Device;
        RHIImageViewHandle m_ImageView {RHIImageViewHandle::Invalid()};
        std::map<FramebufferCacheKey, VkFramebuffer> m_FramebufferCache;
    };
}




