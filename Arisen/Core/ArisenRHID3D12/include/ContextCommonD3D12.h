#pragma once
#include "CommandQueueD3D12.h"
#include "ContextBase.h"
#include "CoreMinimalD3D12.h"
#include "DescriptorManagerD3D12.h"
#include "DeviceD3D12.h"
#include "IContextCommonD3D12.h"
#include "ProgramD3D12.h"
#include "ShaderD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
template<typename TContext, typename TSettings> //requires std::is_base_of_v<ContextBase<>, TContext>
class ContextCommonD3D12 : public TContext, public IRHIContextCommonD3D12
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


    // IRHIContext
    virtual Ptr<ICommandQueue> CreateCommandQueue(CommandListType type) const override
    {
        return MakePtr(CommandQueueD3D12, *this, type);
    }

    [[nodiscard]] virtual Ptr<IProgram> CreateProgram(const ProgramSettings& settings) const
    {
        return MakePtr(ProgramD3D12, *this, settings);
    }

    CommandQueueD3D12& GetDefaultCommandQueueD3D12(CommandListType type)
    {
        return static_cast<CommandQueueD3D12&>(ContextCommonD3D12::GetDefaultCommandKit(type).GetQueue());
    }

    [[nodiscard]] virtual Ptr<IShader> CreateShader(ShaderType type, const ShaderSettings& settings) const override final
    {
        return MakePtr(ShaderD3D12,type, *this,  settings);
    }

    // IContextCommonD3D12
    virtual DescriptorManagerD3D12& GetDescriptorManagerD3D12() const override
    {
        return static_cast<DescriptorManagerD3D12&>(const_cast<ContextCommonD3D12*>(this)->GetDescriptorManager());
    }

    const DeviceD3D12& GetDeviceD3D12() const noexcept override
    {
        return static_cast<const DeviceD3D12&>(TContext::GetDevice());
    }
};



ARISENRHI_D3D12_END_NAMESPACE
