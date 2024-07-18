#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Devices/Device.h"
#include "../Surfaces/RHIVkSurface.h"
#include "../Common.h"
#include <optional>

#include "Logger/Logger.h"

namespace NebulaEngine::RHI
{
    struct LogicalDevice
    {
        VkQueue vkGraphicQueue;
        VkQueue vkPresentQueue;
        VkDevice vkDevice;

        NO_COPY_NO_MOVE_NO_DEFAULT(LogicalDevice)
        
        LogicalDevice(VkQueue graphicQueue, VkQueue presentQueue, VkDevice device)
        : vkGraphicQueue(graphicQueue),
          vkPresentQueue(presentQueue),
          vkDevice(device)
        {}

        ~LogicalDevice()
        {
            vkDestroyDevice(vkDevice, nullptr);
            LOG_INFO("[LogicalDevice::~LogicalDevice]: Destroy Vulkan Logical Device");
        }
    };

    

    class RHIVkDevice final: public Device
    {
        
    public:
        
        ~RHIVkDevice() noexcept override;
        
        
    private:

        friend class RHIVkInstance;
        RHIVkDevice(Instance* instance);
        void PickPhysicalDevice();

        // devices
        VkPhysicalDevice m_CurrentPhysicsDevice { VK_NULL_HANDLE };
        VkPhysicalDeviceProperties m_DeviceProperties {};
        
        bool IsPhysicalDeviceAvailable() const override { return m_CurrentPhysicsDevice != VK_NULL_HANDLE; }
        bool IsSurfaceAvailable() const override { return !m_Surfaces.empty(); }
        const VkSwapChainSupportDetail GetSwapChainSupportDetails(u32&& windowId);
    
        Containers::Map<u32, std::unique_ptr<LogicalDevice>> m_LogicalDevices;
        Containers::Map<u32, std::unique_ptr<Surface>> m_Surfaces;

        void CreateLogicDevice(u32 windowId) override;
        void InitLogicDevices() override;
        void* GetLogicalDevice(u32 windowId) override;
        const VkSwapChainSupportDetail QuerySwapChainSupport(const VkSurfaceKHR surface) const;

        void CreateSurface(u32&& windowId) override;
        void DestroySurface(u32&& windowId) override;
        const Surface& GetSurface(u32&& windowId) override;
        void SetResolution(const u32&& windowId, const u32&& width, const u32&& height) override;
        void CheckSwapChainCapabilities() override;

        NebulaEngine::RHI::VkQueueFamilyIndices FindQueueFamilies(VkSurfaceKHR surface);
    };
}
