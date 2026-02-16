#pragma once
#include "Base/FoundationMinimal.h"
#include "../Core/RHICommon.h"
#include "RHI/Enums/Pipeline/EShaderStage.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    class RHI_DLL RHIShaderProgram
    {
    public:
        RHIShaderProgram();
        NO_COPY_NO_MOVE(RHIShaderProgram)
        virtual ~RHIShaderProgram() noexcept;
        virtual void* GetHandle() const = 0;
        const char* GetEntry() const;
        virtual bool AttachProgramByteCode(RHIShaderProgramDesc&& desc) = 0;
        virtual UInt32 GetShaderStageCreateFlags() = 0;
        virtual void* GetSpecializationInfo() = 0;
        virtual void SetSpecializationConstant(UInt32 constantID, UInt32 size, const void* data) = 0;
        void SetSpecializationConstant(UInt32 constantID, UInt32 value) { SetSpecializationConstant(constantID, sizeof(UInt32), &value); }

    public:
        const EShaderStage GetShaderState() const;
        const String& GetName() const;

    
    protected:    
        virtual void DestroyHandle() = 0;
        EShaderStage m_Stage;
        String m_Entry {};
        String m_Name {};
    };
}

