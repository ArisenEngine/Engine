#include "TypeConverterDX.h"

#include "DebugUtils/Checks.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
    DXGI_FORMAT TextureFormatToDXGI_Format(TextureFormat textureFormat)
{
    switch (textureFormat)
    {
    case TextureFormat::UnKnown:            return DXGI_FORMAT_UNKNOWN;
    case TextureFormat::RGBA8:              return DXGI_FORMAT_B8G8R8A8_TYPELESS;
    case TextureFormat::RGBA8Unorm:         return DXGI_FORMAT_R8G8B8A8_UNORM;
    case TextureFormat::RGBA8Unorm_sRGB:    return DXGI_FORMAT_R8G8B8A8_UNORM_SRGB;
    case TextureFormat::BGRA8Unorm:         return DXGI_FORMAT_B8G8R8A8_UNORM;
    case TextureFormat::BGRA8Unorm_sRGB:    return DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
    default: CHECK_UNEXPECTED_RETURN(textureFormat, DXGI_FORMAT_UNKNOWN);
    }
}
ARISENRHI_D3D12_END_NAMESPACE