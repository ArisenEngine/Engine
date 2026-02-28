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
        EFormat GetAttachFormat() override;

    private:
        RHIVkDevice* m_Device;
        Containers::Vector<VkFramebuffer> m_VkFrameBuffers;
        RHIImageViewHandle m_ImageView{RHIImageViewHandle::Invalid()};
    };
}
