#include "RHIVkSurface.h"

#include "../RHIVkInstance.h"
#include "Logger/Logger.h"
#include "RHI/Enums/ImageUsageFlagBits.h"
#include "Windows/Platform.h"
#include "Windows/RenderWindowAPI.h"


using namespace NebulaEngine;

NebulaEngine::RHI::RHIVkSurface::~RHIVkSurface() noexcept
{
    LOG_INFO("~RHIVkSurface");
    vkDestroySurfaceKHR(static_cast<VkInstance>(m_Instance->GetHandle()), m_VkSurface, nullptr);
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
        LOG_FATAL_AND_THROW("failed to create window surface!");
    }

}

void RHI::RHIVkSurface::InitSwapChain()
{
    LOG_INFO("InitSwapChain");
    
    if (m_VkSurface == VK_NULL_HANDLE)
    {
        LOG_FATAL_AND_THROW("Should init VkSurfachKHR first.");
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

    SwapChainDescriptor desc
    {
        width, height,
        imageCount, 1,
        IMAGE_USAGE_COLOR_ATTACHMENT_BIT,
        
        
    };

    swapChainPtr->CreateSwapChainWithDesc(desc);
}
