#pragma once
#include "../../Common/CommandHeaders.h"
#include "../RHICommon.h"
#include "RHI/Enums/Pipeline/ShaderStage.h"

namespace NebulaEngine::RHI
{
    class GPUProgram
    {
    public:
        GPUProgram() = default;
        NO_COPY_NO_MOVE(GPUProgram)
        VIRTUAL_DECONSTRUCTOR(GPUProgram)
        virtual void* GetHandle() const = 0;
        const char* GetEntry() const { return m_Entry; }
        virtual bool AttachProgramByteCode(GPUProgramDesc&& desc) = 0;

    protected:    
        virtual void DestroyHandle() = 0;
        ShaderStage m_Stage;
        const char* m_Entry { nullptr };
    };
}
