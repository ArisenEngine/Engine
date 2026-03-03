#pragma once

#include "./Base/FoundationMinimal.h"
#include <vulkan/vulkan_core.h>

#ifdef _DEBUG
#define RHI_VALIDATION
#endif

#ifdef RHIVULKAN_EXPORTS

#define RHI_VULKAN_DLL   __declspec( dllexport )

#else

#define RHI_VULKAN_DLL   __declspec( dllimport )

#endif

extern "C" RHI_VULKAN_DLL void dummy_vulkan_function();

inline void dummy_vulkan_function()
{
}

namespace ArisenEngine::RHI
{
    struct VkSwapChainSupportDetail
    {
        VkSurfaceCapabilitiesKHR capabilities;
        ArisenEngine::Containers::Vector<VkSurfaceFormatKHR> formats;
        ArisenEngine::Containers::Vector<VkPresentModeKHR> presentModes;
    };

    struct VkQueueFamilyIndices
    {
        VkQueueFamilyIndices() = default;
        std::optional<uint32_t> graphicsFamily;
        std::optional<uint32_t> presentFamily;
        std::optional<uint32_t> computeFamily;
        std::optional<uint32_t> transferFamily;

        bool IsComplete() const
        {
            return graphicsFamily.has_value() && presentFamily.has_value();
        }
    };

    // Removed hardcoded layers and extensions; these are now in VulkanInitSettings in RHIVkInstance.h
}
