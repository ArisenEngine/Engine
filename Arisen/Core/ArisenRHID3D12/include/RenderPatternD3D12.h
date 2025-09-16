#pragma once
#include "CoreMinimalD3D12.h"
#include "RenderPatternBase.h"
#include "RHID3D12ImplTraits.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
class RenderPatternD3D12 final: public RenderPatternBase<RHID3D12ImplTraits>
{
public:
    RenderPatternD3D12(IRenderContext& Context, const RenderPatternSettings& Settings);
};
ARISENRHI_D3D12_END_NAMESPACE
