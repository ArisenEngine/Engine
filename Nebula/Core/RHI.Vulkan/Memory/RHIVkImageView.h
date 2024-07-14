#pragma once
#include "../Common.h"
#include "RHI/Memory/MemoryView.h"

namespace NebulaEngine::RHI
{
    class RHIVkImageView final : public MemoryView
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkImageView)
        RHIVkImageView(ImageViewDesc desc, VkDevice device, VkImage image);
        ~RHIVkImageView() noexcept override;
    private:
        VkDevice m_VkDevice;
        VkImageView m_VkImageView;
    };
}
