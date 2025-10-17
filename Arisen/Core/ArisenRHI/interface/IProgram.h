#pragma once
#include "IObject.h"
#include "IShader.h"
#include "RHIMacros.h"
#include "RHITypes.h"
ARISENRHI_BEGIN_NAMEPSACE
struct ProgramSettings
{
    Ptrs<IShader> shaders;
    AttachmentFormats attachment_formats;
};

enum class InputBufferLayoutStepType : uint32_t
{
    Undefined = 0,
    PerVertex,
    PerInstance,
};

struct IProgram : IObject
{
    virtual const Ptr<IShader>& GetShader(ShaderType shader_type) const = 0;
};
ARISENRHI_END_NAMESPACE
