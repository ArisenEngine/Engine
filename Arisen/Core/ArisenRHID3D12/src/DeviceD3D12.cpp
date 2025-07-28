#include "DeviceD3D12.h"

ArisenRHI::DeviceD3D12::DeviceD3D12(const std::string& adapter_name, const ComPtr<ID3D12Device>& device_cptr)
    :DeviceBase(adapter_name)
    ,m_device_cptr(device_cptr)
{
    
}
