#pragma once
#include "IDescriptorManagerD3D12.h"
#include "IResourceD3D12.h"
#include "ResourceD3D12.h"
#include "ResourceViewBase.h"
#include "RHIMacrosD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
class ResourceViewD3D12 final: public ResourceViewBase
{
public:
    ResourceViewD3D12(const ResourceViewBase& view_id, ResourceUsageMask usage);

private:
    ResourceViewId m_id;
    IResourceD3D12& m_resource_d3d12;
    Opt<ResourceDescriptor> m_descriptor_opt;
};
ARISENRHI_D3D12_END_NAMESPACE
