#pragma once
#include "CommandQueueD3D12.h"
#include "ContextBase.h"
#include "CoreMinimalD3D12.h"
#include "DeviceD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE

template<typename TContext, typename TSettings> //requires std::is_base_of_v<IRHIContext, TContext>
class ContextCommonD3D12 : public TContext
{
public:
    ContextCommonD3D12(IDevice& device, const TSettings& settings)
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
        return MakePtr(CommandQueueD3D12, *this, type);
    }

    CommandQueueD3D12& GetDefaultCommandQueueD3D12(CommandListType type)
    {
        return static_cast<CommandQueueD3D12&>(ContextCommonD3D12::GetDefaultCommandKit(type).GetQueue());
    }
    
};



ARISENRHI_D3D12_END_NAMESPACE
