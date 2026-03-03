#include "Presentation/RHIVkSwapChain.h"
#include "Profiler.h"

using namespace ArisenEngine;
#include "Logger/Logger.h"
#include "Core/RHIVkDevice.h"
#include "Core/RHIVkFactory.h"
#include "RHI/Enums/Image/ECompositeAlphaFlagBits.h"
#include "RHI/Enums/Image/EImageAspectFlagBits.h"

ArisenEngine::RHI::RHIVkSwapChain::RHIVkSwapChain(RHIDevice* device, const RHIVkSurface* surface,
                                                  UInt32 maxFramesInFlight):
    RHISwapChain(maxFramesInFlight), m_Device(device), m_VkDevice(static_cast<VkDevice>(
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

    // Release semaphores here as they persist across SwapChain recreation
    auto* factory = m_Device->GetFactory();
    for (auto h : m_ImageAvailableSemaphores) factory->ReleaseSemaphore(h);
    for (auto h : m_RenderFinishSemaphores) factory->ReleaseSemaphore(h);
    m_ImageAvailableSemaphores.clear();
    m_RenderFinishSemaphores.clear();

    // Safety wait to ensure GPU is not using the swapchain
    // TODO: maybe we dont need to WaitForDevice,    // Targeted wait for presentation related work
    if (m_Device)
    {
        m_Device->GraphicQueueWaitIdle();
        m_Device->PresentQueueWaitIdle();
    }

    Cleanup();
}

void ArisenEngine::RHI::RHIVkSwapChain::CreateSwapChainWithDesc(RHISwapChainDescriptor desc)
{
    ARISEN_PROFILE_ZONE("RHI::VulkanCreateSwapChain");
    m_Desc = desc;

    VkSwapchainCreateInfoKHR createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR;
    createInfo.pNext = VK_NULL_HANDLE;
    createInfo.flags = static_cast<VkSwapchainCreateFlagsKHR>(m_Desc.swapChainCreateFlags);
    createInfo.surface = m_VkSurface;
    createInfo.minImageCount = m_Desc.imageCount;
    createInfo.imageFormat = static_cast<VkFormat>(m_Desc.colorFormat);
    createInfo.imageColorSpace = static_cast<VkColorSpaceKHR>(m_Desc.colorSpace);
    createInfo.imageExtent = {m_Desc.width, m_Desc.height};
    createInfo.imageArrayLayers = m_Desc.imageArrayLayers;
    createInfo.imageUsage = m_Desc.imageUsageFlagBits;
    createInfo.imageSharingMode = static_cast<VkSharingMode>(m_Desc.sharingMode);
    createInfo.queueFamilyIndexCount = m_Desc.queueFamilyIndexCount;
    auto queueSurfaceFamilyIndices = m_Surface->GetQueueFamilyIndices();
    uint32_t queueFamilyIndices[] = {
        queueSurfaceFamilyIndices.graphicsFamily.value(), queueSurfaceFamilyIndices.presentFamily.value()
    };
    createInfo.pQueueFamilyIndices = queueFamilyIndices;
    createInfo.preTransform = static_cast<VkSurfaceTransformFlagBitsKHR>(m_Desc.surfaceTransformFlagBits);
    createInfo.compositeAlpha = static_cast<VkCompositeAlphaFlagBitsKHR>(m_Desc.compositeAlphaFlagBits);
    createInfo.presentMode = static_cast<VkPresentModeKHR>(m_Desc.presentMode);
    createInfo.clipped = static_cast<VkBool32>(m_Desc.clipped);
    createInfo.oldSwapchain = VK_NULL_HANDLE;

    // Zero-Stall: Check if we have an old swapchain passed via customData
    if (m_Desc.customData != nullptr)
    {
        createInfo.oldSwapchain = (VkSwapchainKHR)m_Desc.customData;
    }

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
        // For RHISwapChain images, we manually allocate a handle since they are not created via factory
        m_ImageHandles[i] = vkDevice->GetImagePool()->Allocate([&images, i, this](RHIVkImagePoolItem* imageItem)
        {
            *imageItem = RHIVkImagePoolItem();
            imageItem->image = images[i];
            imageItem->width = m_Desc.width;
            imageItem->height = m_Desc.height;
            std::cout << "[RHIVkSwapChain] Setting image pool width=" << m_Desc.width << " height=" << m_Desc.height <<
                std::endl;
            imageItem->name = String::Format("SwapChainImage_%d", i);
            imageItem->needDestroy = false; // RHISwapChain owns these images
        });

        RHIImageViewDesc viewDesc;
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

ArisenEngine::RHI::RHIImageHandle ArisenEngine::RHI::RHIVkSwapChain::BeginFrame(UInt32 frameIndex)
{
    return AcquireCurrentImage(frameIndex);
}

void ArisenEngine::RHI::RHIVkSwapChain::EndFrame(UInt32 frameIndex)
{
    Present(frameIndex);
}

ArisenEngine::RHI::RHISemaphoreHandle ArisenEngine::RHI::RHIVkSwapChain::GetImageAvailableSemaphore(
    UInt32 currentFrame) const
{
    return m_ImageAvailableSemaphores[currentFrame % m_MaxFramesInFlight];
}

ArisenEngine::RHI::RHISemaphoreHandle ArisenEngine::RHI::RHIVkSwapChain::GetRenderFinishSemaphore(
    UInt32 currentFrame) const
{
    return m_RenderFinishSemaphores[currentFrame % m_MaxFramesInFlight];
}

ArisenEngine::RHI::RHIImageViewHandle ArisenEngine::RHI::RHIVkSwapChain::GetImageView(UInt32 frameIndex) const
{
    auto currentFrame = frameIndex % m_MaxFramesInFlight;
    return m_ImageViewHandles[m_AcquiredImageIndices[currentFrame]];
}

ArisenEngine::RHI::RHIImageHandle ArisenEngine::RHI::RHIVkSwapChain::AcquireCurrentImage(UInt32 frameIndex)
{
    ARISEN_PROFILE_ZONE("RHI::VulkanAcquireImage");
    auto currentFrame = frameIndex % m_MaxFramesInFlight;
    auto hSem = m_ImageAvailableSemaphores[currentFrame];
    auto* semItem = static_cast<RHIVkDevice*>(m_Device)->GetSemaphorePool()->Get(hSem);
    VkSemaphore vkSem = semItem ? semItem->semaphore : VK_NULL_HANDLE;

    uint32_t imageIndex_local = 0;
    VkResult result = vkAcquireNextImageKHR(m_VkDevice, m_VkSwapChain, UINT64_MAX, vkSem,
                                            VK_NULL_HANDLE, &imageIndex_local);

    if (result == VK_ERROR_OUT_OF_DATE_KHR)
    {
        // LOG_INFO("[RHIVkSwapChain::AcquireCurrentImage]: Out of date, triggering recreation");
        // We can't easily trigger recreation here without risk of recursion or synchronization issues in the middle of BeginFrame.
        // But we must signal that acquisition failed.
        return RHIImageHandle::Invalid();
    }

    if (result != VK_SUCCESS && result != VK_SUBOPTIMAL_KHR)
    {
        String msg = String::Format(
            "[RHIVkSwapChain::AcquireCurrentImage]: failed to acquire next image (frame %d) result: %d", frameIndex,
            result);
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

    for (auto h : m_ImageViewHandles)
    {
        factory->ReleaseImageView(h);
    }
    for (auto h : m_ImageHandles)
    {
        // RHISwapChain images are not created via Factory, so we should not call factory->ReleaseImage(h) 
        // if it tries to do full liberation. However, our ReleaseImage in factory calls Device::ReleaseImage.
        // For RHISwapChain images, needDestroy is false, so it's safe.
        factory->ReleaseImage(h);
    }
    m_ImageHandles.clear();
    m_ImageViewHandles.clear();

    // Do NOT destroy semaphores here. They are reused across Valid/Recreated swapchains.
    // They should be destroyed in Destructor.

    if (m_VkSwapChain != VK_NULL_HANDLE && m_VkDevice != VK_NULL_HANDLE)
    {
        LOG_INFO("[RHIVkSwapChain::~RHIVkSwapChain]: Destroy Vulkan RHISwapChain");
        vkDestroySwapchainKHR(m_VkDevice, m_VkSwapChain, nullptr);
    }
}

void ArisenEngine::RHI::RHIVkSwapChain::Present(UInt32 frameIndex)
{
    ARISEN_PROFILE_ZONE("RHI::VulkanPresent");
    auto currentFrame = frameIndex % m_MaxFramesInFlight;
    VkPresentInfoKHR presentInfo{};
    presentInfo.sType = VK_STRUCTURE_TYPE_PRESENT_INFO_KHR;

    presentInfo.waitSemaphoreCount = 1;
    auto hSem = m_RenderFinishSemaphores[currentFrame];
    auto* semItem = static_cast<RHIVkDevice*>(m_Device)->GetSemaphorePool()->Get(hSem);
    const VkSemaphore semaphore = semItem ? semItem->semaphore : VK_NULL_HANDLE;
    presentInfo.pWaitSemaphores = &semaphore;

    VkSwapchainKHR swapChains[] = {m_VkSwapChain};
    presentInfo.swapchainCount = 1;
    presentInfo.pSwapchains = swapChains;

    presentInfo.pImageIndices = &m_AcquiredImageIndices[currentFrame];

    vkQueuePresentKHR(m_VkPresentQueue, &presentInfo);
}

void ArisenEngine::RHI::RHIVkSwapChain::RecreateSwapChainIfNeeded()
{
    ARISEN_PROFILE_ZONE("RHI::VulkanRecreateSwapChain");
    if (m_VkSurface == VK_NULL_HANDLE || m_VkSwapChain == VK_NULL_HANDLE)
    {
        // currently we not init a swap chain 
        return;
    }

    // Zero-Stall: Do NOT wait idle.
    // m_Device->DeviceWaitIdle();

    VkSwapchainKHR oldSwapchain = m_VkSwapChain;
    m_VkSwapChain = VK_NULL_HANDLE; // Prevent Cleanup from destroying the old swapchain immediately

    Cleanup();

    // Reset acquired indices to prevent using stale indices from the old swapchain
    for (auto& idx : m_AcquiredImageIndices) idx = 0;

    // Pass old swapchain to Create functions
    m_Desc.customData = (void*)oldSwapchain;
    CreateSwapChainWithDesc(m_Desc);
    m_Desc.customData = nullptr; // Clear after use

    // Defer destroy oldSwapchain
    auto* vkDevice = static_cast<RHIVkDevice*>(m_Device);
    // Use the latest ticket from graphics queue as synchronization point
    // This ensures that we only destroy the old swapchain after all commands submitted UP TO NOW have finished.
    // Add delay to ensure presentation engine is done with the old swapchain
    auto* graphicsQueue = vkDevice->GetQueue(RHIQueueType::Graphics);
    auto ticket = graphicsQueue ? graphicsQueue->GetLatestTicket() + m_Device->GetMaxFramesInFlight() : m_Device->GetMaxFramesInFlight();
    RHIDeletionDependencies deps;
    deps.tickets[(int)RHIQueueType::Graphics] = ticket;
    vkDevice->EnqueueDeferredDestroy(deps,
                                     [dev = m_VkDevice, sw = oldSwapchain]()
                                     {
                                         // LOG_INFO("[RHIVkSwapChain] Destroying Old Swapchain (Deferred)");
                                         vkDestroySwapchainKHR(dev, sw, nullptr);
                                     });
}
