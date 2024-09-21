#pragma once
#include "RHIVkGPUPipelineManager.h"
#include "RHI/Program/GPUPipeline.h"
#include "RHI/Program/GPUSubPass.h"

namespace NebulaEngine::RHI
{
    class GPUPipelineStateObject;
    
    class RHIVkGPUPipeline final : public GPUPipeline
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPUPipeline)
        ~RHIVkGPUPipeline() noexcept override;
        RHIVkGPUPipeline(RHIVkDevice* device, GPUPipelineStateObject* pipelineStateObject, u32 maxFramesInFlight);
        void* GetGraphicsPipeline(u32 frameIndex) override;

        void AllocGraphicPipeline(u32 frameIndex, GPUSubPass* subPass) override;

        const EPipelineBindPoint GetBindPoint() const override;
        void BindPipelineStateObject(GPUPipelineStateObject* pso) override;
        
    private:

        void FreePipelineLayout(u32 frameIndex);
        void FreePipeline(u32 frameIndex);

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
        Containers::Vector<VkDescriptorSetLayout> m_DescriptorSetLayouts { };
        Containers::Vector<VkPushConstantRange> m_PushConstantRanges { };

    };
}
