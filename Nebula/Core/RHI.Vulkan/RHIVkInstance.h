#pragma once
#include <string>
#include <vulkan/vulkan.h>
#include "./Common.h"
#include "RHI/Instance.h"
#include "Logger/Logger.h"
#include "Devices/RHIVkDevice.h"
#include "Surfaces/RHIVkSurface.h"

namespace NebulaEngine::RHI
{
    struct VulkanVersion
    {
        UInt32 variant, major, minor;
    };

    class RHIVkInstance final : public Instance
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkInstance)
        
        RHIVkInstance(InstanceInfo&& app_info);
        ~RHIVkInstance() noexcept override;

        [[nodiscard]] void* GetHandle() const override { return m_VkInstance; }
        void InitLogicDevices() override;
        void PickPhysicalDevice(bool considerSurface = false) override;

        bool IsSupportLinearColorSpace(UInt32&& windowId) override;
        bool PresentModeSupported(UInt32&& windowId, PresentMode mode) override;
        void SetCurrentPresentMode(UInt32&& windowId, PresentMode mode) override;
        const Format GetSuitableSwapChainFormat(UInt32&& windowId) override;
        const PresentMode GetSuitablePresentMode(UInt32&& windowId) override;
        
        const std::wstring GetEnvString() const override
        {
            return std::wstring(
                L"vulkan"
                + std::to_wstring(m_VulkanVersion.major)
                + L"." + std::to_wstring(m_VulkanVersion.minor));
        };
        
        VkInstance GetVkInstance() const { return m_VkInstance; }

        void CreateSurface(UInt32&& windowId) override;
        void DestroySurface(UInt32&& windowId) override;
        Surface& GetSurface(UInt32&& windowId) override;
        void SetResolution(const UInt32&& windowId, const UInt32&& width, const UInt32&& height) override;

        bool IsPhysicalDeviceAvailable() const override { return m_CurrentPhysicsDevice != VK_NULL_HANDLE; }
        bool IsSurfacesAvailable() const override { return !m_Surfaces.empty(); }
        
        void CreateLogicDevice(UInt32 windowId) override;
        Device* GetLogicalDevice(UInt32 windowId) override;
        
        const UInt32 GetExternalIndex() const override { return VK_SUBPASS_EXTERNAL; }

        void UpdateSurfaceCapabilities(Surface* surface) override;
    protected:
        
        void CheckSwapChainCapabilities() override;
        
    private:
        VkInstance m_VkInstance;
        // devices
        VkPhysicalDevice m_CurrentPhysicsDevice { VK_NULL_HANDLE };
        VkPhysicalDeviceProperties m_DeviceProperties {};
        
        VulkanVersion m_VulkanVersion;

        // devices
        Containers::Map<UInt32, std::unique_ptr<RHIVkDevice>> m_LogicalDevices;
        Containers::Map<UInt32, std::unique_ptr<RHIVkSurface>> m_Surfaces;

        NebulaEngine::RHI::VkQueueFamilyIndices FindQueueFamilies(VkSurfaceKHR surface);
        const VkSwapChainSupportDetail GetSwapChainSupportDetails(UInt32&& windowId);
        const VkSwapChainSupportDetail QuerySwapChainSupport(const VkSurfaceKHR surface) const;
        
        // debuger
        VkDebugUtilsMessengerEXT m_VkDebugMessenger;
        
        void SetupDebugMessager();
        void DisposeDebugMessager();

    };
}

extern "C" RHI_VULKAN_DLL NebulaEngine::RHI::Instance* CreateInstance(NebulaEngine::RHI::InstanceInfo&& app_info);
