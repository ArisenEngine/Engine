#pragma once
#include "ContextBase.h"
#include "CoreMiminalRHI.h"
#include "IDevice.h"
#include "IRenderContext.h"

ARISENRHI_BEGIN_NAMEPSACE
class RenderContextBase : public ContextBase<IRenderContext>
{
public:
    RenderContextBase(IDevice& device, const RenderContextSettings& settings);

    virtual void Initialize() override;
private:
    RenderContextSettings m_settings;
    
};
ARISENRHI_END_NAMESPACE
