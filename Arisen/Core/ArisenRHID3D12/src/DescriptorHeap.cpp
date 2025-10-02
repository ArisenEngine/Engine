#include "DescriptorHeap.h"

#include "DeviceD3D12.h"
#include "ExceptionHandle.h"
#include "DebugUtils/Checks.h"
#include "DebugUtils/Verifies.h"
#include "d3dx12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
    D3D12_DESCRIPTOR_HEAP_TYPE GetNativeDescriptorHeapType(DescriptorType type)
{
    switch (type)
    {
    case DescriptorType::ShaderResources:
        return D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV;

    case DescriptorType::Samplers:
        return D3D12_DESCRIPTOR_HEAP_TYPE_SAMPLER;

    case DescriptorType::RenderTargets:
        return D3D12_DESCRIPTOR_HEAP_TYPE_RTV;

    case DescriptorType::DepthStencil:
        return D3D12_DESCRIPTOR_HEAP_TYPE_DSV;

    default:
        CHECK_UNEXPECTED_RETURN(type, D3D12_DESCRIPTOR_HEAP_TYPE_NUM_TYPES);
    }
}

DescriptorHeap::DescriptorHeap(const IRHIContext& context, const DescriptorHeapSettings& settings)
    :m_context(const_cast<IRHIContext&>( context))
    ,m_settings(settings)
    ,m_deferred_size(settings.size)
    ,m_native_heap_type(GetNativeDescriptorHeapType(settings.type))
    ,m_native_heap_size(static_cast<const DeviceD3D12&>(context.GetDevice()).GetNativeDevice()->GetDescriptorHandleIncrementSize(m_native_heap_type))
{
    if (m_deferred_size > 0)
    {
        m_resources.reserve(m_deferred_size);
        m_free_ranges.Add({0, m_deferred_size});
    }

    if (settings.size > 0)
    {
        Allocate();
    }
}

bool DescriptorHeap::IsShaderVisibleHeapType(DescriptorType type)
{
    return type == DescriptorType::ShaderResources || type == DescriptorType::Samplers;
}

void DescriptorHeap::Allocate()
{
    const uint32_t allocated_size = GetAllocatedSize();
    const uint32_t deferred_size = GetDeferredSize();

    if (deferred_size == allocated_size)
    {
        return;
    }

    const ComPtr<ID3D12Device> device_cptr = static_cast<const DeviceD3D12&>(m_context.GetDevice()).GetNativeDevice();
    VERIFY_NOT_NULL(device_cptr,std::format("{}:device is not valid ",__func__));

    ComPtr<ID3D12DescriptorHeap> old_desciprotr_heap_ptr = m_native_heap_ptr;

    D3D12_DESCRIPTOR_HEAP_DESC heap_desc = {};
    heap_desc.NumDescriptors = deferred_size;
    heap_desc.Type = m_native_heap_type;
    heap_desc.Flags = m_settings.shader_visible ? D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE : D3D12_DESCRIPTOR_HEAP_FLAG_NONE;

    ThrowIfFailed(device_cptr->CreateDescriptorHeap(&heap_desc, IID_PPV_ARGS(&m_native_heap_ptr)), device_cptr.Get());

    if (!m_settings.shader_visible && old_desciprotr_heap_ptr && allocated_size > 0)
    {
        // copy descriptors from old heap to the new one, it works for cpu heaps only.
        // gpu heaps must be re-filled with updated descriptors using ProgramBinding::CompleteInitialization() and DescriptorManager::CompleteIninitialize()
        device_cptr->CopyDescriptorsSimple(allocated_size,
            m_native_heap_ptr->GetCPUDescriptorHandleForHeapStart(),
            old_desciprotr_heap_ptr->GetCPUDescriptorHandleForHeapStart(),
            m_native_heap_type);
    }

    m_allocated_size = m_deferred_size;

    // TODO: emit and notify render pass to update all descriptor heaps.

}

uint32_t DescriptorHeap::GetAllocatedSize() const
{
    return m_allocated_size;
}
uint32_t DescriptorHeap::GetDeferredSize() const
{
    return  m_deferred_size;
}

uint32_t DescriptorHeap::AddResource(const IResourceD3D12& resource)
{
    std::scoped_lock lock_guard(m_modification_mutext);

    if (m_resources.size() >= m_settings.size)
    {
        m_deferred_size++;
        Allocate();
    }

    m_resources.push_back(&resource);
    uint32_t resource_index = m_resources.size() - 1;
    m_free_ranges.Remove(Range(resource_index, resource_index + 1));
    return resource_index;
}

D3D12_CPU_DESCRIPTOR_HANDLE DescriptorHeap::GetNativeCpuDescriptorHandle(uint32_t descriptor_index) const
{
    return CD3DX12_CPU_DESCRIPTOR_HANDLE(m_native_heap_ptr->GetCPUDescriptorHandleForHeapStart(), descriptor_index, m_native_heap_size);
}

ARISENRHI_D3D12_END_NAMESPACE
