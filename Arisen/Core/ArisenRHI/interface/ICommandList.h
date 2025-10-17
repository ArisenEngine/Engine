#pragma once
#include "IObject.h"
#include "IRenderPipelineStateObject.h"


ARISENRHI_BEGIN_NAMEPSACE
enum class CommandListType
{
    Transfer,
    Render,
    ParallelRender,
    Compute,
    __COUNT
};

struct ICommandList : IObject
{
    virtual struct ICommandQueue& GetCommandQueue() const = 0;
};

struct IRenderCommandList : ICommandList
{
    virtual void ResetWithPSO(const IRenderPipelineStateObject& pso) const = 0;
    virtual void SetPSO(const IRenderPipelineStateObject& pso) const = 0;
};
ARISENRHI_END_NAMESPACE
