#pragma once
#include "Presentation/RHIVkSurface.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Presentation/RHISwapChain.h"

namespace ArisenEngine::RHI
{
    class RHIVkSurface;

    class RHIVkSwapChain final : public RHISwapChain
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkSwapChain)
        RHIVkSwapChain(RHIDevice* device, const RHIVkSurface* surface, UInt32 maxFramesInFlight);
        ~RHIVkSwapChain() noexcept override;
        void* GetHandle() const override { return m_VkSwapChain; };
        void CreateSwapChainWithDesc(RHISwapChainDescriptor desc) override;
        RHIImageHandle BeginFrame(UInt32 frameIndex) override;
        void EndFrame(UInt32 frameIndex) override;

        RHISemaphoreHandle GetImageAvailableSemaphore(UInt32 frameIndex) const override;
        RHISemaphoreHandle GetRenderFinishSemaphore(UInt32 frameIndex) const override;
        RHIImageHandle AcquireCurrentImage(UInt32 frameIndex) override;
        RHIImageViewHandle GetImageView(UInt32 frameIndex) const override;
        void Cleanup() override;
        void Present(UInt32 frameIndex) override;

    protected:
        void RecreateSwapChainIfNeeded() override;

    private:
        VkSwapchainKHR m_VkSwapChain{VK_NULL_HANDLE};
        RHIDevice* m_Device;
        VkDevice m_VkDevice;
        VkSurfaceKHR m_VkSurface;
        const RHIVkSurface* m_Surface;
        Containers::Vector<RHIImageHandle> m_ImageHandles;
        Containers::Vector<RHIImageViewHandle> m_ImageViewHandles;

        Containers::Vector<RHISemaphoreHandle> m_ImageAvailableSemaphores;
        Containers::Vector<RHISemaphoreHandle> m_RenderFinishSemaphores;
        Containers::Vector<uint32_t> m_AcquiredImageIndices;
        VkQueue m_VkPresentQueue;
    };
}
