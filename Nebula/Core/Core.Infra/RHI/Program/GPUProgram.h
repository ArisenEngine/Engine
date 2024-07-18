#pragma once
#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/PipelineShaderStageCreateFlagBits.h"
#include "RHI/Enums/ProgramStage.h"
#include "RHI/Enums/ShaderStage.h"

namespace NebulaEngine::RHI
{
    struct GPUProgramDesc
    {
        size_t codeSize;
        const uint32_t* byteCode;
        const char* entry;
        ShaderStage stage;
    };
    
    class GPUProgram
    {
    public:
        GPUProgram() = default;
        NO_COPY_NO_MOVE(GPUProgram)
        VIRTUAL_DECONSTRUCTOR(GPUProgram)
        virtual void* GetHandle() const = 0;
        virtual bool AttachProgramByteCode(GPUProgramDesc&& desc) = 0;

    protected:    
        virtual void DestroyHandle() = 0;
        ShaderStage m_Stage;
    };
}
