#pragma once
#include "RenderPassBase.h"
#include "ResourceViewD3D12.h"
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
class RenderPassD3D12 final : public RenderPassBase<RHID3D12ImplTraits>
{
public:
    RenderPassD3D12(const IRenderPattern& render_pattern, const RenderPassSettings& settings);

private:
    std::vector<ResourceViewD3D12> m_dx_attachments;
};
ARISENRHI_D3D12_END_NAMESPACE
