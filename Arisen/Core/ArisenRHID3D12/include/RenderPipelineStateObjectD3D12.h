#pragma once
#include "ProgramD3D12.h"
#include "RenderPipelineStateObjectBase.h"
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"


ARISENRHI_D3D12_BEGIN_NAMEPSACE
class RenderPipelineStateObjectD3D12 final: public RenderPipelineStateObjectBase<RHID3D12ImplTraits>
{
public:
    RenderPipelineStateObjectD3D12(const IRHIContext& context, const RenderPipelineStateObjectSettings& settings);

    // IRenderPipelineStateObject
    virtual void Apply(const IRenderCommandList& command_list) const override final;
    void Reset(const RenderPipelineStateObjectSettings& settings) override final;


    ComPtr<ID3D12PipelineState>& GetNativePSO() const;
    void InitializeNativePSO() const;
    ProgramD3D12& GetProgramD3D12() const;
    const class RenderContextD3D12& GetRenderContextD3D12() const;
private:

    ComPtr<ID3D12PipelineState> m_pso_cptr;
    D3D12_GRAPHICS_PIPELINE_STATE_DESC m_pso_desc;
};
ARISENRHI_D3D12_END_NAMESPACE
