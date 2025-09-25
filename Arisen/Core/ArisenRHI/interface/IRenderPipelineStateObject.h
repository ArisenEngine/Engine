#pragma once
#include "IObject.h"
#include "IProgram.h"
#include "IRenderPattern.h"
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE

struct RenderPipelineStateObjectSettings
{
    Ptr<IProgram> program;
    IRenderPattern* render_pattern;
};

struct IRenderPipelineStateObject : IObject
{
    
};
ARISENRHI_END_NAMESPACE