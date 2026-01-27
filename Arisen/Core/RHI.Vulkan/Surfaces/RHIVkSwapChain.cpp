#include "RHIVkSwapChain.h"

using namespace ArisenEngine;
#include "Logger/Logger.h"
#include "../Devices/RHIVkDevice.h"
#include "../Devices/RHIVkFactory.h"
#include "RHI/Enums/Image/CompositeAlphaFlagBits.h"
#include "RHI/Enums/Image/EImageAspectFlagBits.h"

ArisenEngine::RHI::RHIVkSwapChain::RHIVkSwapChain(RHIDevice* device, const RHIVkSurface* surface, UInt32 maxFramesInFlight):
SwapChain(maxFramesInFlight), m_Device(device), m_VkDevice(static_cast<VkDevice>(
            m_Device->GetHandle())),
m_VkSurface(static_cast<VkSurfaceKHR>(surface->GetHandle())), m_Surface(surface)
{
    
    auto* factory = m_Device->GetFactory();
    for (int i = 0; i < (int)m_MaxFramesInFlight; ++i)
    {
        m_ImageAvailableSemaphores.emplace_back(factory->CreateSemaphore());
        m_RenderFinishSemaphores.emplace_back(factory->CreateSemaphore());
        m_AcquiredImageIndices.push_back(0);
    }

    auto indices = surface->GetQueueFamilyIndices();
    vkGetDeviceQueue(m_VkDevice, indices.presentFamily.value(), 0, &m_VkPresentQueue);
}

ArisenEngine::RHI::RHIVkSwapChain::~RHIVkSwapChain() noexcept
{
    LOG_INFO("[RHIVkSwapChain::~RHIVkSwapChain]: ~RHIVkSwapChain");

    m_Surface = nullptr;
    
    m_ImageAvailableSemaphores.clear();
    m_RenderFinishSemaphores.clear();
    
    Cleanup();
}

void ArisenEngine::RHI::RHIVkSwapChain::CreateSwapChainWithDesc(SwapChainDescriptor desc)
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

    UInt32 actualImageCount = 0;
    Containers::Vector<VkImage> images;

    if (vkGetSwapchainImagesKHR(m_VkDevice, m_VkSwapChain, &actualImageCount, nullptr) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkSwapChain::CreateSwapChainWithDesc]: failed to query image count !");
    }
    
    m_ImageHandles.resize(actualImageCount);
    m_ImageViewHandles.resize(actualImageCount);
    images.resize(actualImageCount);

    if (vkGetSwapchainImagesKHR(m_VkDevice, m_VkSwapChain, &actualImageCount, images.data()) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkSwapChain::CreateSwapChainWithDesc]: failed to query images !");
    }
    
    auto* factory = m_Device->GetFactory();
    auto* vkDevice = static_cast<RHIVkDevice*>(m_Device);

    for (int i = 0; i < images.size(); ++i)
    {
        // For SwapChain images, we manually allocate a handle since they are not created via factory
        m_ImageHandles[i] = vkDevice->GetImagePool()->Allocate([&images, i](RHIVkImagePoolItem* imageItem) {
            *imageItem = RHIVkImagePoolItem();
            imageItem->image = images[i];
            imageItem->name = String::Format("SwapChainImage_%d", i);
            imageItem->needDestroy = false; // Swapchain owns these images
        });

        ImageViewDesc viewDesc;
        viewDesc.viewType = IMAGE_VIEW_TYPE_2D;
        viewDesc.format = m_Desc.colorFormat;
        viewDesc.aspectMask = IMAGE_ASPECT_COLOR_BIT;
        viewDesc.baseMipLevel = 0;
        viewDesc.levelCount = 1;
        viewDesc.baseArrayLayer = 0;
        viewDesc.layerCount = 1;
        viewDesc.width = m_Desc.width;
        viewDesc.height = m_Desc.height;

        m_ImageViewHandles[i] = factory->CreateImageView(m_ImageHandles[i], std::move(viewDesc));
    }
}

ArisenEngine::RHI::RHISemaphoreHandle ArisenEngine::RHI::RHIVkSwapChain::GetImageAvailableSemaphore(UInt32 currentFrame) const
{
    return m_ImageAvailableSemaphores[currentFrame % m_MaxFramesInFlight];
}

ArisenEngine::RHI::RHISemaphoreHandle ArisenEngine::RHI::RHIVkSwapChain::GetRenderFinishSemaphore(UInt32 currentFrame) const
{
    return m_RenderFinishSemaphores[currentFrame % m_MaxFramesInFlight];
}

ArisenEngine::RHI::RHIImageViewHandle ArisenEngine::RHI::RHIVkSwapChain::GetImageView(UInt32 frameIndex) const
{
    auto currentFrame = frameIndex % m_MaxFramesInFlight;
    return m_ImageViewHandles[m_AcquiredImageIndices[currentFrame]];
}

ArisenEngine::RHI::RHIImageHandle ArisenEngine::RHI::RHIVkSwapChain::AquireCurrentImage(UInt32 frameIndex)
{
    auto currentFrame = frameIndex % m_MaxFramesInFlight;
    auto hSem = m_ImageAvailableSemaphores[currentFrame];
    auto* semItem = static_cast<RHIVkDevice*>(m_Device)->GetSemaphorePool()->Get(hSem);
    VkSemaphore vkSem = semItem ? semItem->semaphore : VK_NULL_HANDLE;

    uint32_t imageIndex_local = 0;
    VkResult result = vkAcquireNextImageKHR(m_VkDevice, m_VkSwapChain, UINT64_MAX, vkSem,
                              VK_NULL_HANDLE, &imageIndex_local);
    if (result != VK_SUCCESS && result != VK_SUBOPTIMAL_KHR)
    {
        String msg = String::Format("[RHIVkSwapChain::AquireCurrentImage]: failed to acquire next image (frame %d) result: %d", frameIndex, result);
        LOG_ERROR(msg);
        return RHIImageHandle::Invalid();
    }
    m_AcquiredImageIndices[currentFrame] = imageIndex_local;
    return m_ImageHandles[imageIndex_local];
}

void ArisenEngine::RHI::RHIVkSwapChain::Cleanup()
{
    auto* factory = m_Device->GetFactory();
    auto* vkDevice = static_cast<RHIVkDevice*>(m_Device);

    for (auto h : m_ImageViewHandles) {
        factory->ReleaseImageView(h);
    }
    for (auto h : m_ImageHandles) {
        // Swapchain images are not created via Factory, so we should not call factory->ReleaseImage(h) 
        // if it tries to do full liberation. However, our ReleaseImage in factory calls Device::ReleaseImage.
        // For swapchain images, needDestroy is false, so it's safe.
        factory->ReleaseImage(h);
    }
    m_ImageHandles.clear();
    m_ImageViewHandles.clear();

    for (auto h : m_ImageAvailableSemaphores) factory->ReleaseSemaphore(h);
    for (auto h : m_RenderFinishSemaphores) factory->ReleaseSemaphore(h);
    m_ImageAvailableSemaphores.clear();
    m_RenderFinishSemaphores.clear();

    if (m_VkSwapChain != VK_NULL_HANDLE && m_VkDevice != VK_NULL_HANDLE)
    {
        LOG_INFO("[RHIVkSwapChain::~RHIVkSwapChain]: Destroy Vulkan SwapChain");
        vkDestroySwapchainKHR(m_VkDevice, m_VkSwapChain, nullptr);
    }
}

void ArisenEngine::RHI::RHIVkSwapChain::Present(UInt32 frameIndex)
{
    auto currentFrame = frameIndex % m_MaxFramesInFlight;
    VkPresentInfoKHR presentInfo{};
    presentInfo.sType = VK_STRUCTURE_TYPE_PRESENT_INFO_KHR;

    presentInfo.waitSemaphoreCount = 1;
    auto hSem = m_RenderFinishSemaphores[currentFrame];
    auto* semItem = static_cast<RHIVkDevice*>(m_Device)->GetSemaphorePool()->Get(hSem);
    const VkSemaphore semaphore = semItem ? semItem->semaphore : VK_NULL_HANDLE;
    presentInfo.pWaitSemaphores = &semaphore;

    VkSwapchainKHR swapChains[] = { m_VkSwapChain };
    presentInfo.swapchainCount = 1;
    presentInfo.pSwapchains = swapChains;

    presentInfo.pImageIndices = &m_AcquiredImageIndices[currentFrame];

    {
        std::lock_guard<std::mutex> lock(static_cast<RHIVkDevice*>(m_Device)->GetSubmitMutex());
        vkQueuePresentKHR(m_VkPresentQueue, &presentInfo);
    }
}

void ArisenEngine::RHI::RHIVkSwapChain::RecreateSwapChainIfNeeded()
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
