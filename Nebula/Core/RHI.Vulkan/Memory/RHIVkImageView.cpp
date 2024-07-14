#include "RHIVkImageView.h"

#include "Logger/Logger.h"

NebulaEngine::RHI::RHIVkImageView::RHIVkImageView(ImageViewDesc desc, VkDevice device, VkImage image):
MemoryView(IMAGE_MEMORY_VIEW_TYPE), m_VkDevice(device)
{
    m_ImageViewDesc = desc;
    VkImageViewCreateInfo createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
    createInfo.components.a = static_cast<VkComponentSwizzle>(desc.componentMapping.a);
    createInfo.components.r = static_cast<VkComponentSwizzle>(desc.componentMapping.r);
    createInfo.components.g = static_cast<VkComponentSwizzle>(desc.componentMapping.g);
    createInfo.components.b = static_cast<VkComponentSwizzle>(desc.componentMapping.b);
    createInfo.image = image;
    createInfo.format = static_cast<VkFormat>(desc.format);
    createInfo.viewType = static_cast<VkImageViewType>(desc.type);
    createInfo.subresourceRange.aspectMask = static_cast<VkImageAspectFlags>(desc.aspectMask);
    createInfo.subresourceRange.baseMipLevel = desc.baseMipLevel;
    createInfo.subresourceRange.levelCount = desc.levelCount;
    createInfo.subresourceRange.baseArrayLayer = desc.baseArrayLayer;
    createInfo.subresourceRange.layerCount = desc.layerCount;

    if (vkCreateImageView(device, &createInfo, nullptr, &m_VkImageView) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to create image views!");
    }

    LOG_INFO(" Vk Image View Created. ");
}

NebulaEngine::RHI::RHIVkImageView::~RHIVkImageView() noexcept
{
    vkDestroyImageView(m_VkDevice, m_VkImageView, nullptr);
    LOG_INFO("## Destroy Vulkan ImageView ##");
}
