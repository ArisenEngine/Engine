#pragma once
#include <dxgi1_4.h>

#include "ContextCommonD3D12.h"
#include "CoreMinimalRHI.h"
#include "DeviceD3D12.h"
#include "RenderContextBase.h"
#include "RHID3D12ImplTraits.h"
#include "Windows/Environment.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
class RenderContextD3D12
    : public ContextCommonD3D12<RenderContextBase<RHID3D12ImplTraits>, RenderContextSettings>
{
public:
    RenderContextD3D12(DeviceD3D12& device, RenderContextSettings settings, Environment environment);

    void Initialize() override;

    virtual Ptr<IRenderPattern> CreateRenderPattern(const RenderPatternSettings& Settings) noexcept override;
    virtual Ptr<IViewState> CreateViewState(const ViewSettings& view_settings) noexcept override;
protected:
    virtual uint32_t GetNextFrameBufferIndex() override;
private:
    bool mIsTearingSupported{false};
    Environment m_environment;
    ComPtr<IDXGISwapChain3> m_swap_chain_cptr;
    HANDLE m_frame_latency_waitable_object;

    
};
ARISENRHI_D3D12_END_NAMESPACE
