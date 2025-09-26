#pragma once
#include "IResource.h"
#include "ResourceViewBase.h"
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

    Opt<uint32_t> frame_index_opt;
};

class TextureViewBase;
struct ITexture : IResource
{
    virtual const TextureSettings& GetSettings() const = 0;
    [[nodiscard]] virtual TextureViewBase GetTextureView() = 0;
};

class TextureViewBase: public ResourceViewBase
{
public:
    TextureViewBase(ITexture& texture);
private:
    Ptr<ITexture> m_texture_ptr;
};


ARISENRHI_END_NAMESPACE