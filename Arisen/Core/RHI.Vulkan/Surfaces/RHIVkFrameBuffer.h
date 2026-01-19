#pragma once
#include <vulkan/vulkan_core.h>

#include "Logger/Logger.h"
#include "RHI/Surfaces/FrameBuffer.h"
#include <map>
#include <vector>

namespace ArisenEngine::RHI
{
    class RHIVkFrameBuffer final : public FrameBuffer
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkFrameBuffer)
        RHIVkFrameBuffer(VkDevice device, UInt32 maxFramesInFlight);
        ~RHIVkFrameBuffer() noexcept override;

        void* GetHandle(UInt32 currentFrameIndex) override;
        void SetAttachment(UInt32 frameIndex, ImageView* imageView, GPURenderPass* renderPass) override;
        
        // Extended for multi-attachment caching
        void SetAttachments(UInt32 frameIndex, const Containers::Vector<ImageView*>& imageViews, GPURenderPass* renderPass);

        EFormat GetAttachFormat() override;
    private:
        void FreeFrameBuffer(UInt32 currentFrameIndex);
        void FreeAllFrameBuffers();

        struct FramebufferCacheKey;

        Containers::Vector<VkFramebuffer> m_VkFrameBuffers;
        VkDevice m_VkDevice;
        ImageView* m_ImageView {nullptr};
        std::map<FramebufferCacheKey, VkFramebuffer> m_FramebufferCache;
    };
}
