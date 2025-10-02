#include "TextureD3D12.h"

#include "ExceptionHandle.h"
#include "RenderContextD3D12.h"
#include "TypeConverterDX.h"
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

Opt<ResourceDescriptor> TextureD3D12::InitializeNativeViewDescriptor(const ResourceViewId& view_id)
{
    const TextureSettings& settings = GetSettings();
    const ResourceDescriptor& descriptor = GetDescriptorByViewId(view_id);

    switch (settings.type)
    {
    case TextureType::Image:
        break;
    case TextureType::RenderTarget:
        break;
    case TextureType::FrameBuffer:
        {
            if (view_id.usage.HasAnyBit(ResourceUsage::RenderTarget))
            {
                CreateRenderTargetView(descriptor, view_id);
            }
        }
        break;
    case TextureType::DepthStencil:
        break;
    default: ;
    }
    return descriptor;
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

static D3D12_RENDER_TARGET_VIEW_DESC CreateRenderTargetViewDesc(const TextureSettings& settings, const ResourceViewId& view_id)
{
    D3D12_RENDER_TARGET_VIEW_DESC desc = {};
    switch (settings.dimension_type)
    {
    case TextureDimensionType::Tex2D:
        // desc.Texture2D.MipSlice =
        desc.ViewDimension = D3D12_RTV_DIMENSION_TEXTURE2D;
        break;
    default: ;
    }

    desc.Format = TextureFormatToDXGI_Format(settings.format);
    return desc;
}

void TextureD3D12::CreateRenderTargetView(const ResourceDescriptor& descriptor, const ResourceViewId& view_id) const
{
    const D3D12_RENDER_TARGET_VIEW_DESC desc = CreateRenderTargetViewDesc(GetSettings(), view_id);
    GetRenderContextD3D12().GetDeviceD3D12().GetNativeDevice()->CreateRenderTargetView(GetNativeResource(),&desc, GetNativeCpuDescriptorHandle(descriptor));
}

ARISENRHI_D3D12_END_NAMESPACE
