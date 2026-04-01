#pragma once
#include "IObject.h"
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE
struct IFence : IObject
{
    virtual ICommandQueue& GetCommandQueue() = 0;

    virtual void Signal() = 0;
    virtual void WaitOnCpu() = 0;
    virtual void WaitOnGpu(ICommandQueue& command_queue) = 0;
    virtual void FlushOnCpu() = 0;
    virtual void FlushOnGpu(ICommandQueue& command_queue) = 0;
};
ARISENRHI_END_NAMESPACE
