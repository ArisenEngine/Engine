#pragma once
#include "BasicTypes.h"
#include "CoreMiminalRHI.h"
#include "IContext.h"

ARISENRHI_BEGIN_NAMEPSACE

struct RenderContextSettings
{
    FrameSize frame_size{800, 600};
};

struct IRenderContext : virtual IContext
{
    using Settings = RenderContextSettings;
};
ARISENRHI_END_NAMESPACE
