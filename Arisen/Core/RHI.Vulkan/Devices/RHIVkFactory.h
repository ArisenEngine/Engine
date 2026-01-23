#pragma once
#include "RHI/Devices/RHIFactory.h"
#include <vulkan/vulkan_core.h>

namespace ArisenEngine::RHI
{
    class RHIVkDevice;

    class RHIVkFactory final : public RHIFactory
    {
    public:
        explicit RHIVkFactory(RHIVkDevice* device);
        ~RHIVkFactory() noexcept override = default;

        GPUProgram* CreateGPUProgram() override;
        void ReleaseGPUProgram(GPUProgram* program) override;
        bool AttachProgramByteCode(GPUProgram* program, GPUProgramDesc&& desc) override;

        RHICommandBufferPool* CreateCommandBufferPool() override;
        void ReleaseCommandBufferPool(RHICommandBufferPool* pool) override;

        RHIRenderPassHandle CreateRenderPass() override;
        void ReleaseRenderPass(RHIRenderPassHandle renderPass) override;

        RHIFrameBufferHandle CreateFrameBuffer() override;
        void ReleaseFrameBuffer(RHIFrameBufferHandle frameBuffer) override;

        RHIBufferHandle CreateBuffer(BufferDescriptor&& desc, const std::string&& name = "Anonymous") override;
        void ReleaseBuffer(RHIBufferHandle bufferHandle) override;

        RHIImageHandle CreateImage(ImageDescriptor&& desc, const std::string&& name = "Anonymous") override;
        void ReleaseImage(RHIImageHandle imageHandle) override;

        RHIImageViewHandle CreateImageView(RHIImageHandle image, ImageViewDesc&& desc) override;
        void ReleaseImageView(RHIImageViewHandle imageView) override;

        RHISamplerHandle CreateSampler(RHISamplerDesc&& desc) override;
        void ReleaseSampler(RHISamplerHandle sampler) override;

        RHISemaphoreHandle CreateSemaphore() override;
        void ReleaseSemaphore(RHISemaphoreHandle semaphore) override;

        RHIFenceHandle CreateFence(bool signaled = false) override;
        void ReleaseFence(RHIFenceHandle fence) override;

    private:
        RHIVkDevice* m_Device;
    };
}
