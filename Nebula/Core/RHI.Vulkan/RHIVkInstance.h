#pragma once
#define VK_USE_PLATFORM_WIN32_KHR
#include <vulkan/vulkan.h>
#include "./Common.h"
#include "RHI/Instance.h"

namespace NebulaEngine::RHI
{
    class DLL VkInstance final : public Instance
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(VkInstance)
        VkInstance(AppInfo&& app_info);
        ~VkInstance() noexcept final override;
    private:
        
        VkInstance m_Instance;
        
    };
}

extern "C" DLL NebulaEngine::RHI::Instance * CreateInstance();