#include "RHIVkGPURenderPass.h"

#include "Logger/Logger.h"

NebulaEngine::RHI::RHIVkGPURenderPass::RHIVkGPURenderPass(VkDevice device): GPURenderPass(), m_VkDevice(device)
{
}

NebulaEngine::RHI::RHIVkGPURenderPass::~RHIVkGPURenderPass() noexcept
{
    vkDestroyRenderPass(m_VkDevice, m_VkRenderPass, nullptr);
    LOG_DEBUG("## Destroy Vulkan Render Pass ##");
}
