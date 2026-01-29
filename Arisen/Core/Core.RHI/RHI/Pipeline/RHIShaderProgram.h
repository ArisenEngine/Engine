#pragma once
#include "Base/FoundationMinimal.h"
#include "../Core/RHICommon.h"
#include "RHI/Enums/Pipeline/EShaderStage.h"

namespace ArisenEngine::RHI
{
    class RHIShaderProgram
    {
    public:
        RHIShaderProgram() = default;
        NO_COPY_NO_MOVE(RHIShaderProgram)
        VIRTUAL_DECONSTRUCTOR(RHIShaderProgram)
        virtual void* GetHandle() const = 0;
        const char* GetEntry() const { return m_Entry.c_str(); }
        virtual bool AttachProgramByteCode(RHIShaderProgramDesc&& desc) = 0;
        virtual UInt32 GetShaderStageCreateFlags() = 0;
        virtual void* GetSpecializationInfo() = 0;

    public:
        const EShaderStage GetShaderState() const { return m_Stage; }
        const String& GetName() const { return m_Name; }

    
    protected:    
        virtual void DestroyHandle() = 0;
        EShaderStage m_Stage;
        String m_Entry {};
        String m_Name {};
    };
}

