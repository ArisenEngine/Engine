#include "RHIVkGPUPipeline.h"
#include "Logger/Logger.h"

/**
    Shader stages: the shader modules that define the functionality of the programmable stages of the graphics pipeline
    Fixed-function state: all of the structures that define the fixed-function stages of the pipeline, like input assembly, rasterizer, viewport and color blending
    Pipeline layout: the uniform and push values referenced by the shader that can be updated at draw time
    Render pass: the attachments referenced by the pipeline stages and their usage 
 */

NebulaEngine::RHI::RHIVkGPUPipeline::RHIVkGPUPipeline(VkDevice device):GPUPipeline(), m_VkDevice(device)
{
    
}

NebulaEngine::RHI::RHIVkGPUPipeline::~RHIVkGPUPipeline() noexcept
{
    if (m_VkPipeline != VK_NULL_HANDLE)
    {
        vkDestroyPipeline(m_VkDevice, m_VkPipeline, nullptr);
        LOG_DEBUG("## Destroy Vulkan Pipeline ##");
    }

    if (m_VkPipelineLayout != VK_NULL_HANDLE)
    {
        vkDestroyPipelineLayout(m_VkDevice, m_VkPipelineLayout, nullptr);
        LOG_DEBUG("## Destroy Vulkan Pipeline Layout ##");
    }
}
