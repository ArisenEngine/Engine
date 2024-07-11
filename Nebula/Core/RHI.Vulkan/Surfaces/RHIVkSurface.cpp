#include "RHIVkSurface.h"
#include "Logger/Logger.h"
#include "Windows/Platform.h"
#include "Windows/RenderWindowAPI.h"


using namespace NebulaEngine;

NebulaEngine::RHI::RHIVkSurface::~RHIVkSurface() noexcept
{
    LOG_INFO("~RHIVkSurface");
    vkDestroySurfaceKHR(static_cast<VkInstance>(m_Instance->GetHandle()), m_Surface, nullptr);
}

NebulaEngine::RHI::RHIVkSurface::RHIVkSurface(u32&& id, Instance* instance): Surface(std::move(id), instance)
{
    VkWin32SurfaceCreateInfoKHR createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_WIN32_SURFACE_CREATE_INFO_KHR;
    createInfo.hwnd = Platforms::GetWindowHandle(id);
    createInfo.hinstance = GetModuleHandle(nullptr);
    
    if (vkCreateWin32SurfaceKHR(static_cast<VkInstance>(m_Instance->GetHandle()), &createInfo, nullptr, &m_Surface) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to create window surface!");
    }
}
