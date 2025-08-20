#pragma once
#include "CoreMinimalD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE

enum class DescriptorType
{
    ShaderResources = 0,
    Sampler,

    RenderTargets,
    DepthStencil,

    Undefined
};

class DescriptorHeap
{
public:
    
};
ARISENRHI_D3D12_END_NAMESPACE
