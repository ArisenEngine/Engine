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
        RHIVkFrameBuffer(VkDevice device, UInt32 maxFramesInFlight);
        ~RHIVkFrameBuffer() noexcept override;

        void* GetHandle(UInt32 currentFrameIndex) override;
        void SetAttachment(UInt32 frameIndex, ImageView* imageView, GPURenderPass* renderPass) override;
        Format GetAttachFormat() override;
    private:
        void FreeFrameBuffer(UInt32 currentFrameIndex);
        void FreeAllFrameBuffers();
        Containers::Vector<VkFramebuffer> m_VkFrameBuffers;
        VkDevice m_VkDevice;
        ImageView* m_ImageView {nullptr};
    };
}
