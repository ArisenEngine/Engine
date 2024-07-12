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
    createInfo.surface = m_VkSurface;
    createInfo.minImageCount = m_Desc.m_ImageCount;
    createInfo.imageFormat = static_cast<VkFormat>(m_Desc.m_Format);
    createInfo.imageColorSpace = static_cast<VkColorSpaceKHR>( m_Desc.m_ColorSpace);
    createInfo.imageExtent = { m_Desc.m_Width,  m_Desc.m_Height};
    createInfo.imageArrayLayers =  m_Desc.m_ImageArrayLayers;
    createInfo.imageUsage =  m_Desc.m_ImageUsageFlagBits;
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
