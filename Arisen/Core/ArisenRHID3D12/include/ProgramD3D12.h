#pragma once
#include "ProgramBase.h"
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
class ProgramD3D12 final : public ProgramBase<RHID3D12ImplTraits>
{
public:
    ProgramD3D12(const IRHIContext& context, const ProgramSettings& settings);
};
ARISENRHI_D3D12_END_NAMESPACE