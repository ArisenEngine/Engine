#pragma once
#include <vulkan/vulkan_core.h>
#include "RHIVkGPURenderPass.h"
#include "RHI/Program/GPUPipelineManager.h"

namespace NebulaEngine::RHI
{
    class RHIVkDevice;

    class RHIVkGPUPipeline final : public GPUPipelineManager
    {

    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPUPipeline);
        RHIVkGPUPipeline(RHIVkDevice* device);
        ~RHIVkGPUPipeline() noexcept override;
        
        void* GetGraphicsPipeline() override { ASSERT(m_VkGraphicPipeline != VK_NULL_HANDLE); return m_VkGraphicPipeline; }
        
        void AddProgram(u32 programId) override;

        void AllocGraphicsPipeline(GPUSubPass* subPass) override;
        void AllocGraphicsPipelineLayout() override;
        void FreeGraphicsPipelineLayout() override;
        void FreeGraphicsPipeline() override;
        
    private:

        // pipeline layout
        VkPipelineLayout m_VkGraphicsPipelineLayout { VK_NULL_HANDLE };
        Containers::Vector<VkDescriptorSetLayout> m_DescriptorSetLayouts;
        Containers::Vector<VkPushConstantRange> m_PushConstantRanges;
        // graphics pipeline 
        VkPipeline m_VkGraphicPipeline { VK_NULL_HANDLE };

        
        VkDevice m_VkDevice;
        RHIVkDevice* m_Device;
        
        Containers::Vector<VkPipelineShaderStageCreateInfo> m_PipelineStageCreateInfos;
        Containers::Vector<VkVertexInputBindingDescription> m_VertexInputBindingDescriptions;
        Containers::Vector<VkVertexInputAttributeDescription> m_VertexInputAttributeDescriptions;

        
        
        // VkGraphicsPipelineCreateInfo;
        // VkComputePipelineCreateInfo;
    };
}
