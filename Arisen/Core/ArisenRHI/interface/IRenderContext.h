#pragma once
#include "BasicTypes.h"
#include "CoreMiminalRHI.h"
#include "IContext.h"

ARISENRHI_BEGIN_NAMEPSACE

struct RenderContextSettings
{
    
    FrameSize frame_size;
};

struct IRenderContext : virtual IContext
{
};
ARISENRHI_END_NAMESPACE
