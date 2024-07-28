#pragma once
#include <vulkan/vulkan_core.h>

#include "RHI/Surfaces/FrameBuffer.h"

namespace NebulaEngine::RHI
{
    class RHIVkFrameBuffer final : public FrameBuffer
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkFrameBuffer)
        RHIVkFrameBuffer(VkDevice device);
        ~RHIVkFrameBuffer() noexcept override;

        void* GetHandle() override { return m_VkFrameBuffer; }
    private:
        VkFramebuffer m_VkFrameBuffer;
        VkDevice m_VkDevice;
    };
}
