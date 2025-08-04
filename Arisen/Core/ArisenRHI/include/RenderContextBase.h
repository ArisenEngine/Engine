#pragma once
#include "ContextBase.h"
#include "CoreMinimalRHI.h"
#include "IDevice.h"
#include "IRenderContext.h"

ARISENRHI_BEGIN_NAMEPSACE
class RenderContextBase : public ContextBase<IRenderContext>
{
public:
    RenderContextBase(IDevice& device, const RenderContextSettings& settings);

    virtual void Initialize() override;

    const virtual Settings& GetSettings() const noexcept final {return mSettings;}
    
private:
    RenderContextSettings mSettings;
    
};
ARISENRHI_END_NAMESPACE
