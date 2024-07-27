#pragma once
#include <string>
#include <vulkan/vulkan.h>
#include "./Common.h"
#include "RHI/Instance.h"
#include "Logger/Logger.h"
#include "Devices/RHIVkDevice.h"
#include "RHI/Enums/Format.h"
#include "RHI/Enums/PresentMode.h"
#include "Surfaces/RHIVkSurface.h"

namespace NebulaEngine::RHI
{
    struct VulkanVersion
    {
        u32 variant, major, minor;
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

        bool IsSupportLinearColorSpace(u32&& windowId) override;
        bool PresentModeSupported(u32&& windowId, PresentMode mode) override;
        void SetCurrentPresentMode(u32&& windowId, PresentMode mode) override;
        const Format GetSuitableSwapChainFormat(u32&& windowId) override;
        const PresentMode GetSuitablePresentMode(u32&& windowId) override;
        
        const std::wstring GetEnvString() const override
        {
            return std::wstring(
                L"vulkan"
                + std::to_wstring(m_VulkanVersion.major)
                + L"." + std::to_wstring(m_VulkanVersion.minor));
        };
        
        VkInstance GetVkInstance() const { return m_VkInstance; }

        void CreateSurface(u32&& windowId) override;
        void DestroySurface(u32&& windowId) override;
        Surface& GetSurface(u32&& windowId) override;
        void SetResolution(const u32&& windowId, const u32&& width, const u32&& height) override;

        bool IsPhysicalDeviceAvailable() const override { return m_CurrentPhysicsDevice != VK_NULL_HANDLE; }
        bool IsSurfacesAvailable() const override { return !m_Surfaces.empty(); }
        
        void CreateLogicDevice(u32 windowId) override;
        Device& GetLogicalDevice(u32 windowId) override;
    protected:
        
        void CheckSwapChainCapabilities() override;
        
    private:
        VkInstance m_VkInstance;
        // devices
        VkPhysicalDevice m_CurrentPhysicsDevice { VK_NULL_HANDLE };
        VkPhysicalDeviceProperties m_DeviceProperties {};
        
        VulkanVersion m_VulkanVersion;

        // devices
        Containers::Map<u32, std::unique_ptr<RHIVkDevice>> m_LogicalDevices;
        Containers::Map<u32, std::unique_ptr<RHIVkSurface>> m_Surfaces;

        NebulaEngine::RHI::VkQueueFamilyIndices FindQueueFamilies(VkSurfaceKHR surface);
        const VkSwapChainSupportDetail GetSwapChainSupportDetails(u32&& windowId);
        const VkSwapChainSupportDetail QuerySwapChainSupport(const VkSurfaceKHR surface) const;
        
        // debuger
        VkDebugUtilsMessengerEXT m_VkDebugMessenger;
        
        void SetupDebugMessager();
        void DisposeDebugMessager();

    };
}

extern "C" RHI_VULKAN_DLL NebulaEngine::RHI::Instance* CreateInstance(NebulaEngine::RHI::InstanceInfo&& app_info);
