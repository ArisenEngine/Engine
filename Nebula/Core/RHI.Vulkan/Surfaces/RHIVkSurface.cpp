#include "RHIVkSurface.h"

#include "../RHIVkInstance.h"
#include "Logger/Logger.h"
#include "RHI/Enums/Image/CompositeAlphaFlagBits.h"
#include "RHI/Enums/Image/ImageUsageFlagBits.h"
#include "Windows/Platform.h"
#include "Windows/RenderWindowAPI.h"


using namespace NebulaEngine;

NebulaEngine::RHI::RHIVkSurface::~RHIVkSurface() noexcept
{
    delete m_SwapChain;
    vkDestroySurfaceKHR(static_cast<VkInstance>(m_Instance->GetHandle()), m_VkSurface, nullptr);
    LOG_INFO("[RHIVkSurface::~RHIVkSurface]: Destroy Vulkan Surface");
}

NebulaEngine::RHI::RHIVkSurface::RHIVkSurface(u32&& id, Instance* instance):
Surface(std::move(id), instance), m_SwapChainSupportDetail({}), m_SwapChain(nullptr)
{
    VkWin32SurfaceCreateInfoKHR createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_WIN32_SURFACE_CREATE_INFO_KHR;
    createInfo.hwnd = Platforms::GetWindowHandle(id);
    createInfo.hinstance = GetModuleHandle(nullptr);
    
    if (vkCreateWin32SurfaceKHR(static_cast<VkInstance>(m_Instance->GetHandle()), &createInfo, nullptr, &m_VkSurface) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkSurface::RHIVkSurface]: failed to create window surface!");
    }

}

void RHI::RHIVkSurface::InitSwapChain()
{
    LOG_DEBUG("[RHIVkSurface::InitSwapChain]: InitSwapChain");
    
    if (m_VkSurface == VK_NULL_HANDLE)
    {
        LOG_FATAL_AND_THROW("[RHIVkSurface::InitSwapChain]: Should init VkSurfachKHR first.");
    }
    
    auto rhiInstance = static_cast<RHIVkInstance*>(m_Instance);
    m_SwapChain = new RHIVkSwapChain(
        static_cast<VkDevice>(rhiInstance->GetLogicalDevice(std::move(m_RenderWindowId)).GetHandle()),
        this, rhiInstance->GetMaxFramesInFlight());
    auto width = Platforms::GetWindowWidth(m_RenderWindowId);
    auto height = Platforms::GetWindowHeight(m_RenderWindowId);
    
    m_SwapChain->SetResolution(width, height);

    auto imageCount = m_SwapChainSupportDetail.capabilities.minImageCount + 1;
    if (m_SwapChainSupportDetail.capabilities.maxImageCount > 0
        && imageCount > m_SwapChainSupportDetail.capabilities.maxImageCount)
    {
        imageCount = m_SwapChainSupportDetail.capabilities.maxImageCount;
    }

    auto formats = GetDefaultSurfaceFormat();
    auto presentMode = GetDefaultSwapPresentMode();
    width = std::clamp(width, m_SwapChainSupportDetail.capabilities.minImageExtent.width, m_SwapChainSupportDetail.capabilities.maxImageExtent.width);
    height = std::clamp(height, m_SwapChainSupportDetail.capabilities.minImageExtent.height, m_SwapChainSupportDetail.capabilities.maxImageExtent.height);

    u32 queueFamilyIndexCount;
    SharingMode sharingMode;
    if (m_QueueFamilyIndices.graphicsFamily != m_QueueFamilyIndices.presentFamily)
    {
        sharingMode = SHARING_MODE_CONCURRENT;
        queueFamilyIndexCount = 2;
       
    } else {
        sharingMode = SHARING_MODE_EXCLUSIVE;
        queueFamilyIndexCount = 0; // Optional
    }
    
    SwapChainDescriptor desc {};
    desc.width = width;
    desc.height = height;
    desc.imageCount = imageCount;
    desc.imageArrayLayers = 1;
    desc.imageUsageFlagBits = IMAGE_USAGE_COLOR_ATTACHMENT_BIT;
    desc.queueFamilyIndexCount = queueFamilyIndexCount;
    desc.colorFormat = static_cast<Format>(formats.format);
    desc.colorSpace = static_cast<ColorSpace>(formats.colorSpace);
    desc.sharingMode = sharingMode;
    desc.presentMode = static_cast<PresentMode>(presentMode);
    desc.clipped = true;
    desc.surfaceTransformFlagBits = static_cast<u32>(m_SwapChainSupportDetail.capabilities.currentTransform);
    desc.compositeAlphaFlagBits = COMPOSITE_ALPHA_OPAQUE_BIT;
   
    m_SwapChain->CreateSwapChainWithDesc(this, desc);
}

RHI::SwapChain* RHI::RHIVkSurface::GetSwapChain()
{
    return m_SwapChain;
}

VkSurfaceFormatKHR RHI::RHIVkSurface::GetDefaultSurfaceFormat()
{
    for (const auto& availableFormat : m_SwapChainSupportDetail.formats)
    {
        if (availableFormat.format == VK_FORMAT_B8G8R8A8_SRGB && availableFormat.colorSpace == VK_COLOR_SPACE_SRGB_NONLINEAR_KHR)
        {
            return availableFormat;
        }
    }
    
    return m_SwapChainSupportDetail.formats[0];
}

VkPresentModeKHR RHI::RHIVkSurface::GetDefaultSwapPresentMode()
{
    for (const auto& availablePresentMode : m_SwapChainSupportDetail.presentModes)
    {
        if (availablePresentMode == VK_PRESENT_MODE_MAILBOX_KHR)
        {
            return availablePresentMode;
        }
    }
    
    return VK_PRESENT_MODE_FIFO_KHR;
}
