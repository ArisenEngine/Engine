#pragma once
#include "ContextDxCommon.h"
#include "CoreMiminalRHI.h"
#include "DeviceD3D12.h"
#include "RenderContextBase.h"

ARISENRHI_BEGIN_NAMEPSACE
    class RenderContextD3D12
    : public ContextDxCommon<RenderContextBase>
{
public:
    RenderContextD3D12(DeviceD3D12& device, RenderContextSettings settings);

    void Initialize() override;
};
ARISENRHI_END_NAMESPACE
