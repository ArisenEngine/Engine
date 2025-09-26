#pragma once
#include "IObject.h"
#include "IShader.h"
#include "RHIMacros.h"
#include "RHITypes.h"
ARISENRHI_BEGIN_NAMEPSACE
    struct ProgramSettings
{
    std::map<ShaderType, ShaderSettings> shader_set;
    AttachmentFormats attachment_formats;
};

struct IProgram : IObject
{
    
};
ARISENRHI_END_NAMESPACE