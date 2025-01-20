#pragma once

#include "RHI/Surfaces/Surface.h"
#include "../Common.h"

#if VK_USE_PLATFORM_WIN32_KHR
#include <vulkan/vulkan_win32.h>
#endif

#include <optional>

#include "RHIVkSwapChain.h"
#include "Logger/Logger.h"

namespace NebulaEngine::RHI
{
    class RHIVkSwapChain;
    
    class RHIVkSurface final : public Surface
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkSurface);
        ~RHIVkSurface() noexcept override;
        explicit RHIVkSurface(UInt32&& id, Instance*  instance);
        [[nodiscard]] void* GetHandle() const override { return m_VkSurface; }

        void InitSwapChain() override;
        const VkQueueFamilyIndices GetQueueFamilyIndices() const { return m_QueueFamilyIndices; }

        SwapChain* GetSwapChain() override;
    private:

        friend class RHIVkDevice;
        friend class RHIVkInstance;

        void SetSwapChainSupportDetail(VkSwapChainSupportDetail&& swapChainSupportDetail) { m_SwapChainSupportDetail = swapChainSupportDetail; };
        void SetQueueFamilyIndices(VkQueueFamilyIndices&& queueFaimlyIndices) { m_QueueFamilyIndices = queueFaimlyIndices; }
       

        VkSurfaceFormatKHR GetDefaultSurfaceFormat();
        VkPresentModeKHR GetDefaultSwapPresentMode();
        
        const VkSwapChainSupportDetail& GetSwapChainSupportDetail() const { return m_SwapChainSupportDetail; }
        VkSwapChainSupportDetail m_SwapChainSupportDetail;
        VkSurfaceKHR m_VkSurface;

        RHIVkSwapChain* m_SwapChain;
        VkQueueFamilyIndices m_QueueFamilyIndices;
        
    };
}
