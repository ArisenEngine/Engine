#pragma once
#include <wrl/client.h>
#include <map>

#include "CommandQueueD3D12.h"
#include "DescriptorHeap.h"
#include "DescriptorManagerD3D12.h"
#include "IContextCommonD3D12.h"
#include "RHIMacrosD3D12.h"
#include "DebugUtils/Checks.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
using namespace Microsoft::WRL;
template<typename BaseResourceType, typename ResourceSettingType>
class ResourceD3D12 : public BaseResourceType, public IResourceD3D12
{
public:
    ResourceD3D12(const IRHIContext& context, const ResourceSettingType& settings)
        :BaseResourceType(context, settings)
        , m_context_dx(DYNAMIC_CAST(const IRHIContextCommonD3D12&, context))
    {}

    void SetNativeResource(const ComPtr<ID3D12Resource> resource_cptr){m_resource_cptr = resource_cptr;}

    const ResourceDescriptor& GetDescriptorByViewId(const ResourceViewId& view_id)
    {
        if (const auto it = m_descriptor_by_view_id.find(view_id); it != m_descriptor_by_view_id.end())
        {
            return it->second;
        }

        return m_descriptor_by_view_id.try_emplace(view_id, CreateResourceDescriptor(view_id.usage)).first->second;
    }

    // IResourceD3D12
    ID3D12Resource* GetNativeResource() const
    {
        return m_resource_cptr.Get();
    }

    static D3D12_CPU_DESCRIPTOR_HANDLE GetNativeCpuDescriptorHandle(const ResourceDescriptor& descriptor)
    {
        return descriptor.heap.GetNativeCpuDescriptorHandle(descriptor.index);
    }
private:

    DescriptorType GetDescriptorTypeByUsage(ResourceUsageMask usage) const
    {
        ResourceType resource_type = ResourceD3D12::GetResourceType();
        if (usage.HasAnyBits({ResourceUsage::ShaderRead, ResourceUsage::ShaderRead}))
        {
            return (resource_type == ResourceType::Sampler)
               ? DescriptorType::Samplers
               : DescriptorType::ShaderResources;
        }
        else if (usage.HasAnyBit(ResourceUsage::RenderTarget))
        {

            return resource_type == ResourceType::Texture &&
                    static_cast<const BaseResourceType*>(this)->GetSettings().type == TextureType::DepthStencil
                   ? DescriptorType::DepthStencil
                   : DescriptorType::RenderTargets;
        }
        else
        {
            CHECK_UNEXPECTED_RETURN(usage, DescriptorType::Undefined);
        }
    }

    ResourceDescriptor CreateResourceDescriptor(ResourceUsageMask usage)
    {
        DescriptorManagerD3D12& descriptor_manager = m_context_dx.GetDescriptorManagerD3D12();
        const DescriptorType descriptor_type = GetDescriptorTypeByUsage(usage);
        DescriptorHeap& descriptor_heap = descriptor_manager.GetDescriptorHeap(descriptor_type);
        return ResourceDescriptor(descriptor_heap, descriptor_heap.AddResource(*this));
    }

private:
    const IRHIContextCommonD3D12& m_context_dx;
    ComPtr<ID3D12Resource> m_resource_cptr;
    std::map<ResourceViewId, ResourceDescriptor> m_descriptor_by_view_id;
};
ARISENRHI_D3D12_END_NAMESPACE
