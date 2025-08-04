#pragma once
#include "ContextBase.h"
#include "CoreMinimalD3D12.h"
#include "DeviceD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
    template<typename TContext> requires std::is_base_of_v<ArisenRHI::IRHIContext, TContext>
class ContextDxCommon : public TContext
{
public:
    ContextDxCommon(IDevice& device, const TContext::Settings& settings)
        :TContext(device, settings){}

    virtual void Initialize()
    {
        
    }

    const DeviceD3D12& GetDeviceD3D12() const noexcept
    {
        return static_cast<const DeviceD3D12&>(TContext::GetDevice());
    }
};



ARISENRHI_D3D12_END_NAMESPACE
