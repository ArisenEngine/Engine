#include "DeviceD3D12.h"

#include <codecvt>

ARISENRHI_BEGIN_NAMEPSACE
    static std::string GetAdapterNameDxgi(IDXGIAdapter& adapter)
{
    DXGI_ADAPTER_DESC desc{};
    adapter.GetDesc(&desc);
    std::wstring_convert<std::codecvt_utf8<wchar_t>> converter;
    return converter.to_bytes(desc.Description);
}

DeviceD3D12::DeviceD3D12(const ComPtr<IDXGIAdapter>& adapter_cptr, const ComPtr<ID3D12Device>& device_cptr)
    :DeviceBase(GetAdapterNameDxgi(*adapter_cptr.Get()))
    ,m_device_cptr(device_cptr)
{
    
}

ARISENRHI_END_NAMESPACE
