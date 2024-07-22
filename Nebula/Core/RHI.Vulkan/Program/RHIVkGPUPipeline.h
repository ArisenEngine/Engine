#pragma once
#include <vulkan/vulkan_core.h>

#include "RHIVkGPURenderPass.h"
#include "RHI/Program/GPUPipeline.h"

namespace NebulaEngine::RHI
{
    class RHIVkGPUPipeline final : public GPUPipeline
    {

    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPUPipeline);
        RHIVkGPUPipeline(VkDevice device);
        ~RHIVkGPUPipeline() noexcept override;



    private:
        Containers::Vector<std::unique_ptr<RHIVkGPURenderPass>> m_RenderPasses;
        VkPipelineLayout m_VkPipelineLayout;
        VkPipeline m_VkPipeline;
        VkDevice m_VkDevice;
        // VkPipelineShaderStageCreateInfo ;
        // VkGraphicsPipelineCreateInfo;
        // VkComputePipelineCreateInfo;
    };
}
