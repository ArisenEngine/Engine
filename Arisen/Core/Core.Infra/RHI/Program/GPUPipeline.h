#pragma once
#include "GPUPipelineStateObject.h"
#include "RHI/Enums/Pipeline/EPipelineBindPoint.h"

namespace ArisenEngine::RHI
{
    class GPUPipeline
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(GPUPipeline)

        explicit GPUPipeline(UInt32 maxFramesInFlight);
        virtual ~GPUPipeline() noexcept = default;

        virtual void* GetGraphicsPipeline(UInt32 frameIndex) = 0;
        
        virtual void AllocGraphicPipeline(UInt32 frameIndex, GPUSubPass* subPass) = 0;
        virtual const EPipelineBindPoint GetBindPoint() const = 0;
        
        virtual void BindPipelineStateObject(GPUPipelineStateObject* pso) = 0;
    protected:
        UInt32 m_MaxFramesInFlight;
    };

    inline GPUPipeline::GPUPipeline(UInt32 maxFramesInFlight):m_MaxFramesInFlight(maxFramesInFlight)
    {
            
    }
}
