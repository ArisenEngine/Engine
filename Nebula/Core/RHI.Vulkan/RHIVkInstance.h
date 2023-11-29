#pragma once
#define VK_USE_PLATFORM_WIN32_KHR
#include <vulkan/vulkan.h>
#include "./Common.h"
#include "RHI/Instance.h"
#include "Logger/Logger.h"

namespace NebulaEngine::RHI
{
    class DLL RHIVkInstance final : public Instance
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkInstance)
        RHIVkInstance(AppInfo&& app_info);
        ~RHIVkInstance() noexcept final override;

        void* GetHandle() final override { return m_Instance; }
    
    private:
        
        VkInstance m_Instance;
        VkDebugUtilsMessengerEXT m_DebugMessenger;

        void SetupDebugMessager();
        void DisposeDebugMessager();
    };
}

extern "C" DLL NebulaEngine::RHI::Instance* CreateInstance(NebulaEngine::RHI::AppInfo&& app_info);