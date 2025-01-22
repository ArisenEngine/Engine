#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Synchronization/RHIFence.h"

namespace ArisenEngine::RHI
{
    class RHIFence;
    class RHIVkFence final : public RHIFence
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkFence)
        RHIVkFence(VkDevice device);
        ~RHIVkFence() noexcept override;
        void* GetHandle() override { return m_VkFence; }
        void Lock() override;
        void Unlock() override;
    private:
        VkFence m_VkFence;
        VkDevice m_VkDevice;
    };
}
