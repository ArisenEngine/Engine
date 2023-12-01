#pragma once
#define VK_USE_PLATFORM_WIN32_KHR
#include <string>
#include <vulkan/vulkan.h>
#include "./Common.h"
#include "RHI/Instance.h"
#include "Logger/Logger.h"
#include "Devices/RHIVkDevice.h"

namespace NebulaEngine::RHI
{
    struct VulkanVersion
    {
        u32 variant, major, minor;
    };

    class DLL RHIVkInstance final : public Instance
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkInstance)
        
        RHIVkInstance(InstanceInfo&& app_info);
        ~RHIVkInstance() noexcept final override;

        [[nodiscard]] void* GetHandle() const final override { return m_Instance; }

        const std::string GetEnvString() const final override
        {
            return std::string(
                "vulkan"
                + std::to_string(m_VulkanVersion.major)
                + "." + std::to_string(m_VulkanVersion.minor));
        };

        VkInstance GetVkInstance() const { return m_Instance; }

    private:
        VkInstance m_Instance;
        VulkanVersion m_VulkanVersion;

        // devices
        RHIVkDevice* m_PhyscialDevice;
        void CreatePhysicalDevice();

        // debuger
        VkDebugUtilsMessengerEXT m_DebugMessenger;
        void SetupDebugMessager();
        void DisposeDebugMessager();

    };
}

extern "C" DLL NebulaEngine::RHI::Instance* CreateInstance(NebulaEngine::RHI::InstanceInfo&& app_info);
