#pragma once
#include "CoreMinimalD3D12.h"
#include "IRHIFactory.h"

ARISENRHI_BEGIN_NAMEPSACE

struct IRHIFactoryD3D12 : public IRHIFactory
{
    virtual void CreateDeviceAndContextD3D12() = 0;
    
};

IRHIFactoryD3D12* CreateRHIFactoryD3D12();

ARISENRHI_END_NAMESPACE