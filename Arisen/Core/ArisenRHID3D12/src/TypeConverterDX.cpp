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

DXGI_FORMAT ParameterDescToDXGIFormatAndSize(const D3D12_SIGNATURE_PARAMETER_DESC& param_desc,
    uint32_t& out_element_byte_size)
{
    const uint32_t component_32bit_byte_size = 4;
    if (param_desc.Mask == 1)
    {
        out_element_byte_size = component_32bit_byte_size;
        if (param_desc.ComponentType == D3D_REGISTER_COMPONENT_UINT32)
            return DXGI_FORMAT_R32_UINT;
        else if (param_desc.ComponentType == D3D_REGISTER_COMPONENT_SINT32)
            return DXGI_FORMAT_R32_SINT;
        else if (param_desc.ComponentType == D3D_REGISTER_COMPONENT_FLOAT32)
            return DXGI_FORMAT_R32_FLOAT;
    }
    else if (param_desc.Mask <= 3)
    {
        out_element_byte_size = 2 * component_32bit_byte_size;
        if (param_desc.ComponentType == D3D_REGISTER_COMPONENT_UINT32)
            return DXGI_FORMAT_R32G32_UINT;
        else if (param_desc.ComponentType == D3D_REGISTER_COMPONENT_SINT32)
            return DXGI_FORMAT_R32G32_SINT;
        else if (param_desc.ComponentType == D3D_REGISTER_COMPONENT_FLOAT32)
            return DXGI_FORMAT_R32G32_FLOAT;
    }
    else if (param_desc.Mask <= 7)
    {
        out_element_byte_size = 3 * component_32bit_byte_size;
        if (param_desc.ComponentType == D3D_REGISTER_COMPONENT_UINT32)
            return DXGI_FORMAT_R32G32B32_UINT;
        else if (param_desc.ComponentType == D3D_REGISTER_COMPONENT_SINT32)
            return DXGI_FORMAT_R32G32B32_SINT;
        else if (param_desc.ComponentType == D3D_REGISTER_COMPONENT_FLOAT32)
            return DXGI_FORMAT_R32G32B32_FLOAT;
    }
    else if (param_desc.Mask <= 15)
    {
        out_element_byte_size = 4 * component_32bit_byte_size;
        if (param_desc.ComponentType == D3D_REGISTER_COMPONENT_UINT32)
            return DXGI_FORMAT_R32G32B32A32_UINT;
        else if (param_desc.ComponentType == D3D_REGISTER_COMPONENT_SINT32)
            return DXGI_FORMAT_R32G32B32A32_SINT;
        else if (param_desc.ComponentType == D3D_REGISTER_COMPONENT_FLOAT32)
            return DXGI_FORMAT_R32G32B32A32_FLOAT;
    }

    out_element_byte_size = 0;
    return DXGI_FORMAT_UNKNOWN;
}
ARISENRHI_D3D12_END_NAMESPACE
