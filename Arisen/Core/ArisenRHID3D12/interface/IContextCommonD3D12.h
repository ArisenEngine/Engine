#pragma once
#include "DescriptorManagerD3D12.h"
#include "RHIMacrosD3D12.h"
#include "DeviceD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
struct IRHIContextCommonD3D12
{
    virtual DescriptorManagerD3D12& GetDescriptorManagerD3D12() const = 0;
    virtual const DeviceD3D12& GetDeviceD3D12() const noexcept = 0;
};
ARISENRHI_D3D12_END_NAMESPACE
