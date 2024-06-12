#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Devices/Device.h"
#include "../Common.h"
#include <optional>

namespace NebulaEngine::RHI
{
    struct QueueFamilyIndices
    {
        std::optional<uint32_t> graphicsFamily;

        bool IsComplete()
        {
            return graphicsFamily.has_value();
        }
            
    };
    
    class DLL RHIVkDevice final: public Device
    {
        
    public:
        
        ~RHIVkDevice() noexcept;
        
    private:

        friend class RHIVkInstance;
        RHIVkDevice(Instance& instance);

        // devices
        VkPhysicalDevice m_CurrentPhysicsDevice { VK_NULL_HANDLE };
        // Use an ordered map to automatically sort candidates by increasing score
        Containers::multimap<int, std::pair<VkPhysicalDevice, QueueFamilyIndices>> m_Candidates;

        VkDevice m_LogicalDevice;
        VkQueue m_GraphicsQueue;
        
        // queue family
        QueueFamilyIndices m_QueueFamilyIndices {};
    };
}
