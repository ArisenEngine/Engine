#pragma once
#include "IFence.h"
#include "ObjectBase.h"
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE
template<typename RHIImplTraits> requires std::is_base_of_v<IFence, typename RHIImplTraits::FenceInterface>
class FenceBase : public ObjectBase<typename RHIImplTraits::FenceInterface>
{
public:
    FenceBase(ICommandQueue& command_queue ) : m_command_queue(command_queue)
    {
    }

    // IFence
    ICommandQueue& GetCommandQueue() override final { return m_command_queue; }
    virtual void Signal() override
    {
        LOG_INFO(std::format("Fence {} Signal from gpu with value {}", FenceBase::GetName(), m_value + 1));
        ++m_value;
    }
    virtual void WaitOnCpu()
    {
        LOG_INFO(std::format("Fence {} wait on cpu with value {}", FenceBase::GetName(), m_value));

    }
    virtual void WaitOnGpu(ICommandQueue& command_queue)
    {
        LOG_INFO(std::format("Fence {} wait on gpu with value {}", FenceBase::GetName(), m_value));

    }
    virtual void FlushOnCpu()
    {
        Signal();
        WaitOnCpu();
    }
    virtual void FlushOnGpu(ICommandQueue& command_queue)
    {
        Signal();
        WaitOnGpu(command_queue);
    }

protected:
    uint64_t GetValue() const {return m_value;}

private:
    ICommandQueue& m_command_queue;
    uint64_t m_value = 0u;
};
ARISENRHI_END_NAMESPACE
