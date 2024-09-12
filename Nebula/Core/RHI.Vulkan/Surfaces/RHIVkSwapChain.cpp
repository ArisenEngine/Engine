#include "RHIVkSwapChain.h"
#include "Logger/Logger.h"
#include "RHI/Enums/Image/ImageAspectFlagBits.h"

NebulaEngine::RHI::RHIVkSwapChain::RHIVkSwapChain(const Device* device, const RHIVkSurface* surface, u32 maxFramesInFlight):
SwapChain(maxFramesInFlight), m_Device(device), m_VkDevice(static_cast<VkDevice>(
            m_Device->GetHandle())),
m_VkSurface(static_cast<VkSurfaceKHR>(surface->GetHandle())), m_ImageIndex(0), m_Surface(surface)
{
    
    for (int i = 0; i < m_MaxFramesInFlight; ++i)
    {
        m_ImageAvailableSemaphores.emplace_back(std::make_unique<RHIVkSemaphore>(m_VkDevice));
        m_RenderFinishSemaphores.emplace_back(std::make_unique<RHIVkSemaphore>(m_VkDevice));
    }

    auto indices = surface->GetQueueFamilyIndices();
    vkGetDeviceQueue(m_VkDevice, indices.presentFamily.value(), 0, &m_VkPresentQueue);
}

NebulaEngine::RHI::RHIVkSwapChain::~RHIVkSwapChain() noexcept
{
    LOG_INFO("[RHIVkSwapChain::~RHIVkSwapChain]: ~RHIVkSwapChain");

    m_Surface = nullptr;
    
    m_ImageAvailableSemaphores.clear();
    m_RenderFinishSemaphores.clear();
    
    Cleanup();
}

void NebulaEngine::RHI::RHIVkSwapChain::CreateSwapChainWithDesc(SwapChainDescriptor desc)
{
    
    m_Desc = desc;
    
    VkSwapchainCreateInfoKHR createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR;
    createInfo.pNext = VK_NULL_HANDLE;
    createInfo.flags = static_cast<VkSwapchainCreateFlagsKHR>(m_Desc.swapChainCreateFlags);
    createInfo.surface = m_VkSurface;
    createInfo.minImageCount = m_Desc.imageCount;
    createInfo.imageFormat = static_cast<VkFormat>(m_Desc.colorFormat);
    createInfo.imageColorSpace = static_cast<VkColorSpaceKHR>( m_Desc.colorSpace);
    createInfo.imageExtent = { m_Desc.width,  m_Desc.height};
    createInfo.imageArrayLayers =  m_Desc.imageArrayLayers;
    createInfo.imageUsage =  m_Desc.imageUsageFlagBits;
    createInfo.imageSharingMode = static_cast<VkSharingMode>(m_Desc.sharingMode);
    createInfo.queueFamilyIndexCount = m_Desc.queueFamilyIndexCount;
    auto queueSurfaceFamilyIndices = m_Surface->GetQueueFamilyIndices();
    uint32_t queueFamilyIndices[] = {queueSurfaceFamilyIndices.graphicsFamily.value(), queueSurfaceFamilyIndices.presentFamily.value()};
    createInfo.pQueueFamilyIndices = queueFamilyIndices;
    createInfo.preTransform = static_cast<VkSurfaceTransformFlagBitsKHR>(m_Desc.surfaceTransformFlagBits);
    createInfo.compositeAlpha = static_cast<VkCompositeAlphaFlagBitsKHR>(m_Desc.compositeAlphaFlagBits);
    createInfo.presentMode = static_cast<VkPresentModeKHR>(m_Desc.presentMode);
    createInfo.clipped = static_cast<VkBool32>(m_Desc.clipped);
    createInfo.oldSwapchain = VK_NULL_HANDLE;

    if (vkCreateSwapchainKHR(m_VkDevice, &createInfo, nullptr, &m_VkSwapChain) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkSwapChain::CreateSwapChainWithDesc]: failed to create swap chain!");
    }

    LOG_DEBUG("[RHIVkSwapChain::CreateSwapChainWithDesc]: vkSwapchain Created .");

    u32 actualImageCount = 0;
    Containers::Vector<VkImage> images;

    if (vkGetSwapchainImagesKHR(m_VkDevice, m_VkSwapChain, &actualImageCount, nullptr) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkSwapChain::CreateSwapChainWithDesc]: failed to query image count !");
    }
    
    m_ImageHandles.resize(actualImageCount);
    images.resize(actualImageCount);

    if (vkGetSwapchainImagesKHR(m_VkDevice, m_VkSwapChain, &actualImageCount, images.data()) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkSwapChain::CreateSwapChainWithDesc]: failed to query images !");
    }
    
    for (int i = 0; i < images.size(); ++i)
    {
        VkImage image = images[i];
        ImageViewDesc desc
        {
            0,
            IMAGE_VIEW_TYPE_2D,
            m_Desc.colorFormat,
            {
                COMPONENT_SWIZZLE_IDENTITY,
                COMPONENT_SWIZZLE_IDENTITY,
                COMPONENT_SWIZZLE_IDENTITY,
                COMPONENT_SWIZZLE_IDENTITY
            },
            m_Desc.width, m_Desc.height,
            IMAGE_ASPECT_COLOR_BIT,
            0,1,0,1
        };
        
        m_ImageHandles[i] = std::make_unique<RHIVkImageHandle>(m_VkDevice, image, desc);
    }

}

NebulaEngine::RHI::RHISemaphore* NebulaEngine::RHI::RHIVkSwapChain::GetImageAvailableSemaphore(u32 currentFrame) const
{
    return m_ImageAvailableSemaphores[currentFrame % m_MaxFramesInFlight].get();
}

NebulaEngine::RHI::RHISemaphore* NebulaEngine::RHI::RHIVkSwapChain::GetRenderFinishSemaphore(u32 currentFrame) const
{
    return m_RenderFinishSemaphores[currentFrame % m_MaxFramesInFlight].get();
}

NebulaEngine::RHI::ImageHandle* NebulaEngine::RHI::RHIVkSwapChain::AquireCurrentImage(u32 frameIndex)
{
    auto currentFrame = frameIndex % m_MaxFramesInFlight;
    if (vkAcquireNextImageKHR(m_VkDevice, m_VkSwapChain, UINT64_MAX, static_cast<VkSemaphore>(
                              m_ImageAvailableSemaphores[currentFrame]->GetHandle()),
                              VK_NULL_HANDLE, &m_ImageIndex) != VK_SUCCESS)
    {
        LOG_DEBUG("[RHIVkSwapChain::AquireCurrentImage]: failed to acquire next image.");
    }
    return m_ImageHandles[m_ImageIndex].get();
}

void NebulaEngine::RHI::RHIVkSwapChain::Cleanup()
{
    m_ImageHandles.clear();
    if (m_VkSwapChain != VK_NULL_HANDLE && m_VkDevice != VK_NULL_HANDLE)
    {
        LOG_INFO("[RHIVkSwapChain::~RHIVkSwapChain]: Destroy Vulkan SwapChain");
        vkDestroySwapchainKHR(m_VkDevice, m_VkSwapChain, nullptr);
    }
}

void NebulaEngine::RHI::RHIVkSwapChain::Present(u32 frameIndex)
{
    VkPresentInfoKHR presentInfo{};
    presentInfo.sType = VK_STRUCTURE_TYPE_PRESENT_INFO_KHR;

    presentInfo.waitSemaphoreCount = 1;
    const VkSemaphore semaphore =
        static_cast<VkSemaphore>(m_RenderFinishSemaphores[frameIndex % m_MaxFramesInFlight]->GetHandle());
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
    
    m_Device->DeviceWaitIdle();

    Cleanup();

    CreateSwapChainWithDesc(m_Desc);
}
