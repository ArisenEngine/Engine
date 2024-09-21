#pragma once
#include "GPUPipelineStateObject.h"
#include "RHI/Enums/Pipeline/EPipelineBindPoint.h"

namespace NebulaEngine::RHI
{
    class GPUPipeline
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(GPUPipeline)

        explicit GPUPipeline(u32 maxFramesInFlight);
        virtual ~GPUPipeline() noexcept = default;

        virtual void* GetGraphicsPipeline(u32 frameIndex) = 0;
        
        virtual void AllocGraphicPipeline(u32 frameIndex, GPUSubPass* subPass) = 0;
        virtual const EPipelineBindPoint GetBindPoint() const = 0;
        
        virtual void BindPipelineStateObject(GPUPipelineStateObject* pso) = 0;
    protected:
        u32 m_MaxFramesInFlight;
    };

    inline GPUPipeline::GPUPipeline(u32 maxFramesInFlight):m_MaxFramesInFlight(maxFramesInFlight)
    {
            
    }
}
