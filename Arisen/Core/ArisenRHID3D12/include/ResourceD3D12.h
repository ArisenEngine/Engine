#pragma once
#include <wrl/client.h>

#include "CommandQueueD3D12.h"
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
using namespace Microsoft::WRL;
template<typename BaseResourceType, typename ResourceSettingType>
class ResourceD3D12 : public BaseResourceType
{
public:
    ResourceD3D12(const IRHIContext& context, const ResourceSettingType& settings)
        :BaseResourceType(context, settings)
    {}

    void SetNativeResource(const ComPtr<ID3D12Resource> resource_cptr){m_resource_cptr = resource_cptr;}
private:
    ComPtr<ID3D12Resource> m_resource_cptr;
};
ARISENRHI_D3D12_END_NAMESPACE