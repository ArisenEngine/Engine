#pragma once
#include "GPUPipelineStateObject.h"
#include "RHI/Enums/Pipeline/EPipelineBindPoint.h"

namespace NebulaEngine::RHI
{
    class GPUPipeline
    {
    public:
        NO_COPY_NO_MOVE(GPUPipeline)
        GPUPipeline() = default;
        virtual ~GPUPipeline() noexcept = default;

        virtual void* GetGraphicsPipeline() = 0;
        
        virtual void AllocGraphicPipeline(GPUSubPass* subPass) = 0;
        virtual const EPipelineBindPoint GetBindPoint() const = 0;
        
        virtual void BindPipelineStateObject(GPUPipelineStateObject* pso) = 0;
    private:
        
    };
}
