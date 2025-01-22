#pragma once
#include "RHIVkGPUPipelineManager.h"
#include "RHI/Program/GPUPipeline.h"
#include "RHI/Program/GPUSubPass.h"

namespace ArisenEngine::RHI
{
    class GPUPipelineStateObject;
    
    class RHIVkGPUPipeline final : public GPUPipeline
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPUPipeline)
        ~RHIVkGPUPipeline() noexcept override;
        RHIVkGPUPipeline(RHIVkDevice* device, GPUPipelineStateObject* pipelineStateObject, UInt32 maxFramesInFlight);
        void* GetGraphicsPipeline(UInt32 frameIndex) override;

        void AllocGraphicPipeline(UInt32 frameIndex, GPUSubPass* subPass) override;

        const EPipelineBindPoint GetBindPoint() const override;
        void BindPipelineStateObject(GPUPipelineStateObject* pso) override;
        
    private:

        void FreePipelineLayout(UInt32 frameIndex);
        void FreePipeline(UInt32 frameIndex);

        void FreeAllPipelineLayouts();
        void FreeAllPipelines();
        
        // device
        VkDevice m_VkDevice;
        RHIVkDevice* m_Device;

        // subPass
        GPUSubPass* m_SubPass;
        
        // graphics pipeline
        GPUPipelineStateObject* m_PipelineStateObject;
        Containers::Vector<VkPipeline> m_VkGraphicPipelines;
        Containers::Vector<VkPipelineLayout> m_VkGraphicsPipelineLayouts;
        Containers::Vector<VkPushConstantRange> m_PushConstantRanges { };

    };
}
