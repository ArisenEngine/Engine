#pragma once
#include "GPUProgram.h"
#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/EDynamicState.h"
#include "RHI/Enums/ShaderStage.h"

namespace NebulaEngine::RHI
{
    // TODO
    struct SpecializationInfoDesc
    {
        
    };
    
    struct PipelineShaderStageDesc
    {
        u32 flag;
        ShaderStage stage;
        GPUProgram& program;
        std::optional<SpecializationInfoDesc> specializationInfo;
    };
    
    class GPUPipeline
    {
        
    public:
        NO_COPY_NO_MOVE(GPUPipeline)
        GPUPipeline() = default;
        virtual ~GPUPipeline() noexcept { m_DynamicPipelineStates.clear(); }
    protected:
        Containers::Vector<EDynamicPipelineState> m_DynamicPipelineStates;
    };
}
