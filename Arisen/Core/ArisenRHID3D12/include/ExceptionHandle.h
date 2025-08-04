#pragma once
#include "CoreMinimalD3D12.h"
#include "DeviceD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
    class ExceptionHandle
{
public:
    
};

inline void ThrowIfFailed(HRESULT hr, ID3D12Device* devicePtr = nullptr)
{
    if (FAILED(hr))
    {
        throw std::runtime_error("hr is null, exception!");
    }
}

ARISENRHI_D3D12_END_NAMESPACE