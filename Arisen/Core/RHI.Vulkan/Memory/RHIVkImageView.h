#pragma once
#include "../Common.h"
#include "RHI/Enums/Image/Format.h"
#include "RHI/Memory/ImageView.h"

namespace ArisenEngine::RHI
{
    class RHIVkImageView final : public ImageView
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkImageView)
        RHIVkImageView(ImageViewDesc desc, VkDevice device, VkImage image);
        ~RHIVkImageView() noexcept override;
        void* GetView() override { return m_VkImageView; }
        void* GetViewPointer() override { return &m_VkImageView; }

        const UInt32 GetWidth() const override { return m_ImageViewDesc.value().width; }
        const UInt32 GetHeight() const override { return m_ImageViewDesc.value().height; }
        const UInt32 GetLayerCount() const override { return m_ImageViewDesc.value().layerCount; }
        const Format GetFormat() const override { return m_ImageViewDesc.value().format; }
        
    private:
        VkDevice m_VkDevice;
        VkImageView m_VkImageView;
    };
}
