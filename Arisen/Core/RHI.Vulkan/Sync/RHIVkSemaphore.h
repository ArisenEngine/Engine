#pragma once
#include <vulkan/vulkan_core.h>

#include "RHI/Sync/RHISemaphore.h"

namespace ArisenEngine::RHI
{
    class RHISemaphore;

    class RHIVkSemaphore final : public RHISemaphore
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkSemaphore)
        ~RHIVkSemaphore() noexcept override;
        RHIVkSemaphore(VkDevice device, bool isTimeline = false, uint64_t initialValue = 0);
        void* GetHandle() override { return m_VkSemaphore; }

        void Wait() override
        {
        }

        void Signal() override
        {
        }

        // Timeline semaphore support
        void Wait(uint64_t value) override;
        void Signal(uint64_t value) override;
        uint64_t GetValue() override;
        bool IsTimeline() const override { return m_IsTimeline; }

    private:
        VkSemaphore m_VkSemaphore;
        VkDevice m_VkDevice;
        bool m_IsTimeline;
    };
}
