#pragma once
#include <d3d12.h>
#include <wrl/client.h>

#include "DeviceBase.h"
#include "RHIMacros.h"
#include "RHIMacrosD3D12.h"

ARISENRHI_BEGIN_NAMEPSACE

using namespace Microsoft::WRL;

class DeviceD3D12 : public DeviceBase
{
public:
    DeviceD3D12(const std::string& adapter_name, const ComPtr<ID3D12Device>& device_cptr);
private:
    const ComPtr<ID3D12Device> m_device_cptr;
};
ARISENRHI_END_NAMESPACE
