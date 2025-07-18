#pragma once
#include "CoreMinimalD3D12.h"
#include "IDevice.h"
#include "IDeviceContext.h"
#include "IRHIFactory.h"
#include "ISwapChain.h"

ARISENRHI_BEGIN_NAMEPSACE
    struct IRHIFactoryD3D12 : public IRHIFactory
{
    virtual void CreateDeviceAndContextD3D12(IDevice** pOutDevice, IDeviceContext** pOutDeviceContext) = 0;
    virtual void CreateSwapChainD3D12(IDevice* pDevice, IDeviceContext* pContext, ISwapChain** pOutSwapChain) = 0;
};

IRHIFactoryD3D12* CreateRHIFactoryD3D12();

ARISENRHI_END_NAMESPACE