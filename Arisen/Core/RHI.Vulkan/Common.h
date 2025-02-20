#pragma once

#include "./Common/CommandHeaders.h"
#include <vulkan/vulkan_core.h>

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

        bool IsComplete() const
        {
            return graphicsFamily.has_value() && presentFamily.has_value();
        }
    };

    // validation layers
    static ArisenEngine::Containers::Vector<const char*> VkValidationLayers
    {
        "VK_LAYER_KHRONOS_validation"
    };
    

    static ArisenEngine::Containers::Vector<const char*> VkDeviceExtensionNames
    {
        VK_KHR_SWAPCHAIN_EXTENSION_NAME
    };

    static ArisenEngine::Containers::Vector<const char*> VkInstanceExtensionNames
    {
        VK_EXT_DEBUG_UTILS_EXTENSION_NAME,
        "VK_KHR_win32_surface",
        "VK_KHR_surface"
    };
}

