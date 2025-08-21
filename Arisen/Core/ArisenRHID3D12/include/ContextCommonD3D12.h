#pragma once
#include "CommandQueueD3D12.h"
#include "ContextBase.h"
#include "CoreMinimalD3D12.h"
#include "DescriptorManagerD3D12.h"
#include "DeviceD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE

struct IRHIContextCommonD3D12 : public IRHIContext
{
    
};

template<typename TContext, typename TSettings> //requires std::is_base_of_v<ContextBase<>, TContext>
class ContextCommonD3D12 : public TContext
{
public:
    ContextCommonD3D12(IDevice& device, const TSettings& settings)
        :TContext(device, settings, MakeUniquePtr(DescriptorManagerD3D12, *this))
    {}

    virtual void Initialize()
    {
        TContext::Initialize();
        // create descriptor.
        GetDescriptorManagerD3D12().Initialize();

        //  TODO:emit context inited.
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

    DescriptorManagerD3D12& GetDescriptorManagerD3D12()
    {
        return static_cast<DescriptorManagerD3D12&>(ContextCommonD3D12::GetDescriptorManager());
    }
};



ARISENRHI_D3D12_END_NAMESPACE
