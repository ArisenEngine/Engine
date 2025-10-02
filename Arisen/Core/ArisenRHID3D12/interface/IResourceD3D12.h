#pragma once
#include "RHIMacrosD3D12.h"
#include "../../ArisenRHI/interface/IResource.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE

// Forward declaration to avoid circular dependency
struct ResourceDescriptor;

struct IResourceD3D12
{
    virtual Opt<ResourceDescriptor> InitializeNativeViewDescriptor(const ResourceViewId& view_id) = 0;
    ID3D12Resource* GetNativeResource() const;
};

ARISENRHI_D3D12_END_NAMESPACE