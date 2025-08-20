#include "DescriptorManagerD3D12.h"
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
    for (const )
}

void DescriptorManagerD3D12::CompleteInitialize()
{
    DescriptorManagerBase<RHID3D12ImplTraits>::CompleteInitialize();
}


ARISENRHI_D3D12_END_NAMESPACE
