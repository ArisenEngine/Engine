#pragma once
#include "GPUPipeline.h"
#include "GPUProgram.h"
#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/Pipeline/EDynamicState.h"
#include "RHI/Enums/Pipeline/EPrimitiveTopology.h"

namespace ArisenEngine::RHI
{
    class GPUPipelineStateObject;

    // TODO
    struct SpecializationInfoDesc
    {
        
    };
    
    struct PipelineShaderStageDesc
    {
        UInt32 flag;
        EShaderStage stage;
        GPUProgram& program;
        std::optional<SpecializationInfoDesc> specializationInfo;
    };
    
    class GPUPipelineManager
    {
    public:
        
        NO_COPY_NO_MOVE_NO_DEFAULT(GPUPipelineManager)
        GPUPipelineManager(UInt32 maxFramesInFlight);
        virtual ~GPUPipelineManager() noexcept = default;
        virtual GPUPipeline* GetGraphicsPipeline(GPUPipelineStateObject* pso) = 0;

        virtual std::unique_ptr<GPUPipelineStateObject> GetPipelineState() = 0;
    protected:
        UInt32 m_MaxFramesInFlight;
    };

    inline GPUPipelineManager::GPUPipelineManager(UInt32 maxFramesInFlight):m_MaxFramesInFlight(maxFramesInFlight)
    {
            
    }
}
