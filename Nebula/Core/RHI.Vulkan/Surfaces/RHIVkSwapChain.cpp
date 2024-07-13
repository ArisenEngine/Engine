#include "RHIVkSwapChain.h"

#include "Logger/Logger.h"

NebulaEngine::RHI::RHIVkSwapChain::RHIVkSwapChain(VkDevice device, VkSurfaceKHR surface):
SwapChain(), m_VkDevice(device), m_VkSurface(surface)
{
    
}

NebulaEngine::RHI::RHIVkSwapChain::~RHIVkSwapChain() noexcept
{
    LOG_INFO("~RHIVkSwapChain");
    if (m_VkSwapChain != VK_NULL_HANDLE && m_VkDevice != VK_NULL_HANDLE)
    {
        LOG_INFO("## Destroy Vulkan SwapChain ##");
        vkDestroySwapchainKHR(m_VkDevice, m_VkSwapChain, nullptr);
    }
}

void NebulaEngine::RHI::RHIVkSwapChain::CreateSwapChainWithDesc(SwapChainDescriptor desc)
{
    m_Desc = desc;
    
    VkSwapchainCreateInfoKHR createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR;
    createInfo.pNext = VK_NULL_HANDLE;
    createInfo.flags = static_cast<VkSwapchainCreateFlagsKHR>(m_Desc.m_SwapChainCreateFlags);
    createInfo.surface = m_VkSurface;
    createInfo.minImageCount = m_Desc.m_ImageCount;
    createInfo.imageFormat = static_cast<VkFormat>(m_Desc.m_ColorFormat);
    createInfo.imageColorSpace = static_cast<VkColorSpaceKHR>( m_Desc.m_ColorSpace);
    createInfo.imageExtent = { m_Desc.m_Width,  m_Desc.m_Height};
    createInfo.imageArrayLayers =  m_Desc.m_ImageArrayLayers;
    createInfo.imageUsage =  m_Desc.m_ImageUsageFlagBits;
    createInfo.imageSharingMode = static_cast<VkSharingMode>(m_Desc.m_SharingMode);
    createInfo.queueFamilyIndexCount = m_Desc.m_QueueFamilyIndexCount;
    if (m_Desc.m_VkQueueFamilyIndices.has_value())
    {
        uint32_t queueFamilyIndices[] = {m_Desc.m_VkQueueFamilyIndices.value().graphicsFamily.value(), m_Desc.m_VkQueueFamilyIndices.value().presentFamily.value()};
        createInfo.pQueueFamilyIndices = queueFamilyIndices;
    }
    createInfo.preTransform = static_cast<VkSurfaceTransformFlagBitsKHR>(m_Desc.m_SurfaceTransformFlagBits);
    createInfo.compositeAlpha = static_cast<VkCompositeAlphaFlagBitsKHR>(m_Desc.m_CompositeAlphaFlagBits);
    createInfo.presentMode = static_cast<VkPresentModeKHR>(m_Desc.m_PresentMode);
    createInfo.clipped = static_cast<VkBool32>(m_Desc.clipped);
    createInfo.oldSwapchain = VK_NULL_HANDLE;

    if (vkCreateSwapchainKHR(m_VkDevice, &createInfo, nullptr, &m_VkSwapChain) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to create swap chain!");
    }

    LOG_INFO(" vkSwapchain Created .");
}

void NebulaEngine::RHI::RHIVkSwapChain::RecreateSwapChainIfNeeded()
{
    if (m_VkSurface == VK_NULL_HANDLE || m_VkSwapChain == VK_NULL_HANDLE)
    {
        // currently we not init a swap chain 
        return;
    }
    
    // TODO
}
