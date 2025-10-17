#pragma once
#include "CommandListD3D12.h"
#include "IRenderCommandListD3D12.h"
#include "RenderCommandListBase.h"
#include "RHIMacrosD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
class RenderCommandListD3D12 final: public CommandListD3D12<RenderCommandListBase<RHID3D12ImplTraits>>
{
public:
    RenderCommandListD3D12(const ICommandQueue& command_queue, IRenderPass& render_pass)
        : CommandListD3D12(command_queue, render_pass, D3D12_COMMAND_LIST_TYPE_DIRECT)
    {}

    // IRenderCommandList
    virtual void ResetWithPSO(const IRenderPipelineStateObject& pso) const override final;

    virtual void SetPSO(const IRenderPipelineStateObject& pso) const override final;
};
ARISENRHI_D3D12_END_NAMESPACE
