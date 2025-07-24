#pragma once
#include <cstdint>
#include "Constants.h"
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

    uint32_t AdapterId{DEFAULT_ADAPTER_ID};
};

ARISENRHI_END_NAMESPACE
