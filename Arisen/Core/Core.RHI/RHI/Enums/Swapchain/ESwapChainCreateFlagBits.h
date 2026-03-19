#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_PACKAGE("com.arisen.rhi.core")
ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(SwapchainCreateFlagBits)

    typedef enum SwapchainCreateFlagBits
    {
        SWAPCHAIN_CREATE_SPLIT_INSTANCE_BIND_REGIONS_BIT = 0x00000001,
        SWAPCHAIN_CREATE_PROTECTED_BIT = 0x00000002,
        SWAPCHAIN_CREATE_MUTABLE_FORMAT_BIT = 0x00000004,
        SWAPCHAIN_CREATE_DEFERRED_MEMORY_ALLOCATION_BIT_EXT = 0x00000008,
        SWAPCHAIN_CREATE_FLAG_BITS_MAX_ENUM_KHR = 0x7FFFFFFF
    } SwapchainCreateFlagBits;
}

