#pragma once
#include "CommandListSetBase.h"
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
class CommandListSetD3D12 final : public CommandListSetBase<RHID3D12ImplTraits>
{
public:
    CommandListSetD3D12(std::vector<Ptr<ICommandList>> command_lists);
};
ARISENRHI_D3D12_END_NAMESPACE
