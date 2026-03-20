#pragma once
#include <vulkan/vulkan.h>
#include "Definitions/RHIVkCommon.h"
#include "RHI/Core/RHIInstance.h"
#include "Logger/Logger.h"
#include "Core/RHIVkDevice.h"
#include "Presentation/RHIVkSurface.h"

namespace ArisenEngine::RHI
{
}

namespace ArisenEngine::RHI
{
    struct VulkanInitSettings
    {
        Containers::Vector<const char*> validationLayers;
        Containers::Vector<const char*> instanceExtensions;
        Containers::Vector<const char*> mandatoryDeviceExtensions;
        Containers::Vector<const char*> optionalDeviceExtensions;
        
        static VulkanInitSettings GetDefault();
    };
}

namespace ArisenEngine::RHI
{
    struct VulkanVersion
    {
        UInt32 variant, major, minor;
    };

    class RHIVkInstance final : public RHIInstance
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkInstance)

        RHIVkInstance(RHIInstanceInfo&& app_info);
        ~RHIVkInstance() noexcept override;

        [[nodiscard]] void* GetHandle() const override { return m_VkInstance; }
        void InitLogicDevices() override;
        void PickPhysicalDevice(bool considerSurface = false) override;

        bool IsSupportLinearColorSpace(UInt32 windowId) override;
        bool PresentModeSupported(UInt32 windowId, EPresentMode mode) override;
        void SetCurrentPresentMode(UInt32 windowId, EPresentMode mode) override;
        EFormat GetSuitableSwapChainFormat(UInt32 windowId) override;
        EPresentMode GetSuitablePresentMode(UInt32 windowId) override;

        String GetEnvString() const override
        {
            return String::Format("vulkan%d.%d", m_VulkanVersion.major, m_VulkanVersion.minor);
        };

        VkInstance GetVkInstance() const { return m_VkInstance; }
        VkPhysicalDevice GetPhysicalDevice() const { return m_CurrentPhysicsDevice; }

        void CreateSurface(UInt32 windowId) override;
        void DestroySurface(UInt32 windowId) override;
        RHISurface& GetSurface(UInt32 windowId) override;
        void SetResolution(UInt32 windowId, UInt32 width, UInt32 height) override;

        bool IsPhysicalDeviceAvailable() const override { return m_CurrentPhysicsDevice != VK_NULL_HANDLE; }
        bool IsSurfacesAvailable() const override { return !m_Surfaces.empty(); }

        void CreateLogicDevice(UInt32 windowId) override;
        RHIDevice* GetLogicalDevice(UInt32 windowId) override;

        UInt32 GetExternalIndex() const override { return VK_SUBPASS_EXTERNAL; }

        void UpdateSurfaceCapabilities(RHISurface* surface) override;

    protected:
        void CheckSwapChainCapabilities() override;

    private:
        VulkanInitSettings m_Settings;
        VkInstance m_VkInstance;
        // devices
        VkPhysicalDevice m_CurrentPhysicsDevice{VK_NULL_HANDLE};
        VkPhysicalDeviceProperties m_DeviceProperties{};

        VulkanVersion m_VulkanVersion;

        // devices
        Containers::Map<UInt32, std::unique_ptr<RHIVkDevice>> m_LogicalDevices;
        Containers::Map<UInt32, std::unique_ptr<RHIVkSurface>> m_Surfaces;
        Containers::Map<UInt32, EPresentMode> m_PreferredPresentModes;

        ArisenEngine::RHI::VkQueueFamilyIndices FindQueueFamilies(VkSurfaceKHR surface);
        const VkSwapChainSupportDetail GetSwapChainSupportDetails(UInt32 windowId);
        const VkSwapChainSupportDetail QuerySwapChainSupport(const VkSurfaceKHR surface) const;

        // debuger
        VkDebugUtilsMessengerEXT m_VkDebugMessenger;

        void SetupDebugMessager();
        void DisposeDebugMessager();
    };
}

extern "C" RHI_VULKAN_DLL ArisenEngine::RHI::RHIInstance* CreateInstance(ArisenEngine::RHI::RHIInstanceInfo&& app_info);
