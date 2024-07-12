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
    struct SwapChainSupportDetail
    {
        VkSurfaceCapabilitiesKHR capabilities;
        NebulaEngine::Containers::Vector<VkSurfaceFormatKHR> formats;
        NebulaEngine::Containers::Vector<VkPresentModeKHR> presentModes;
    };
}

// validation layers
static NebulaEngine::Containers::Vector<const char*> ValidationLayers
{
    "VK_LAYER_KHRONOS_validation"
};