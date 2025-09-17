#pragma once
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
template<typename BaseResourceType, typename ResourceSettingType>
class ResourceD3D12 : public BaseResourceType
{
public:
    ResourceD3D12(const IRHIContext& context, const ResourceSettingType& settings)
        :BaseResourceType(context, settings)
    {}
};
ARISENRHI_D3D12_END_NAMESPACE