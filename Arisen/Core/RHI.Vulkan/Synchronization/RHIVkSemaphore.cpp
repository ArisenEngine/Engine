#include "RHIVkSemaphore.h"

#include "Logger/Logger.h"

ArisenEngine::RHI::RHIVkSemaphore::~RHIVkSemaphore() noexcept
{
    LOG_DEBUG("[RHIVkSemaphore::~RHIVkSemaphore]: ~RHIVkSemaphore")
    vkDestroySemaphore(m_VkDevice, m_VkSemaphore, nullptr);
    LOG_DEBUG("## Destroy Vulkan Semaphore ##");
}

ArisenEngine::RHI::RHIVkSemaphore::RHIVkSemaphore(VkDevice device): RHISemaphore(), m_VkDevice(device)
{
    VkSemaphoreCreateInfo semaphoreInfo{};
    semaphoreInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO;
    
    if (vkCreateSemaphore(device, &semaphoreInfo, nullptr, &m_VkSemaphore) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkSemaphore::RHIVkSemaphore]: failed to create synchronization objects for a frame!");
    }
}
