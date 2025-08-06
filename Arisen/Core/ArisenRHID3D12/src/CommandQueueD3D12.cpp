#include "CommandQueueD3D12.h"

#include "ExceptionHandle.h"
#include "RenderContextD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE

// todo: maybe bundle is needed.
static D3D12_COMMAND_LIST_TYPE GetNativeCommandListType(CommandListType type, ContextOption options)
{
    switch (type)
    {
    case CommandListType::Transfer:
        return HasFlag(options, ContextOption::TransferWithD3D12DirectQueue)
        ?D3D12_COMMAND_LIST_TYPE_DIRECT:D3D12_COMMAND_LIST_TYPE_COPY;
        
    case CommandListType::Render:
    case CommandListType::ParallelRender:
        return D3D12_COMMAND_LIST_TYPE_DIRECT;
        
    case CommandListType::Compute:
        return D3D12_COMMAND_LIST_TYPE_COMPUTE;
        
    default:
        UNEXPECTED_RETURN(type,"Unknown command list type");
    }
}

static ComPtr<ID3D12CommandQueue> CreateNativeCommandQueue(const IRHIContext& context, CommandListType type)
{
    const DeviceD3D12& device = static_cast<const DeviceD3D12&>(context.GetDevice());
    const ComPtr<ID3D12Device>& device_cptr = device.GetNativeDevice();
    CHECK(device_cptr,"device is null,create command queue failed!");

    D3D12_COMMAND_QUEUE_DESC command_queue_desc = {};
    command_queue_desc.Flags = D3D12_COMMAND_QUEUE_FLAG_NONE;
    command_queue_desc.Type = GetNativeCommandListType(type, context.GetOptions());
    
    ComPtr<ID3D12CommandQueue> command_queue_cptr;
    ThrowIfFailed(device_cptr->CreateCommandQueue(&command_queue_desc, IID_PPV_ARGS(&command_queue_cptr)), device_cptr.Get());
    return command_queue_cptr;
}

template<typename T>
CommandQueueD3D12::CommandQueueD3D12(const ContextCommonD3D12<T>& context, CommandListType type)
    :CommandQueueCommon(context, type)
,m_command_queue_cptr(CreateNativeCommandQueue(context, type))
{
    LOG_RHI_CONSTRUCTOR("CommandQueueD3D12");
    // tracy context.
}

CommandQueueD3D12::~CommandQueueD3D12()
{
    CommandQueueCommon::~CommandQueueCommon();
}

ID3D12CommandQueue& CommandQueueD3D12::GetNativeCommandQueue()
{
    CHECK(m_command_queue_cptr, "command queue is null");
    return *m_command_queue_cptr.Get();
}

ARISENRHI_D3D12_END_NAMESPACE
