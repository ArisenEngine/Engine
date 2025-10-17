#pragma once
#include "CommandListBase.h"
#include "CommandQueueD3D12.h"
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"
#include "ExceptionHandle.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
template<typename BaseCommandClass>
class CommandListD3D12 : public BaseCommandClass
{
public:
    CommandListD3D12(const ICommandQueue& command_queue, IRenderPass& render_pass, D3D12_COMMAND_LIST_TYPE native_command_list_type)
        : BaseCommandClass(command_queue, render_pass)
    {
        ComPtr<ID3D12Device>& device = GetCommandQueueD3D12().GetContextD3d12().GetDeviceD3D12().GetNativeDevice();
        VERIFY_NOT_NULL(device,"device is null!");

        ThrowIfFailed(device->CreateCommandAllocator(native_command_list_type, IID_PPV_ARGS(&m_command_allocator_cptr)));
        m_command_list_cptr = nullptr;
        HRESULT hr = device->CreateCommandList(0, native_command_list_type, m_command_allocator_cptr.Get(), nullptr, IID_PPV_ARGS(&m_command_list_cptr));
        ThrowIfFailed(hr);
        m_command_list_cptr.As(&m_command_list4_cptr);
    }

    CommandQueueD3D12& GetCommandQueueD3D12() const
    {
        return static_cast<CommandQueueD3D12&>(const_cast<CommandListD3D12*>(this)->BaseCommandClass::GetCommandQueue());
    }

    ID3D12GraphicsCommandList& GetNativeCommandList() const
    {
        return *m_command_list_cptr.Get();
    }
private:
    ComPtr<ID3D12CommandAllocator> m_command_allocator_cptr;
    ComPtr<ID3D12GraphicsCommandList> m_command_list_cptr;
    ComPtr<ID3D12GraphicsCommandList4> m_command_list4_cptr;

};
ARISENRHI_D3D12_END_NAMESPACE
