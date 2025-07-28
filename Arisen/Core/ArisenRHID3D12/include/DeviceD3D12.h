#pragma once
#include <d3d12.h>
#include <dxgi.h>
#include <wrl/client.h>

#include "DeviceBase.h"
using namespace Microsoft::WRL;
class DeviceD3D12 : public DeviceBase
{
public:
    DeviceD3D12(const ComPtr<IDXGIAdapter>& adapter_cptr, const ComPtr<ID3D12Device>& device_cptr);
private:
    mutable ComPtr<ID3D12Device> m_device_cptr;
    const ComPtr<IDXGIAdapter> m_adapter_cptr;
};
ARISENRHI_END_NAMESPACE
