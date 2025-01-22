#include "RHIVkFence.h"

#include "Logger/Logger.h"

ArisenEngine::RHI::RHIVkFence::RHIVkFence(VkDevice device) : RHIFence(), m_VkDevice(device)
{
    VkFenceCreateInfo fenceInfo{};
    fenceInfo.sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
    fenceInfo.flags = VK_FENCE_CREATE_SIGNALED_BIT;
    
    if (vkCreateFence(device, &fenceInfo, nullptr, &m_VkFence) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkFence::RHIVkFence]: failed to create synchronization objects for a frame!");
    }
}

ArisenEngine::RHI::RHIVkFence::~RHIVkFence() noexcept
{
    LOG_DEBUG("[RHIVkFence::~RHIVkFence]: ~RHIVkFence");
    vkDestroyFence(m_VkDevice, m_VkFence, nullptr);
    LOG_DEBUG("## Destroy Vulkan Fence ##");
}

void ArisenEngine::RHI::RHIVkFence::Lock()
{
    vkWaitForFences(m_VkDevice, 1, &m_VkFence, VK_TRUE, UINT64_MAX);
}

void ArisenEngine::RHI::RHIVkFence::Unlock()
{
    vkResetFences(m_VkDevice, 1, &m_VkFence);
}
