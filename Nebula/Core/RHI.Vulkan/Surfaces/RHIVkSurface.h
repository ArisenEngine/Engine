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
    class DLL RHIVkSurface final : public Surface
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkSurface);
        ~RHIVkSurface() noexcept override;
        explicit RHIVkSurface(u32&& id, Instance*  instance);
        [[nodiscard]] void* GetHandle() const override { return m_VkSurface; }

        void InitSwapChain() override;
    private:

        friend class RHIVkDevice;

        void SetSwapChainSupportDetail(SwapChainSupportDetail&& swapChainSupportDetail) { m_SwapChainSupportDetail = swapChainSupportDetail; };
        const SwapChainSupportDetail& GetSwapChainSupportDetail() const { return m_SwapChainSupportDetail; }
        SwapChainSupportDetail m_SwapChainSupportDetail;
        VkSurfaceKHR m_VkSurface;

        std::unique_ptr<RHIVkSwapChain> m_SwapChain;
        
    };
}
