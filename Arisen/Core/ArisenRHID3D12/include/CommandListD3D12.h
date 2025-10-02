#pragma once
#include "CommandListBase.h"
#include "CommandQueueD3D12.h"
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
template<typename BaseCommandClass>
class CommandListD3D12 : public BaseCommandClass
{
public:
    CommandListD3D12(const ICommandQueue& command_queue, IRenderPass& render_pass, D3D12_COMMAND_LIST_TYPE native_command_list_type)
        : BaseCommandClass(command_queue, render_pass)
    {

    }
private:

};
ARISENRHI_D3D12_END_NAMESPACE
