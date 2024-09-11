#pragma once
#include "RHIVkSurface.h"
#include "../Handles/RHIVkImageHandle.h"
#include "../Synchronization/RHIVkSemaphore.h"
#include "RHI/Surfaces/SwapChain.h"

namespace NebulaEngine::RHI
{
    class RHIVkSurface;
    
    class RHIVkSwapChain final : public SwapChain
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkSwapChain)
        RHIVkSwapChain(const VkDevice device, const RHIVkSurface* surface, u32 maxFramesInFlight);
        ~RHIVkSwapChain() noexcept override;
        void* GetHandle() const override { return m_VkSwapChain; };
        void CreateSwapChainWithDesc(Surface* surface, SwapChainDescriptor desc) override;

        RHISemaphore* GetImageAvailableSemaphore(u32 frameIndex) const override;
        RHISemaphore* GetRenderFinishSemaphore(u32 frameIndex) const override;
        ImageHandle* AquireCurrentImage(u32 frameIndex) override;
        void Present(u32 frameIndex) override;
    protected:
        void RecreateSwapChainIfNeeded() override;
    private:
        
        VkSwapchainKHR m_VkSwapChain { VK_NULL_HANDLE };
        VkDevice m_VkDevice;
        VkSurfaceKHR m_VkSurface;
        const RHIVkSurface* m_Surface;
        Containers::Vector<std::unique_ptr<RHIVkImageHandle>> m_ImageHandles;

        Containers::Vector<std::unique_ptr<RHIVkSemaphore>> m_ImageAvailableSemaphores;
        Containers::Vector<std::unique_ptr<RHIVkSemaphore>> m_RenderFinishSemaphores;
        uint32_t m_ImageIndex;
        VkQueue m_VkPresentQueue;
    };
}
