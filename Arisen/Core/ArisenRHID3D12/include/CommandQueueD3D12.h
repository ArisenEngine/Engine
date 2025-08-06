#pragma once
#include <wrl/client.h>
#include "CommandQueueCommon.h"
#include "RHIMacrosD3D12.h"
#include <d3d12.h>

ARISENRHI_D3D12_BEGIN_NAMEPSACE

template<typename T, typename Settings>
class ContextCommonD3D12;
using namespace Microsoft::WRL;

class CommandQueueD3D12 final : public CommandQueueCommon<ICommandQueue>
{
public:
    template<typename T,typename Settings>
    CommandQueueD3D12(const ContextCommonD3D12<T,Settings>& context, CommandListType type)
        :CommandQueueCommon(context, type)
        ,m_command_queue_cptr(CreateNativeCommandQueue(context, type))
    {
        LOG_RHI_CONSTRUCTOR("CommandQueueD3D12");
        // tracy context.
    }
    
    ~CommandQueueD3D12() override;

    ID3D12CommandQueue& GetNativeCommandQueue();

private:
    static ComPtr<ID3D12CommandQueue> CreateNativeCommandQueue(const IRHIContext& context, CommandListType type);
    
private:
    ComPtr<ID3D12CommandQueue> m_command_queue_cptr;
};
ARISENRHI_D3D12_END_NAMESPACE