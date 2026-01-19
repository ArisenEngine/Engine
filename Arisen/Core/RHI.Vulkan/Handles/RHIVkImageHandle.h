#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Handles/ImageHandle.h"
#include "RHI/Utils/RHIResourceHandle.h"

namespace ArisenEngine::RHI
{
    class RHIDevice;
    class RHIVkImageView;
    class RHIVkDevice;

    class RHIVkImageHandle final : public ImageHandle
    {
    public:
        NO_COPY_NO_MOVE(RHIVkImageHandle)
        RHIVkImageHandle(RHIDevice* device);
        RHIVkImageHandle(RHIDevice* device, VkImage image, ImageViewDesc desc);
        ~RHIVkImageHandle() noexcept override;
        void* GetHandle() const override { return m_VkImage; }
        void AllocHandle(ImageDescriptor&& desc) override;
        void FreeHandle() override;

        UInt32 AddImageView(ImageViewDesc&& desc) override;
        
        bool AllocDeviceMemory(UInt32 memoryPropertiesBits) override;

        void SetRHIHandle(RHIResourceHandle h) { m_RHIHandle = h; }
        RHIResourceHandle GetRHIHandle() const { return m_RHIHandle; }
    private:

        
        bool m_NeedDestroy {false};
        VkImage m_VkImage { VK_NULL_HANDLE };
        VkDevice m_VKDevice;
        RHIDevice* m_Device { nullptr };
        RHIResourceHandle m_RHIHandle;
    };
}
