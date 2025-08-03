#include "RenderContextD3D12.h"

ARISENRHI_BEGIN_NAMEPSACE
RenderContextD3D12::RenderContextD3D12(DeviceD3D12& device, RenderContextSettings settings)
    :ContextDxCommon(device, settings)
{
}

void RenderContextD3D12::Initialize()
{
    
    
    ContextDxCommon<RenderContextBase>::Initialize();
}

ARISENRHI_END_NAMESPACE
