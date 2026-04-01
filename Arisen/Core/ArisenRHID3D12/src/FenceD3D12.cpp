#include "FenceD3D12.h"

#include "ExceptionHandle.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
FenceD3D12::FenceD3D12(CommandQueueD3D12& command_queue) : FenceBase(command_queue)
                                                           , m_event(CreateEvent(nullptr, FALSE, FALSE, nullptr))
{
    if (!m_event) {
        ThrowIfFailed(HRESULT_FROM_WIN32(GetLastError()));
    }

    ComPtr<ID3D12Device> device = GetCommandQueueD3D12().GetContextD3d12().GetDeviceD3D12().GetNativeDevice();
    VERIFY(device);

    ThrowIfFailed(device->CreateFence(GetValue(), D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&m_fence_cptr)));
}

CommandQueueD3D12& FenceD3D12::GetCommandQueueD3D12()
{
    return static_cast<CommandQueueD3D12&>(FenceD3D12::GetCommandQueue());
}

void FenceD3D12::Signal()
{
    FenceBase::Signal();

    CommandQueueD3D12& command_queue = GetCommandQueueD3D12();
    ThrowIfFailed(command_queue.GetNativeCommandQueue().Signal(m_fence_cptr.Get(), GetValue()));
}

void FenceD3D12::WaitOnCpu()
{
    FenceBase<RHID3D12ImplTraits>::WaitOnCpu();

    const uint64_t wait_value = GetValue();
    const uint64_t curr_value = m_fence_cptr->GetCompletedValue();
    if (curr_value >= wait_value)
    {
        return;
    }

    ThrowIfFailed(m_fence_cptr->SetEventOnCompletion(GetValue(), m_event));
    WaitForSingleObjectEx(m_event, INFINITE, FALSE);

    LOG_RHI_INFO(std::format("Fence {} AWAKE on value {}"),GetName(), wait_value);
}

void FenceD3D12::WaitOnGpu(ICommandQueue& command_queue)
{
    FenceBase<RHID3D12ImplTraits>::WaitOnGpu(command_queue);
}

void FenceD3D12::FlushOnCpu()
{
    FenceBase<RHID3D12ImplTraits>::FlushOnCpu();
}

void FenceD3D12::FlushOnGpu(ICommandQueue& command_queue)
{
    FenceBase<RHID3D12ImplTraits>::FlushOnGpu(command_queue);
}

ARISENRHI_D3D12_END_NAMESPACE
