#pragma once
#include "../../Common/CommandHeaders.h"
#include "../RHICommon.h"
#include "RHI/Enums/Pipeline/EShaderStage.h"

namespace NebulaEngine::RHI
{
    class GPUProgram
    {
    public:
        GPUProgram() = default;
        NO_COPY_NO_MOVE(GPUProgram)
        VIRTUAL_DECONSTRUCTOR(GPUProgram)
        virtual void* GetHandle() const = 0;
        const char* GetEntry() const { return m_Entry.c_str(); }
        virtual bool AttachProgramByteCode(GPUProgramDesc&& desc) = 0;
        virtual UInt32 GetShaderStageCreateFlags() = 0;
        virtual void* GetSpecializationInfo() = 0;

    public:
        const EShaderStage GetShaderState() const { return m_Stage; }
        const std::string GetName() const { return m_Name; }

    
    protected:    
        virtual void DestroyHandle() = 0;
        EShaderStage m_Stage;
        std::string m_Entry {};
        std::string m_Name {};
    };
}
