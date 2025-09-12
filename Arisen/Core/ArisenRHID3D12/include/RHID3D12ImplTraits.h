#pragma once
#include "CoreMinimalD3D12.h"
#include "IRenderContext.h"
#include "IDescriptorManager.h"
#include "IRenderPattern.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
    class DescriptorManagerD3D12;

struct RHID3D12ImplTraits
{
    using DescriptorInterface = IDescriptorManager;
    using RenderContextInterface = IRenderContext;
    using RenderPatternInterface = IRenderPattern;
    
    using DescriptorManagerImplType = DescriptorManagerD3D12;
};
ARISENRHI_D3D12_END_NAMESPACE
