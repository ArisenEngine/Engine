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
        RHIVkGPUPipeline(RHIVkDevice* device, GPUPipelineStateObject* pipelineStateObject);
        void* GetGraphicsPipeline() override {  ASSERT(m_VkGraphicPipeline != VK_NULL_HANDLE); return m_VkGraphicPipeline; }
        
        void AllocGraphicPipeline(GPUSubPass* subPass) override;

        const EPipelineBindPoint GetBindPoint() const override;
        void BindPipelineStateObject(GPUPipelineStateObject* pso) override;
        
    private:

        void FreePipelineLayout();
        void FreePipeline();
        
        // device
        VkDevice m_VkDevice;
        RHIVkDevice* m_Device;

        // subPass
        GPUSubPass* m_SubPass;
        
        // graphics pipeline
        GPUPipelineStateObject* m_PipelineStateObject;
        VkPipeline m_VkGraphicPipeline { VK_NULL_HANDLE };
        VkPipelineLayout m_VkGraphicsPipelineLayout { VK_NULL_HANDLE };
        Containers::Vector<VkDescriptorSetLayout> m_DescriptorSetLayouts { };
        Containers::Vector<VkPushConstantRange> m_PushConstantRanges { };

    };
}
