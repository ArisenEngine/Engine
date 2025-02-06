#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Handles/ImageHandle.h"

namespace ArisenEngine::RHI
{
    class Device;

    class RHIVkImageHandle final : public ImageHandle
    {
    public:
        NO_COPY_NO_MOVE(RHIVkImageHandle)
        RHIVkImageHandle(Device* device);
        RHIVkImageHandle(Device* device, VkImage image, ImageViewDesc desc);
        ~RHIVkImageHandle() noexcept override;
        void* GetHandle() const override { return m_VkImage; }
        void AllocHandle(ImageDescriptor&& desc) override;
        void FreeHandle() override;

        UInt32 AddImageView(ImageViewDesc&& desc) override;
        
        bool AllocDeviceMemory(UInt32 memoryPropertiesBits) override;
    private:

        
        bool m_NeedDestroy {false};
        VkImage m_VkImage { VK_NULL_HANDLE };
        VkDevice m_VKDevice;
        Device* m_Device { nullptr };
    };
}
