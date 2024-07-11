#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Devices/Device.h"
#include "../Common.h"
#include <optional>

#include "Logger/Logger.h"

namespace NebulaEngine::RHI
{
    struct QueueFamilyIndices
    {
        std::optional<uint32_t> graphicsFamily;
        std::optional<uint32_t> presentFamily;

        bool IsComplete() const
        {
            return graphicsFamily.has_value() && presentFamily.has_value();
        }
    };

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
            LOG_INFO("## vkDestroyDevice ##");
            vkDestroyDevice(vkDevice, nullptr);
        }
    };
    
    class DLL RHIVkDevice final: public Device
    {
        
    public:
        
        ~RHIVkDevice() noexcept override;
        void CreateLogicDevice(u32 windowId) override;
        
    private:

        friend class RHIVkInstance;
        RHIVkDevice(Instance& instance);
        void PickPhysicalDevice();

        // devices
        VkPhysicalDevice m_CurrentPhysicsDevice { VK_NULL_HANDLE };
        
        // Use an ordered map to automatically sort candidates by increasing score
        Containers::Multimap<int, std::pair<VkPhysicalDevice, QueueFamilyIndices>> m_Candidates;
        
        Containers::Map<u32, std::unique_ptr<LogicalDevice>> m_LogicalDevices;
    };
}
