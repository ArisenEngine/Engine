#pragma once

#include "./Common/CommandHeaders.h"
#include <vulkan/vulkan_core.h>

#ifdef RHIVULKAN_EXPORTS

#define DLL   __declspec( dllexport )

#else

#define DLL   __declspec( dllimport )

#endif

namespace NebulaEngine::RHI
{
    struct VkSwapChainSupportDetail
    {
        VkSurfaceCapabilitiesKHR capabilities;
        NebulaEngine::Containers::Vector<VkSurfaceFormatKHR> formats;
        NebulaEngine::Containers::Vector<VkPresentModeKHR> presentModes;
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

        // VkQueueFamilyIndices(const VkQueueFamilyIndices&& copy) noexcept
        // {
        //     graphicsFamily = copy.graphicsFamily;
        //     presentFamily = copy.presentFamily;
        // }
    };

    // validation layers
    static NebulaEngine::Containers::Vector<const char*> VkValidationLayers
    {
        "VK_LAYER_KHRONOS_validation"
    };
}

