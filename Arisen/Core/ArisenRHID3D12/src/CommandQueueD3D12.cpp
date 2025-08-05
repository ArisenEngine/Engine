#include "CommandQueueD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE



static ComPtr<ID3D12CommandQueue> CreateNativeCommandQueue(const IRHIContext& context, CommandListType type)
{
    //auto a = context.GetDevice().GetInterface<IDevice>();
    return nullptr;
}

CommandQueueD3D12::CommandQueueD3D12(const IRHIContext& context, CommandListType type)
    :CommandQueueCommon(context, type)
,m_command_queue_cptr(CreateNativeCommandQueue(context, type))
{
}

CommandQueueD3D12::~CommandQueueD3D12()
{
    CommandQueueCommon::~CommandQueueCommon();
}

ARISENRHI_D3D12_END_NAMESPACE
