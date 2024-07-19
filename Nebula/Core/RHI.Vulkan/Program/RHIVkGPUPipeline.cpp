#include "RHIVkGPUPipeline.h"

#include "Logger/Logger.h"

NebulaEngine::RHI::RHIVkGPUPipeline::RHIVkGPUPipeline(VkDevice device):GPUPipeline(), m_VkDevice(device)
{
}

NebulaEngine::RHI::RHIVkGPUPipeline::~RHIVkGPUPipeline() noexcept
{
    vkDestroyPipelineLayout(m_VkDevice, m_VkPipelineLayout, nullptr);
    LOG_DEBUG("## Destroy Vulkan Pipeline Layout ##");

    m_RenderPasses.clear();
}
