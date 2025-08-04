#pragma once
#include "ContextDxCommon.h"
#include "CoreMinimalRHI.h"
#include "DeviceD3D12.h"
#include "RenderContextBase.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
class RenderContextD3D12
: public ContextDxCommon<RenderContextBase>
{
public:
    RenderContextD3D12(DeviceD3D12& device, RenderContextSettings settings);

    void Initialize() override;

    
private:
    bool mIsTearingSupported{false};
};
ARISENRHI_D3D12_END_NAMESPACE
