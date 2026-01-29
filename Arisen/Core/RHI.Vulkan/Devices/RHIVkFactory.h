#pragma once
#include "RHI/Core/RHIFactory.h"
#include <vulkan/vulkan_core.h>

namespace ArisenEngine::RHI
{
    class RHIVkDevice;

    class RHIVkFactory final : public RHIFactory
    {
    public:
        explicit RHIVkFactory(RHIVkDevice* device);
        ~RHIVkFactory() noexcept override = default;

        RHIShaderProgramHandle CreateGPUProgram() override;
        void ReleaseGPUProgram(RHIShaderProgramHandle handle) override;
        bool AttachProgramByteCode(RHIShaderProgramHandle handle, RHIShaderProgramDesc&& desc) override;

        RHICommandBufferPoolHandle CreateCommandBufferPool() override;
        void ReleaseCommandBufferPool(RHICommandBufferPoolHandle handle) override;

        RHIRenderPassHandle CreateRenderPass() override;
        void ReleaseRenderPass(RHIRenderPassHandle renderPass) override;

        RHIFrameBufferHandle CreateFrameBuffer() override;
        void ReleaseFrameBuffer(RHIFrameBufferHandle RHIFrameBuffer) override;

        RHIBufferHandle CreateBuffer(RHIBufferDescriptor&& desc, const String& name = "Anonymous") override;
        void ReleaseBuffer(RHIBufferHandle bufferHandle) override;

        RHIImageHandle CreateImage(RHIImageDescriptor&& desc, const String& name = "Anonymous") override;
        void ReleaseImage(RHIImageHandle imageHandle) override;

        RHIImageViewHandle CreateImageView(RHIImageHandle image, RHIImageViewDesc&& desc) override;
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
