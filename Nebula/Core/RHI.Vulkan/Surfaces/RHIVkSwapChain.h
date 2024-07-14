#pragma once
#include "../Common.h"
#include "../Handles/RHIVkImageHandle.h"
#include "RHI/Surfaces/SwapChain.h"

namespace NebulaEngine::RHI
{
    class RHIVkSwapChain final : public SwapChain
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkSwapChain)
        RHIVkSwapChain(const VkDevice device, const VkSurfaceKHR surface);
        ~RHIVkSwapChain() noexcept override;
        void* GetHandle() const override { return m_VkSwapChain; };
        void CreateSwapChainWithDesc(SwapChainDescriptor desc) override;
        
    protected:
        void RecreateSwapChainIfNeeded() override;
    private:
        
        VkSwapchainKHR m_VkSwapChain { VK_NULL_HANDLE };
        VkDevice m_VkDevice;
        VkSurfaceKHR m_VkSurface;
        Containers::Vector<std::unique_ptr<RHIVkImageHandle>> m_ImageHandles;
    };
}
