#pragma once
#include <vulkan/vulkan_core.h>

#include "RHI/Handles/ImageHandle.h"

namespace NebulaEngine::RHI
{
    class RHIVkImageHandle final : public ImageHandle
    {
    public:
        NO_COPY_NO_MOVE(RHIVkImageHandle)
        RHIVkImageHandle(VkDevice device);
        RHIVkImageHandle(VkDevice device, VkImage image, ImageViewDesc desc);
        ~RHIVkImageHandle() noexcept override;
        void* GetHandle() const override { return m_VkImage; }
    private:
        
        VkImage m_VkImage { VK_NULL_HANDLE };
        VkDevice m_VKDevice;
    };
}
