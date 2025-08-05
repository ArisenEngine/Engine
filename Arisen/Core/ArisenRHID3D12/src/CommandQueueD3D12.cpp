#include "CommandQueueD3D12.h"

#include "RenderContextD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE

static ComPtr<ID3D12CommandQueue> CreateNativeCommandQueue(const IRHIContext& context, CommandListType type)
{
    const DeviceD3D12& device = static_cast<const DeviceD3D12&>(context.GetDevice());
    const ComPtr<ID3D12Device>& device_cptr = device.GetNativeDevice();
    CHECK(device_cptr,"device is null,create command queue failed!");

    /create commandqueue/.
}

template<typename T>
CommandQueueD3D12::CommandQueueD3D12(const ContextCommonD3D12<T>& context, CommandListType type)
    :CommandQueueCommon(context, type)
,m_command_queue_cptr(CreateNativeCommandQueue(context, type))
{
    LOG_RHI_CONSTRUCTOR("CommandQueueD3D12");
}

CommandQueueD3D12::~CommandQueueD3D12()
{
    CommandQueueCommon::~CommandQueueCommon();
}

ARISENRHI_D3D12_END_NAMESPACE
