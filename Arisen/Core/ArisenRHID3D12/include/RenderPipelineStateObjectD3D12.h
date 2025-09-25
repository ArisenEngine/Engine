#pragma once
#include "RenderPipelineStateObjectBase.h"
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
class RenderPipelineStateObjectD3D12 final: public RenderPipelineStateObjectBase<RHID3D12ImplTraits>
{
public:
    RenderPipelineStateObjectD3D12(const IRHIContext& context, const RenderPipelineStateObjectSettings& settings);
    
};
ARISENRHI_D3D12_END_NAMESPACE