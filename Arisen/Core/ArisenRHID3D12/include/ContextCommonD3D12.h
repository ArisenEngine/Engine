#pragma once
#include "CommandQueueD3D12.h"
#include "ContextBase.h"
#include "CoreMinimalD3D12.h"
#include "DeviceD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE

struct IContextD3D12
{
        
};

template<typename TContext> requires std::is_base_of_v<IRHIContext, TContext>
class ContextCommonD3D12 : public TContext
{
public:
    ContextCommonD3D12(IDevice& device, const typename TContext::Settings& settings)
        :TContext(device, settings){}

    virtual void Initialize()
    {
        
    }

    const DeviceD3D12& GetDeviceD3D12() const noexcept
    {
        return static_cast<const DeviceD3D12&>(TContext::GetDevice());
    }

    virtual Ptr<ICommandQueue> CreateCommandQueue(CommandListType type) const override
    {
        return MakePtr(CommandQueueD3D12, this, type);
    }
};



ARISENRHI_D3D12_END_NAMESPACE
