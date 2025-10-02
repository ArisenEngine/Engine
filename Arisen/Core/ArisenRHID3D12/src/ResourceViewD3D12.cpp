#include "ResourceViewD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
ResourceViewD3D12::ResourceViewD3D12(const ResourceViewBase& view_id, ResourceUsageMask usage)
    :ResourceViewBase(view_id),m_id(usage, GetSettings())
    ,m_resource_d3d12(DYNAMIC_CAST(IResourceD3D12&, GetResource()))
    ,m_descriptor_opt(m_resource_d3d12.InitializeNativeViewDescriptor(m_id))
{
}
ARISENRHI_D3D12_END_NAMESPACE
