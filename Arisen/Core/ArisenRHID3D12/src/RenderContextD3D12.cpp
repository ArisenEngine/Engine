#include "RenderContextD3D12.h"

#include <dxgi1_2.h>
#include <dxgi1_3.h>
#include <dxgi1_5.h>

#include "ExceptionHandle.h"
#include "RenderPassD3D12.h"
#include "RenderPatternD3D12.h"
#include "RenderPipelineStateObjectD3D12.h"
#include "TextureD3D12.h"
#include "TypeConverterDX.h"
#include "ViewStateD3D12.h"
#include "DebugUtils/Checks.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
    RenderContextD3D12::RenderContextD3D12(DeviceD3D12& device, RenderContextSettings settings,Environment environment)
    :ContextCommonD3D12(device, settings),m_environment(environment)
{
    LOG_RHI_CONSTRUCTOR("RenderContextD3D12");
}

void RenderContextD3D12::Initialize()
{
    const RenderContextSettings settings = GetSettings();
    // create command queue and swapchain.
    DXGI_SWAP_CHAIN_DESC1 swap_chain_desc{};
    swap_chain_desc.Width = settings.frame_size.GetWidth();
    swap_chain_desc.Height = settings.frame_size.GetHeight();
    swap_chain_desc.Format = TextureFormatToDXGI_Format(settings.texture_format);
    swap_chain_desc.Stereo = false;
    swap_chain_desc.BufferCount = settings.frame_buffers_Count;
    swap_chain_desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    swap_chain_desc.Scaling = DXGI_SCALING_NONE;
    swap_chain_desc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD;
    swap_chain_desc.AlphaMode = DXGI_ALPHA_MODE_IGNORE;
    swap_chain_desc.SampleDesc.Count = 1;
    swap_chain_desc.Flags = DXGI_SWAP_CHAIN_FLAG_FRAME_LATENCY_WAITABLE_OBJECT;

    UINT dxgiFactoryFlags = 0;
#ifdef _DEBUG
    dxgiFactoryFlags |= DXGI_CREATE_FACTORY_DEBUG;
#endif

    ComPtr<IDXGIFactory5> factory_cptr;
    ThrowIfFailed(CreateDXGIFactory2(dxgiFactoryFlags, IID_PPV_ARGS(&factory_cptr)));
    CHECK(factory_cptr,"factory is null, create swapchain failed!");

    // TODO:
    ID3D12Device* device_d3d12 = nullptr;
    BOOL presentTearingSupport = FALSE;
    ThrowIfFailed(factory_cptr->CheckFeatureSupport(DXGI_FEATURE_PRESENT_ALLOW_TEARING, &presentTearingSupport, sizeof(presentTearingSupport)), device_d3d12);
    if (presentTearingSupport)
    {
        swap_chain_desc.Flags |= DXGI_FEATURE_PRESENT_ALLOW_TEARING;
        mIsTearingSupported = true;
    }
    else
    {
        mIsTearingSupported = false;
    }

    ComPtr<IDXGISwapChain1> swap_chain_cptr;
    // lazy create command queue first.
    ID3D12CommandQueue& command_queue = GetDefaultCommandQueueD3D12(CommandListType::Render).GetNativeCommandQueue();
    ThrowIfFailed(factory_cptr->CreateSwapChainForHwnd(&command_queue, m_environment.window_handle, &swap_chain_desc,
        nullptr, nullptr, &swap_chain_cptr), device_d3d12);

    CHECK(swap_chain_cptr, "Failed to create swap chain");
    LOG_RHI_DEBUG("swap chain created!");

    ThrowIfFailed(swap_chain_cptr.As(&m_swap_chain_cptr), device_d3d12);
    
    m_swap_chain_cptr->SetMaximumFrameLatency(settings.frame_buffers_Count);
    m_frame_latency_waitable_object = m_swap_chain_cptr->GetFrameLatencyWaitableObject();
    CHECK(m_frame_latency_waitable_object, "Failed to get frame latency object");
    
    ThrowIfFailed(factory_cptr->MakeWindowAssociation(m_environment.window_handle, DXGI_MWA_NO_ALT_ENTER), device_d3d12);
    UpdateFrameBufferIndex();

    // create descriptor.
    ContextCommonD3D12::Initialize();
}

Ptr<IRenderPattern> RenderContextD3D12::CreateRenderPattern(const RenderPatternSettings& Settings) noexcept
{
    return MakePtr(RenderPatternD3D12, *this, Settings);
}

Ptr<IViewState> RenderContextD3D12::CreateViewState(const ViewSettings& view_settings) noexcept
{
    return MakePtr(ViewStateD3D12, view_settings);
}

Ptr<IRenderPass> RenderContextD3D12::CreateRenderPass(const IRenderPattern& render_pattern,
    const RenderPassSettings& settings) noexcept
{
    return MakePtr(RenderPassD3D12, render_pattern, settings);
}

Ptr<IRenderPipelineStateObject> RenderContextD3D12::CreateRenderPipelineStateObject(const RenderPipelineStateObjectSettings& settings) noexcept
{
    return MakePtr(RenderPipelineStateObjectD3D12, *this, settings);
}

Ptr<ITexture> RenderContextD3D12::CreateTexture(const TextureSettings& settings) const
{
    return MakePtr(TextureD3D12, *this, settings);
}

uint32_t RenderContextD3D12::GetNextFrameBufferIndex()
{
    VERIFY_NOT_NULL(m_swap_chain_cptr,"");
    return m_swap_chain_cptr->GetCurrentBackBufferIndex();
}


ARISENRHI_D3D12_END_NAMESPACE
