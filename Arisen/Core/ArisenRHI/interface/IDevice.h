#pragma once
#include "CoreMiminalRHI.h"
#include "IObject.h"
#include "IRenderContext.h"

ARISENRHI_BEGIN_NAMEPSACE
struct IDevice : public IObject
{
    virtual Ptr<IRenderContext> CreateRenderContext(const RenderContextSettings& render_context_settings)= 0;
};


ARISENRHI_END_NAMESPACE
