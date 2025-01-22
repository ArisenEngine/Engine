#pragma once
#include <vulkan/vulkan_core.h>

#include "RHI/Synchronization/RHISemaphore.h"

namespace ArisenEngine::RHI
{
    class RHISemaphore;
    class RHIVkSemaphore final : public RHISemaphore
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkSemaphore)
        ~RHIVkSemaphore() noexcept override;
        RHIVkSemaphore(VkDevice device);
        void* GetHandle() override { return m_VkSemaphore; }
        void Lock() override {};
        void Unlock() override {};
    private:
        VkSemaphore m_VkSemaphore;
        VkDevice m_VkDevice;
    };
}
