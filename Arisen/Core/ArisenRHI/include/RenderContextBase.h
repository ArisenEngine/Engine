#pragma once
#include "ContextBase.h"
#include "CoreMiminalRHI.h"
#include "IDevice.h"
#include "IRenderContext.h"

ARISENRHI_BEGIN_NAMEPSACE
class RenderContextBase : virtual IRenderContext
    , virtual ContextBase
{
public:
    RenderContextBase(IDevice& device, const RenderContextSettings& settings);

private:
    RenderContextSettings m_settings;
    
};
ARISENRHI_END_NAMESPACE
