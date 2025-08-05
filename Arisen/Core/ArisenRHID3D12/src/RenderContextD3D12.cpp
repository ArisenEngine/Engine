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

    ComPtr<IDXGIFactory5> factoryCPtr;
    ThrowIfFailed(CreateDXGIFactory2(dxgiFactoryFlags, IID_PPV_ARGS(&factoryCPtr)));
    CHECK(factoryCPtr,"factory is null, create swapchain failed!");

    // TODO:
    ID3D12Device* DeviceD3D12 = nullptr;
    BOOL presentTearingSupport = FALSE;
    ThrowIfFailed(factoryCPtr->CheckFeatureSupport(DXGI_FEATURE_PRESENT_ALLOW_TEARING, &presentTearingSupport, sizeof(presentTearingSupport)), DeviceD3D12);
    if (presentTearingSupport)
    {
        swapChainDesc.Flags |= DXGI_FEATURE_PRESENT_ALLOW_TEARING;
        mIsTearingSupported = true;
    }
    else
    {
        mIsTearingSupported = false;
    }

    ComPtr<IDXGISwapChain1> swapChainCPtr;
    // lazy create command queue first.
    
    
    
    ContextCommonD3D12<RenderContextBase>::Initialize();
}

ARISENRHI_D3D12_END_NAMESPACE
