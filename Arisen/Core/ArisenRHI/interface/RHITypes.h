#pragma once
#include <cstdint>

#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE

enum class RHI_DEVICE_TYPE : uint8_t
{
    RHI_DEVICE_TYPE_UNDEFINED = 0,
    RHI_DEVICE_TYPE_D3D12,
    RHI_DEVICE_TYPE_VULKAN
};

struct EngineCreateInfo
{
    bool EnableValidation{false};
};

ARISENRHI_END_NAMESPACE
