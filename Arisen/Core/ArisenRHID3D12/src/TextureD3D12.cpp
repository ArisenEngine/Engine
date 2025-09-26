#include "TextureD3D12.h"

#include "ExceptionHandle.h"
#include "RenderContextD3D12.h"
#include "DebugUtils/Checks.h"

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

const RenderContextD3D12& TextureD3D12::GetRenderContextD3D12() const
{
    return static_cast<const RenderContextD3D12&>(m_context);
}

void TextureD3D12::CreateAsFrameBuffer()
{
    const TextureSettings& settings = GetSettings();
    VERIFY(settings.type == TextureType::FrameBuffer);
    // usage?
    VERIFY(settings.frame_index_opt.has_value());

    ComPtr<ID3D12Resource> resource_cptr;
    ThrowIfFailed(
        GetRenderContextD3D12().GetNativeSwapChain()->GetBuffer(settings.frame_index_opt.value(), IID_PPV_ARGS(&resource_cptr))
        );

    SetNativeResource(resource_cptr);
}

ARISENRHI_D3D12_END_NAMESPACE
