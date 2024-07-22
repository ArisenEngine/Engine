#pragma once
#include "../Common/CommandHeaders.h"

#include "Enums/ShaderStage.h"

namespace NebulaEngine::RHI
{
    typedef struct GPUProgramDesc
    {
        size_t codeSize;
        const uint32_t* byteCode;
        const char* entry;
        ShaderStage stage;
    } GPUProgramDesc;
    
}
