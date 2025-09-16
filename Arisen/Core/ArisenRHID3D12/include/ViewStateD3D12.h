#pragma once
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"
#include "ViewStateBase.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
class ViewStateD3D12 final: public ViewStateBase<RHID3D12ImplTraits>
{
public:
    ViewStateD3D12(const ViewSettings& view_settings);
};
ARISENRHI_D3D12_END_NAMESPACE