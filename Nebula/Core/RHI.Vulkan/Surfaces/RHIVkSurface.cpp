#include "RHIVkSurface.h"

#include "../RHIVkInstance.h"
#include "Logger/Logger.h"
#include "RHI/Enums/ColorSpace.h"
#include "RHI/Enums/CompositeAlphaFlagBits.h"
#include "RHI/Enums/ImageUsageFlagBits.h"
#include "RHI/Enums/SharingMode.h"
#include "Windows/Platform.h"
#include "Windows/RenderWindowAPI.h"


using namespace NebulaEngine;

NebulaEngine::RHI::RHIVkSurface::~RHIVkSurface() noexcept
{
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
    m_SwapChain = std::make_unique<RHIVkSwapChain>(static_cast<VkDevice>(
                                                       rhiInstance->GetLogicalDevice(std::move(m_RenderWindowId))), m_VkSurface);
    auto width = Platforms::GetWindowWidth(m_RenderWindowId);
    auto height = Platforms::GetWindowHeight(m_RenderWindowId);

    auto swapChainPtr = m_SwapChain.get();
    swapChainPtr->SetResolution(width, height);

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
    
    SwapChainDescriptor desc
    {
        // TODO: deal with variable dpi 
        width, height, imageCount, 1,
        IMAGE_USAGE_COLOR_ATTACHMENT_BIT,
        queueFamilyIndexCount,
        static_cast<Format>(formats.format),
        static_cast<ColorSpace>(formats.colorSpace),
        sharingMode,
        static_cast<PresentMode>(presentMode),
        m_QueueFamilyIndices,
        true,
        static_cast<u32>(m_SwapChainSupportDetail.capabilities.currentTransform),
        COMPOSITE_ALPHA_OPAQUE_BIT
    };

    swapChainPtr->CreateSwapChainWithDesc(desc);
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
