#pragma once
#include <vulkan/vulkan_core.h>

#include "Logger/Logger.h"
#include "RHI/Surfaces/FrameBuffer.h"

namespace NebulaEngine::RHI
{
    class RHIVkFrameBuffer final : public FrameBuffer
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkFrameBuffer)
        RHIVkFrameBuffer(VkDevice device);
        ~RHIVkFrameBuffer() noexcept override;

        void* GetHandle() override { ASSERT(m_VkFrameBuffer != VK_NULL_HANDLE); return m_VkFrameBuffer; }
        void SetAttachment(ImageView* imageView, GPURenderPass* renderPass) override;
        Format GetAttachFormat() override;
    private:
        void FreeFrameBuffer();
        VkFramebuffer m_VkFrameBuffer { VK_NULL_HANDLE };
        VkDevice m_VkDevice;
        ImageView* m_ImageView {nullptr};
    };
}
