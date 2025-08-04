#pragma once
#include <d3d12.h>
#include <dxgi.h>
#include <wrl/client.h>
#include "RHIMacros.h"
#include "DeviceBase.h"
#include "RHIMacrosD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
using namespace Microsoft::WRL;
class DeviceD3D12 : public DeviceBase
{
public:
    DeviceD3D12(const ComPtr<IDXGIAdapter>& adapter_cptr, const ComPtr<ID3D12Device>& device_cptr);

    virtual Ptr<IRenderContext> CreateRenderContext(const RenderContextSettings& render_context_settings) override;

    
private:
    mutable ComPtr<ID3D12Device> m_device_cptr;
    const ComPtr<IDXGIAdapter> m_adapter_cptr;
};
ARISENRHI_D3D12_END_NAMESPACE
