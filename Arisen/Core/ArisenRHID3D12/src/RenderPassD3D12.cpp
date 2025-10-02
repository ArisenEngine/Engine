#include "RenderPassD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
RenderPassD3D12::RenderPassD3D12(const IRenderPattern& render_pattern, const RenderPassSettings& settings)
    :RenderPassBase(render_pattern, settings)
{
    // create actual render target binding to rtv.
    std::ranges::transform(settings.attachments, std::back_inserter(m_dx_attachments),
        [](const ResourceViewBase& tex_id) {
            return ResourceViewD3D12(tex_id, ResourceUsageMask({ResourceUsage::RenderTarget}));
        });
}
ARISENRHI_D3D12_END_NAMESPACE
