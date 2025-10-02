#include "CommandListSetD3D12.h"

ArisenRHID3D12::CommandListSetD3D12::CommandListSetD3D12(std::vector<Ptr<ICommandList>> command_lists)
    : CommandListSetBase(command_lists)
{
}
