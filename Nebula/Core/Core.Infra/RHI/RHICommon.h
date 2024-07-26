#pragma once
#include "../Common/CommandHeaders.h"

#include "Enums/ShaderStage.h"

namespace NebulaEngine::RHI
{
    typedef struct GPUProgramDesc
    {
        size_t codeSize;
        void* byteCode;
        const char* entry;
        ShaderStage stage;
    } GPUProgramDesc;
    
}
