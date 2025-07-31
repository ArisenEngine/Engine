#pragma once
#include "CoreMinimalD3D12.h"
#include "IDevice.h"
#include "IRHIFactory.h"
#include "ISwapChain.h"
#include "RHIFactoryBase.h"
#include "RHITypesD3D12.h"

ARISENRHI_BEGIN_NAMEPSACE
class RHIFactoryD3D12 : public RHIFactoryBase
{
public:
    static RHIFactoryD3D12* GetInstance();
    virtual Ptr<IDevice> CreateDeviceD3D12(const EngineCreateInfoD3D12& InCreateInfo);
    virtual void CreateSwapChainD3D12(IDevice* pDevice, ISwapChain** pOutSwapChain);
};

RHIFactoryD3D12* CreateRHIFactoryD3D12();

ARISENRHI_END_NAMESPACE
