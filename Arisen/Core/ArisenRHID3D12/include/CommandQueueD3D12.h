#pragma once
#include <wrl/client.h>
#include "CommandQueueCommon.h"
#include "RHIMacrosD3D12.h"
#include <d3d12.h>

ARISENRHI_D3D12_BEGIN_NAMEPSACE
using namespace Microsoft::WRL;
class CommandQueueD3D12 final : public CommandQueueCommon
{
public:
    CommandQueueD3D12(const IRHIContext& context, CommandListType type);
    ~CommandQueueD3D12() override;
private:
    ComPtr<ID3D12CommandQueue> m_command_queue_cptr;
};
ARISENRHI_D3D12_END_NAMESPACE