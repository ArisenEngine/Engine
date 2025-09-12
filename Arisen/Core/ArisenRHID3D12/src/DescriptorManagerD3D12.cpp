#include "DescriptorManagerD3D12.h"
#include<magic_enum.hpp>

ARISENRHI_D3D12_BEGIN_NAMEPSACE

DescriptorManagerD3D12::DescriptorManagerD3D12(IRHIContext& context)
    :DescriptorManagerBase(context)
{
    LOG_RHI_CONSTRUCTOR("DescriptorManagerD3D12")
}

DescriptorManagerD3D12::~DescriptorManagerD3D12()
{
    LOG_RHI_DESTRUCTOR("DescriptorManagerD3D12")
}

void DescriptorManagerD3D12::Initialize()
{
    // Create descriptor heaps./
    auto AddDescriptorHeap = [this](UniquePtrs<DescriptorHeap>& target_heaps, DescriptorType heap_type, bool is_shader_visible)
    {
        DescriptorHeapSettings settings{heap_type, 0, is_shader_visible};
        target_heaps.push_back(MakeUniquePtr(DescriptorHeap, GetContext(), settings));
    };
    
    for (const DescriptorType heap_type : magic_enum::enum_values<DescriptorType>())
    {
        if (heap_type == DescriptorType::Undefined)
        {
            continue;
        }

        UniquePtrs<DescriptorHeap>& heaps = m_descriptor_heap_types[magic_enum::enum_integer(heap_type)];
        heaps.clear();

        AddDescriptorHeap(heaps, heap_type, false);
        if (DescriptorHeap::IsShaderVisibleHeapType(heap_type))
        {
            AddDescriptorHeap(heaps, heap_type, true);
        }
    }
}

void DescriptorManagerD3D12::CompleteInitialize()
{
    DescriptorManagerBase<RHID3D12ImplTraits>::CompleteInitialize();
    // TODO: complete initialize is note implemented.
}


ARISENRHI_D3D12_END_NAMESPACE
