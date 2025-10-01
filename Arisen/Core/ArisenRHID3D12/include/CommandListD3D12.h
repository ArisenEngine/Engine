#pragma once
#include "CommandListBase.h"
#include "RHIMacrosD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
template<typename CommandListD3D12Interface>
    requires std::is_base_of_v<ICommandList, CommandListD3D12Interface>
class CommandListD3D12 : public CommandListBase<CommandListD3D12Interface>
{
public:

};
ARISENRHI_D3D12_END_NAMESPACE
