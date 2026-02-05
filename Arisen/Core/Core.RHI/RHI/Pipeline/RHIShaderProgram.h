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
        virtual void SetSpecializationConstant(UInt32 constantID, UInt32 size, const void* data) = 0;
        void SetSpecializationConstant(UInt32 constantID, UInt32 value) { SetSpecializationConstant(constantID, sizeof(UInt32), &value); }

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

