#include "RHIVkSwapChain.h"
#include "Logger/Logger.h"
#include "RHI/Enums/Image/ImageAspectFlagBits.h"

NebulaEngine::RHI::RHIVkSwapChain::RHIVkSwapChain(VkDevice device, const RHIVkSurface* surface):
SwapChain(), m_VkDevice(device), m_VkSurface(static_cast<VkSurfaceKHR>(surface->GetHandle())), m_ImageIndex(0), m_Surface(surface)
{
    m_ImageAvailableSemaphore = new RHIVkSemaphore(m_VkDevice);
    m_RenderFinishSemaphore = new RHIVkSemaphore(m_VkDevice);

    auto indices = surface->GetQueueFamilyIndices();
    vkGetDeviceQueue(device, indices.presentFamily.value(), 0, &m_VkPresentQueue);
}

NebulaEngine::RHI::RHIVkSwapChain::~RHIVkSwapChain() noexcept
{
    LOG_INFO("[RHIVkSwapChain::~RHIVkSwapChain]: ~RHIVkSwapChain");

    m_Surface = nullptr;
    
    delete m_ImageAvailableSemaphore;
    delete m_RenderFinishSemaphore;
    
    m_ImageHandles.clear();
    if (m_VkSwapChain != VK_NULL_HANDLE && m_VkDevice != VK_NULL_HANDLE)
    {
        LOG_INFO("[RHIVkSwapChain::~RHIVkSwapChain]: Destroy Vulkan SwapChain");
        vkDestroySwapchainKHR(m_VkDevice, m_VkSwapChain, nullptr);
    }
}

void NebulaEngine::RHI::RHIVkSwapChain::CreateSwapChainWithDesc(Surface* surface, SwapChainDescriptor desc)
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
    auto queueSurfaceFamilyIndices = static_cast<RHIVkSurface*>(surface)->GetQueueFamilyIndices();
    uint32_t queueFamilyIndices[] = {queueSurfaceFamilyIndices.graphicsFamily.value(), queueSurfaceFamilyIndices.presentFamily.value()};
    createInfo.pQueueFamilyIndices = queueFamilyIndices;
    createInfo.preTransform = static_cast<VkSurfaceTransformFlagBitsKHR>(m_Desc.m_SurfaceTransformFlagBits);
    createInfo.compositeAlpha = static_cast<VkCompositeAlphaFlagBitsKHR>(m_Desc.m_CompositeAlphaFlagBits);
    createInfo.presentMode = static_cast<VkPresentModeKHR>(m_Desc.m_PresentMode);
    createInfo.clipped = static_cast<VkBool32>(m_Desc.clipped);
    createInfo.oldSwapchain = VK_NULL_HANDLE;

    if (vkCreateSwapchainKHR(m_VkDevice, &createInfo, nullptr, &m_VkSwapChain) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkSwapChain::CreateSwapChainWithDesc]: failed to create swap chain!");
    }

    LOG_DEBUG("[RHIVkSwapChain::CreateSwapChainWithDesc]: vkSwapchain Created .");

    u32 actualImageCount = 0;
    Containers::Vector<VkImage> images;
    vkGetSwapchainImagesKHR(m_VkDevice, m_VkSwapChain, &actualImageCount, nullptr);
    m_ImageHandles.resize(actualImageCount);
    images.resize(actualImageCount);
    vkGetSwapchainImagesKHR(m_VkDevice, m_VkSwapChain, &actualImageCount, images.data());
    for (int i = 0; i < images.size(); ++i)
    {
        VkImage image = images[i];
        ImageViewDesc desc
        {
            0,
            IMAGE_VIEW_TYPE_2D,
            m_Desc.m_ColorFormat,
            {
                COMPONENT_SWIZZLE_IDENTITY,
                COMPONENT_SWIZZLE_IDENTITY,
                COMPONENT_SWIZZLE_IDENTITY,
                COMPONENT_SWIZZLE_IDENTITY
            },
            m_Desc.m_Width, m_Desc.m_Height,
            IMAGE_ASPECT_COLOR_BIT,
            0,1,0,1
        };
        
        m_ImageHandles[i] = std::make_unique<RHIVkImageHandle>(m_VkDevice, image, desc);
    }

}

NebulaEngine::RHI::RHISemaphore* NebulaEngine::RHI::RHIVkSwapChain::GetImageAvailableSemaphore() const
{
    return m_ImageAvailableSemaphore;
}

NebulaEngine::RHI::RHISemaphore* NebulaEngine::RHI::RHIVkSwapChain::GetRenderFinishSemaphore() const
{
    return m_RenderFinishSemaphore;
}

NebulaEngine::RHI::ImageHandle* NebulaEngine::RHI::RHIVkSwapChain::AquireCurrentImage()
{
    vkAcquireNextImageKHR(m_VkDevice, m_VkSwapChain, UINT64_MAX, static_cast<VkSemaphore>(
                              m_ImageAvailableSemaphore->GetHandle()), VK_NULL_HANDLE, &m_ImageIndex);
    return m_ImageHandles[m_ImageIndex].get();
}

void NebulaEngine::RHI::RHIVkSwapChain::Present()
{
    VkPresentInfoKHR presentInfo{};
    presentInfo.sType = VK_STRUCTURE_TYPE_PRESENT_INFO_KHR;

    presentInfo.waitSemaphoreCount = 1;
    const VkSemaphore semaphore = static_cast<VkSemaphore>(m_RenderFinishSemaphore->GetHandle());
    presentInfo.pWaitSemaphores = &semaphore;

    VkSwapchainKHR swapChains[] = { m_VkSwapChain };
    presentInfo.swapchainCount = 1;
    presentInfo.pSwapchains = swapChains;

    presentInfo.pImageIndices = &m_ImageIndex;

    vkQueuePresentKHR(m_VkPresentQueue, &presentInfo);
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
