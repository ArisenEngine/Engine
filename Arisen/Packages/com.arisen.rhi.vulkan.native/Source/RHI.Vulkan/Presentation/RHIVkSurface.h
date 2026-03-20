#pragma once

#include "RHI/Presentation/RHISurface.h"
#include "Definitions/RHIVkCommon.h"

#if VK_USE_PLATFORM_WIN32_KHR
#include <vulkan/vulkan_win32.h>
#endif

#include <optional>

// #include "Presentation/RHIVkSwapChain.h"
#include "Logger/Logger.h"

namespace ArisenEngine::RHI
{
    class RHIVkSwapChain;

    class RHIVkSurface final : public RHISurface
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkSurface);
        ~RHIVkSurface() noexcept override;
        explicit RHIVkSurface(UInt32&& id, RHIInstance* instance);
        [[nodiscard]] void* GetHandle() const override { return m_VkSurface; }

        void InitSwapChain() override;
        const VkQueueFamilyIndices GetQueueFamilyIndices() const { return m_QueueFamilyIndices; }

        RHISwapChain* GetSwapChain() override;

    private:
        friend class RHIVkDevice;
        friend class RHIVkInstance;

        void SetSwapChainSupportDetail(VkSwapChainSupportDetail&& swapChainSupportDetail)
        {
            m_SwapChainSupportDetail = swapChainSupportDetail;
        };

        void SetQueueFamilyIndices(VkQueueFamilyIndices&& queueFaimlyIndices)
        {
            m_QueueFamilyIndices = queueFaimlyIndices;
        }


        VkSurfaceFormatKHR GetDefaultSurfaceFormat();
        VkPresentModeKHR GetDefaultSwapPresentMode();

        const VkSwapChainSupportDetail& GetSwapChainSupportDetail() const { return m_SwapChainSupportDetail; }
        VkSwapChainSupportDetail m_SwapChainSupportDetail;
        VkSurfaceKHR m_VkSurface;

        RHIVkSwapChain* m_SwapChain;
        VkQueueFamilyIndices m_QueueFamilyIndices;
    };
}
