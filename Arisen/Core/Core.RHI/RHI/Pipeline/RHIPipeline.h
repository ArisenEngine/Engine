#pragma once
#include "RHIPipelineState.h"
#include "RHI/Enums/Pipeline/EPipelineBindPoint.h"

namespace ArisenEngine::RHI
{
    class RHISubPass;
    class RHIPipeline
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIPipeline)

        explicit RHIPipeline(UInt32 maxFramesInFlight);
        virtual ~RHIPipeline() noexcept = default;

        virtual void* GetGraphicsPipeline(UInt32 frameIndex) = 0;
        virtual void* GetComputePipeline(UInt32 frameIndex) = 0;
        
        virtual void AllocGraphicPipeline(UInt32 frameIndex, RHISubPass* subPass) = 0;
        virtual void AllocComputePipeline(UInt32 frameIndex) = 0;
        virtual const EPipelineBindPoint GetBindPoint() const = 0;
        
        virtual void BindPipelineStateObject(RHIPipelineState* pso) = 0;
        virtual RHIPipelineState* GetPipelineStateObject() const = 0;
    protected:
        UInt32 m_MaxFramesInFlight;
    };

    inline RHIPipeline::RHIPipeline(UInt32 maxFramesInFlight):m_MaxFramesInFlight(maxFramesInFlight)
    {
            
    }
}
