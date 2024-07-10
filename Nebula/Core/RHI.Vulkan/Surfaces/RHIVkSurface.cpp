#include "RHIVkSurface.h"
#include "Logger/Logger.h"
#include "Platforms/Platform.h"
#include "Platforms/RenderWindow.h"


using namespace NebulaEngine;

NebulaEngine::RHI::RHIVkSurface::~RHIVkSurface() noexcept
{
    LOG_INFO("~RHIVkSurface");
    vkDestroySurfaceKHR(static_cast<VkInstance>(m_Instance->GetHandle()), m_Surface, nullptr);
}

NebulaEngine::RHI::RHIVkSurface::RHIVkSurface(u32&& id, std::shared_ptr<Instance> instance): Surface(std::move(id), instance)
{
    VkWin32SurfaceCreateInfoKHR createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_WIN32_SURFACE_CREATE_INFO_KHR;
    createInfo.hwnd = Platforms::GetWindowHandle(id);
    createInfo.hinstance = GetModuleHandle(nullptr);

    auto instancePtr = m_Instance.get();
    if (vkCreateWin32SurfaceKHR(static_cast<VkInstance>(instancePtr->GetHandle()), &createInfo, nullptr, &m_Surface) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to create window surface!");
    }
}
