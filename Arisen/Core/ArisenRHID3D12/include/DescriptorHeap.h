#pragma once
#include "CoreMinimalD3D12.h"
#include "IRHIContext.h"
#include "ObjectBase.h"
#include <wrl/client.h>
#include <d3d12.h>

ARISENRHI_D3D12_BEGIN_NAMEPSACE
using namespace Microsoft::WRL;

enum class DescriptorType
{
    ShaderResources = 0,
    Sampler,

    RenderTargets,
    DepthStencil,

    Undefined
};

struct IDescriptorHeap : public IObject
{
    
};

struct DescriptorHeapSettings
{
    DescriptorType type;
    uint32_t size;
    bool shader_visible;
};

class DescriptorHeap : public ObjectBase<IDescriptorHeap>
{
public:
    DescriptorHeap(const IRHIContext& context, const DescriptorHeapSettings& settings);

    static bool IsShaderVisibleHeapType(DescriptorType type);

    void Allocate();
    uint32_t GetAllocatedSize() const;
    uint32_t GetDeferredSize() const;
    
private:
    IRHIContext& m_context;
    DescriptorHeapSettings m_settings;

    uint32_t m_allocated_size{0};
    uint32_t m_deferred_size;
    D3D12_DESCRIPTOR_HEAP_TYPE m_native_heap_type;
    uint32_t m_native_heap_size;
    ComPtr<ID3D12DescriptorHeap> m_native_heap_ptr;
};
ARISENRHI_D3D12_END_NAMESPACE
