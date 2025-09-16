#pragma once
#include "CoreMinimalD3D12.h"
#include "IRenderContext.h"
#include "IDescriptorManager.h"
#include "IRenderContextD3D12.h"
#include "IRenderPassD3D12.h"
#include "IRenderPattern.h"
#include "ITextureD3D12.h"
#include "IViewStateD3D12.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
    class DescriptorManagerD3D12;

struct RHID3D12ImplTraits
{
    using DescriptorInterface = IDescriptorManager;
    using RenderContextInterface = IRenderContextD3D12;
    using RenderPatternInterface = IRenderPattern;
    using ViewStateInterface = IViewStateD3D12;
    using TextureInterface = ITextureD3D12;
    using RenderPassInterface = IRenderPassD3D12;
    
    using DescriptorManagerImplType = DescriptorManagerD3D12;
};
ARISENRHI_D3D12_END_NAMESPACE
