#include "RenderCommandListD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
void RenderCommandListD3D12::ResetWithPSO(const IRenderPipelineStateObject& pso) const
{
    CommandListD3D12::ResetWithPSO(pso);
}

void RenderCommandListD3D12::SetPSO(const IRenderPipelineStateObject& pso) const
{
    CommandListD3D12::SetPSO(pso);
}
ARISENRHI_D3D12_END_NAMESPACE
