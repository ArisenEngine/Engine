#pragma once
#include "GPUProgram.h"
#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/Pipeline/EDynamicState.h"
#include "RHI/Enums/Pipeline/EPrimitiveTopology.h"

namespace NebulaEngine::RHI
{
    // TODO
    struct SpecializationInfoDesc
    {
        
    };
    
    struct PipelineShaderStageDesc
    {
        u32 flag;
        EShaderStage stage;
        GPUProgram& program;
        std::optional<SpecializationInfoDesc> specializationInfo;
    };
    
    class GPUPipeline
    {
        
    public:
        NO_COPY_NO_MOVE(GPUPipeline)
        GPUPipeline() = default;
        virtual ~GPUPipeline() noexcept { ClearDynamicPipelineStates(); }

        virtual void* GetGraphicsPipeline() = 0;
        virtual void AddProgram(u32 programId) = 0;
        
        virtual void AllocGraphicsPipeline(GPUSubPass* subPass) = 0;
        virtual void AllocGraphicsPipelineLayout() = 0;
        virtual void FreeGraphicsPipelineLayout() = 0;
        virtual void FreeGraphicsPipeline() = 0;

    public:

        void ClearDynamicPipelineStates() { m_DynamicPipelineStates.clear(); }
        void AddDynamicPipelineState(EDynamicPipelineState state)
        {
            m_DynamicPipelineStates.emplace_back(state);
        }

        void EnablePrimitiveRestart()
        {
            m_PrimitiveRestart = true;
        }

        void DisablePrimitiveRestart()
        {
            m_PrimitiveRestart = false;
        }

        void SetPrimitiveTopology(EPrimitiveTopology topology)
        {
            m_PrimitiveTopology = topology;
        }
        
    protected:
        
        // input assembly
        bool m_PrimitiveRestart {false};
        EPrimitiveTopology m_PrimitiveTopology { EPrimitiveTopology::PRIMITIVE_TOPOLOGY_TRIANGLE_LIST };
        Containers::Vector<EDynamicPipelineState> m_DynamicPipelineStates;
    };
}
