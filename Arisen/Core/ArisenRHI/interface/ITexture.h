#pragma once
#include "IResource.h"
#include "RHIMacros.h"
#include "RHITypes.h"

ARISENRHI_BEGIN_NAMEPSACE
    enum class TextureType : uint32_t
{
    Image = 0,
    RenderTarget,
    FrameBuffer,
    DepthStencil
};

struct TextureSettings
{
    TextureType type = TextureType::Image;
    TextureDimensionType dimension_type = TextureDimensionType::Tex2D;
    TextureFormat format = TextureFormat::UnKnown;
    
};

struct ITexture : IResource
{
    virtual const TextureSettings& GetSettings() const;
};
ARISENRHI_END_NAMESPACE