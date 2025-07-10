#include "RHIFactoryD3D12.h"
#include "RHIFactoryBase.h"

ARISENRHI_BEGIN_NAMEPSACE
class RHIFactoryD3D12Impl : public RHIFactoryBase<IRHIFactoryD3D12>
{
public:
    static RHIFactoryD3D12Impl* GetInstance()
    {
        static RHIFactoryD3D12Impl FactoryD3D12Impl;
        return &FactoryD3D12Impl;
    }
    
    RHIFactoryD3D12Impl()
        :RHIFactoryD3D12Impl()
    {
        
    }

    virtual void CreateDeviceAndContextD3D12() override;
};

void RHIFactoryD3D12Impl::CreateDeviceAndContextD3D12()
{
    
}

IRHIFactoryD3D12* CreateRHIFactoryD3D12()
{
    return RHIFactoryD3D12Impl::GetInstance();
}
ARISENRHI_END_NAMESPACE