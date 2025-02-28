﻿#include "RHIVkImageView.h"

#include "Logger/Logger.h"

ArisenEngine::RHI::RHIVkImageView::RHIVkImageView(ImageViewDesc desc, VkDevice device, VkImage image):
ImageView(), m_VkDevice(device)
{
    m_ImageViewDesc = desc;
    VkImageViewCreateInfo createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
    createInfo.image = image;
    createInfo.format = static_cast<VkFormat>(desc.format);
    createInfo.viewType = static_cast<VkImageViewType>(desc.type);
    createInfo.subresourceRange.aspectMask = static_cast<VkImageAspectFlags>(desc.aspectMask);
    createInfo.subresourceRange.baseMipLevel = desc.baseMipLevel;
    createInfo.subresourceRange.levelCount = desc.levelCount;
    createInfo.subresourceRange.baseArrayLayer = desc.baseArrayLayer;
    createInfo.subresourceRange.layerCount = desc.layerCount;

    if (desc.componentMapping.has_value())
    {
        createInfo.components.a = static_cast<VkComponentSwizzle>(desc.componentMapping.value().a);
        createInfo.components.r = static_cast<VkComponentSwizzle>(desc.componentMapping.value().r);
        createInfo.components.g = static_cast<VkComponentSwizzle>(desc.componentMapping.value().g);
        createInfo.components.b = static_cast<VkComponentSwizzle>(desc.componentMapping.value().b);
    }

    if (vkCreateImageView(device, &createInfo, nullptr, &m_VkImageView) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to create image views!");
    }

    LOG_INFO(" Vk Image View Created. ");
}

ArisenEngine::RHI::RHIVkImageView::~RHIVkImageView() noexcept
{
    vkDestroyImageView(m_VkDevice, m_VkImageView, nullptr);
    LOG_INFO("## Destroy Vulkan ImageView ##");
}
