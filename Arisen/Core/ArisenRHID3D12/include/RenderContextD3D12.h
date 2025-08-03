#pragma once
#include "CoreMiminalRHI.h"
#include "DeviceD3D12.h"
#include "RenderContextBase.h"

ARISENRHI_BEGIN_NAMEPSACE
    class RenderContextD3D12
    : public RenderContextBase
{
public:
    RenderContextD3D12(DeviceD3D12& device, RenderContextSettings settings);
};
ARISENRHI_END_NAMESPACE
