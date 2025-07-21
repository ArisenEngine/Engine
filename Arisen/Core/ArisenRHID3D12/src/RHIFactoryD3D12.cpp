#include "RHIFactoryD3D12.h"

#include <wrl/client.h>

#include "RHIFactoryBase.h"
#include "Logger/DebugUtils.h"

using Microsoft::WRL::ComPtr;

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
        :RHIFactoryBase()
    {
        
    }

    virtual void CreateDeviceD3D12(const EngineCreateInfoD3D12& InCreateInfo, IDevice** pOutDevice) override;
    virtual void CreateSwapChainD3D12(IDevice* pDevice, ISwapChain** pOutSwapChain) override;
};

void RHIFactoryD3D12Impl::CreateSwapChainD3D12(IDevice* pDevice, ISwapChain** pOutSwapChain)
{
}

void RHIFactoryD3D12Impl::CreateDeviceD3D12(const EngineCreateInfoD3D12& InCreateInfo, IDevice** pOutDevice)
{
    //TODO: Check Engine version.

   CHECK(pOutDevice, "pOutDevice is nullptr.");

    try
    {
        // enable debug layer.
        if (InCreateInfo.EnableValidation)
        {
            ComPtr<ID3D12Debug> debugController;
        }
    }
    catch (const std::runtime_error& e)
    {
        LOG_ERROR("failed to create DeviceD3D12.");
        return;
    }
}

IRHIFactoryD3D12* CreateRHIFactoryD3D12()
{
    return RHIFactoryD3D12Impl::GetInstance();
}
ARISENRHI_END_NAMESPACE
