#include "RenderPassD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
RenderPassD3D12::RenderPassD3D12(const IRenderPattern& render_pattern, const RenderPassSettings& settings)
    :RenderPassBase(render_pattern, settings)
{
}
ARISENRHI_D3D12_END_NAMESPACE