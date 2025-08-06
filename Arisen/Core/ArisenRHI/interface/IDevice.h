#pragma once
#include "CoreMinimalRHI.h"
#include "IObject.h"
#include "Windows/Environment.h"

ARISENRHI_BEGIN_NAMEPSACE
    struct IRenderContext;
struct RenderContextSettings;

struct IDevice : public IObject
{
    virtual Ptr<IRenderContext> CreateRenderContext(const RenderContextSettings& render_context_settings, const Environment environment)= 0;
};


ARISENRHI_END_NAMESPACE
