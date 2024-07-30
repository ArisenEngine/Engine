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
        RHIVkSwapChain(const VkDevice device, const RHIVkSurface* surface);
        ~RHIVkSwapChain() noexcept override;
        void* GetHandle() const override { return m_VkSwapChain; };
        void CreateSwapChainWithDesc(Surface* surface, SwapChainDescriptor desc) override;

        const RHISemaphore* GetImageAvailableSemaphore() const override;
        const RHISemaphore* GetRenderFinishSemaphore() const override;
        ImageHandle* AquireCurrentImage() override;
        void Present() override;
    protected:
        void RecreateSwapChainIfNeeded() override;
    private:
        
        VkSwapchainKHR m_VkSwapChain { VK_NULL_HANDLE };
        VkDevice m_VkDevice;
        VkSurfaceKHR m_VkSurface;
        const RHIVkSurface* m_Surface;
        Containers::Vector<std::unique_ptr<RHIVkImageHandle>> m_ImageHandles;

        RHIVkSemaphore* m_ImageAvailableSemaphore;
        RHIVkSemaphore* m_RenderFinishSemaphore;
        uint32_t m_ImageIndex;
        VkQueue m_VkPresentQueue;
    };
}
