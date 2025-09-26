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

enum class TextureFormat : uint16_t
{
    UnKnown = 0u,
    RGBA8,
    RGBA8Unorm,
    RGBA8Unorm_sRGB,
    BGRA8Unorm,
    BGRA8Unorm_sRGB,
};

struct AttachmentFormats
{
    std::vector<TextureFormat> colors;
    TextureFormat depth = TextureFormat::UnKnown;
    TextureFormat stencil = TextureFormat::UnKnown;
};

ARISENRHI_END_NAMESPACE
