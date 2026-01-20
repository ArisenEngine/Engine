#pragma once
#include <vulkan/vulkan_core.h>

#include "Logger/Logger.h"
#include "RHI/Surfaces/FrameBuffer.h"
#include <map>
#include <vector>
#include <tuple>

namespace ArisenEngine::RHI
{
    class RHIVkDevice;
    class RHIVkFrameBuffer final : public FrameBuffer
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkFrameBuffer)
        RHIVkFrameBuffer(RHIVkDevice* device, UInt32 maxFramesInFlight);
        ~RHIVkFrameBuffer() noexcept override;

        void* GetHandle(UInt32 currentFrameIndex) override;
        void SetAttachment(UInt32 frameIndex, ImageView* imageView, GPURenderPass* renderPass) override;
        
        // Extended for multi-attachment caching
        void SetAttachments(UInt32 frameIndex, const Containers::Vector<ImageView*>& imageViews, GPURenderPass* renderPass);

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
        ImageView* m_ImageView {nullptr};
        std::map<FramebufferCacheKey, VkFramebuffer> m_FramebufferCache;
    };
}
