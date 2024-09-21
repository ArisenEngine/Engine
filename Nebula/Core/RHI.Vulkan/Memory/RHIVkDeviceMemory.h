#pragma once
#include "RHI/Memory/DeviceMemory.h"
#include "vulkan_core.h"

namespace NebulaEngine::RHI
{
    class RHIVkDeviceMemory final : DeviceMemory
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDeviceMemory)
        VIRTUAL_DECONSTRUCTOR(RHIVkDeviceMemory)

    private:
        VkDeviceMemory m_VkDeviceMemory;
    };
}
