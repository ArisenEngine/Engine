#pragma once
#include "CoreMinimalD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE

class DesciptorManagerD3D12;

struct RHID3D12ImplTraits
{
    using DescriptorInterface = IDescriptorManager;
    using DescriptorManagerImplType = DesciptorManagerD3D12;

};
ARISENRHI_D3D12_END_NAMESPACE
