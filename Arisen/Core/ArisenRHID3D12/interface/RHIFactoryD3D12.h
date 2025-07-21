#pragma once
#include "CoreMinimalD3D12.h"
#include "IDevice.h"
#include "IRHIFactory.h"
#include "ISwapChain.h"
#include "RHITypesD3D12.h"

ARISENRHI_BEGIN_NAMEPSACE
    struct IRHIFactoryD3D12 : public IRHIFactory
{
    virtual void CreateDeviceD3D12(const EngineCreateInfoD3D12& InCreateInfo, IDevice** pOutDevice) = 0;
    virtual void CreateSwapChainD3D12(IDevice* pDevice, ISwapChain** pOutSwapChain) = 0;
};

IRHIFactoryD3D12* CreateRHIFactoryD3D12();

ARISENRHI_END_NAMESPACE
