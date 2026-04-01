#pragma once
#include "CommandQueueD3D12.h"
#include "FenceBase.h"
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
class FenceD3D12 final : public FenceBase<RHID3D12ImplTraits>
{
public:
    FenceD3D12(CommandQueueD3D12& command_queue);

    CommandQueueD3D12& GetCommandQueueD3D12();

    // IFence
    void Signal() override final;
    void WaitOnCpu() override final;
    void WaitOnGpu(ICommandQueue& command_queue) override final;
    void FlushOnCpu() override final;
    void FlushOnGpu(ICommandQueue& command_queue) override final;
private:
    HANDLE m_event = nullptr;
    ComPtr<ID3D12Fence> m_fence_cptr;
};
ARISENRHI_D3D12_END_NAMESPACE
