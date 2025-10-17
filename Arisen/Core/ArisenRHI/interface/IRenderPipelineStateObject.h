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
    virtual void Reset(const RenderPipelineStateObjectSettings& settings) = 0;
    virtual void Apply(const struct IRenderCommandList& command_list) const = 0;
    virtual IProgram& GetProgram() = 0;
};
ARISENRHI_END_NAMESPACE
