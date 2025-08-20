#pragma once
#include "CoreMinimalD3D12.h"
#include "DescriptorHeap.h"
#include "DescriptorManagerBase.h"
#include "RHID3D12ImplTraits.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
// Note: RTV/DSV Heaps are cpu visible heaps and SRV/SAMPLER Heaps are shader visible heaps.
// We just need to copy the old descriptor heaps when add new descriptor heap for new resource,
// but it's not suitable in the situation when we add a shader visible descriptor heap,instead
// we should rebuild all the descriptor heaps and descriptor table bindings using IDescriptorManager::CompleteInitialize()
class DescriptorManagerD3D12 : public DescriptorManagerBase<RHID3D12ImplTraits>
{
public:
    explicit DescriptorManagerD3D12(IRHIContext& context);

    virtual ~DescriptorManagerD3D12();

    void Initialize();

    virtual void CompleteInitialize() override;

private:

    // pool
    std::array<UniquePtrs<DescriptorHeap>, static_cast<size_t>(DescriptorType::Undefined)> m_descriptor_heap_types;
};
ARISENRHI_D3D12_END_NAMESPACE
