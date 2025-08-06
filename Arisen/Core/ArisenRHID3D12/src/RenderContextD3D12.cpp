#include "RenderContextD3D12.h"

#include <dxgi1_2.h>
#include <dxgi1_3.h>
#include <dxgi1_5.h>

#include "ExceptionHandle.h"
#include "TypeConverterDX.h"
#include "Logger/DebugUtils.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
    RenderContextD3D12::RenderContextD3D12(DeviceD3D12& device, RenderContextSettings settings)
    :ContextCommonD3D12(device, settings)
{
    LOG_RHI_CONSTRUCTOR("RenderContextD3D12");
}

void RenderContextD3D12::Initialize()
{
    const RenderContextSettings settings = GetSettings();
    // create command queue and swapchain.
    DXGI_SWAP_CHAIN_DESC1 swapChainDesc{};
    swapChainDesc.Width = settings.frameSize.GetWidth();
    swapChainDesc.Height = settings.frameSize.GetHeight();
    swapChainDesc.Format = TextureFormatToDXGI_Format(settings.textureFormat);
    swapChainDesc.Stereo = false;
    swapChainDesc.BufferCount = settings.frameBuffersCount;
    swapChainDesc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    swapChainDesc.Scaling = DXGI_SCALING_NONE;
    swapChainDesc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD;
    swapChainDesc.AlphaMode = DXGI_ALPHA_MODE_IGNORE;
    swapChainDesc.SampleDesc.Count = 1;
    swapChainDesc.Flags = DXGI_SWAP_CHAIN_FLAG_FRAME_LATENCY_WAITABLE_OBJECT;

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
        swapChainDesc.Flags |= DXGI_FEATURE_PRESENT_ALLOW_TEARING;
        mIsTearingSupported = true;
    }
    else
    {
        mIsTearingSupported = false;
    }

    ComPtr<IDXGISwapChain1> swap_chain_cptr;
    // lazy create command queue first.
    ID3D12CommandQueue& command_queue = GetDefaultCommandQueueD3D12(CommandListType::Render).GetNativeCommandQueue();
    ThrowIfFailed(factory_cptr->CreateSwapChainForHwnd(command_queue,))
    
    
    ContextCommonD3D12<RenderContextBase>::Initialize();
}


ARISENRHI_D3D12_END_NAMESPACE
