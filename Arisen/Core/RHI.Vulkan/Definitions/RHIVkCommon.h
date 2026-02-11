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
    

    // device extensions
    static ArisenEngine::Containers::Vector<const char*> VkMandatoryDeviceExtensionNames
    {
        VK_KHR_SWAPCHAIN_EXTENSION_NAME,
        VK_KHR_TIMELINE_SEMAPHORE_EXTENSION_NAME,
        VK_KHR_SYNCHRONIZATION_2_EXTENSION_NAME,
        VK_KHR_DYNAMIC_RENDERING_EXTENSION_NAME,
        VK_KHR_BUFFER_DEVICE_ADDRESS_EXTENSION_NAME
    };

    static ArisenEngine::Containers::Vector<const char*> VkOptionalDeviceExtensionNames
    {
        VK_EXT_MESH_SHADER_EXTENSION_NAME,
        VK_KHR_ACCELERATION_STRUCTURE_EXTENSION_NAME,
        VK_KHR_RAY_TRACING_PIPELINE_EXTENSION_NAME,
        VK_KHR_RAY_QUERY_EXTENSION_NAME,
        VK_KHR_DEFERRED_HOST_OPERATIONS_EXTENSION_NAME,
        VK_EXT_ROBUSTNESS_2_EXTENSION_NAME
    };

    static ArisenEngine::Containers::Vector<const char*> VkInstanceExtensionNames
    {
        VK_EXT_DEBUG_UTILS_EXTENSION_NAME,
        VK_EXT_LAYER_SETTINGS_EXTENSION_NAME,
        "VK_KHR_win32_surface",
        "VK_KHR_surface"
    };
}






