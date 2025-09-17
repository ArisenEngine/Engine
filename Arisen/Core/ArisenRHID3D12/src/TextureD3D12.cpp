#include "TextureD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
TextureD3D12::TextureD3D12(const IRHIContext& context, const TextureSettings& settings)
    :ResourceD3D12(context, settings)
{
    // create real resource.
    switch (settings.type)
    {
    case TextureType::Image:
        break;
    case TextureType::RenderTarget:
        break;
    case TextureType::FrameBuffer:
        break;
    case TextureType::DepthStencil:
        break;
    default: ;
    }
}

void TextureD3D12::CreateAsFrameBuffer()
{
    const TextureSettings& settings = GetSettings();
    /
}

ARISENRHI_D3D12_END_NAMESPACE
