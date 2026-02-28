#include "Sync/RHIVkSemaphore.h"

#include "Logger/Logger.h"

ArisenEngine::RHI::RHIVkSemaphore::~RHIVkSemaphore() noexcept
{
    LOG_DEBUG("[RHIVkSemaphore::~RHIVkSemaphore]: ~RHIVkSemaphore");
    vkDestroySemaphore(m_VkDevice, m_VkSemaphore, nullptr);
    LOG_DEBUG("## Destroy Vulkan Semaphore ##");
}

ArisenEngine::RHI::RHIVkSemaphore::RHIVkSemaphore(VkDevice device, bool isTimeline, uint64_t initialValue)
    : RHISemaphore(), m_VkDevice(device), m_IsTimeline(isTimeline)
{
    VkSemaphoreCreateInfo semaphoreInfo{};
    semaphoreInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO;

    VkSemaphoreTypeCreateInfo typeInfo{};
    if (isTimeline)
    {
        typeInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_TYPE_CREATE_INFO;
        typeInfo.semaphoreType = VK_SEMAPHORE_TYPE_TIMELINE;
        typeInfo.initialValue = initialValue;
        semaphoreInfo.pNext = &typeInfo;
    }

    if (vkCreateSemaphore(device, &semaphoreInfo, nullptr, &m_VkSemaphore) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkSemaphore::RHIVkSemaphore]: failed to create semaphore!");
    }
}

void ArisenEngine::RHI::RHIVkSemaphore::Wait(uint64_t value)
{
    if (!m_IsTimeline) return;

    VkSemaphoreWaitInfo waitInfo{};
    waitInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_WAIT_INFO;
    waitInfo.semaphoreCount = 1;
    waitInfo.pSemaphores = &m_VkSemaphore;
    waitInfo.pValues = &value;

    vkWaitSemaphores(m_VkDevice, &waitInfo, UINT64_MAX);
}

void ArisenEngine::RHI::RHIVkSemaphore::Signal(uint64_t value)
{
    if (!m_IsTimeline) return;

    VkSemaphoreSignalInfo signalInfo{};
    signalInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_SIGNAL_INFO;
    signalInfo.semaphore = m_VkSemaphore;
    signalInfo.value = value;

    vkSignalSemaphore(m_VkDevice, &signalInfo);
}

uint64_t ArisenEngine::RHI::RHIVkSemaphore::GetValue()
{
    if (!m_IsTimeline) return 0;

    uint64_t value = 0;
    vkGetSemaphoreCounterValue(m_VkDevice, m_VkSemaphore, &value);
    return value;
}
