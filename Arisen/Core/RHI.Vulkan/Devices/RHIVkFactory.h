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

        GPURenderPass* CreateRenderPass() override;
        void ReleaseRenderPass(GPURenderPass* renderPass) override;

        FrameBuffer* CreateFrameBuffer() override;
        void ReleaseFrameBuffer(FrameBuffer* frameBuffer) override;

        BufferHandle* CreateBuffer(const std::string&& name = "Anonymous") override;
        void ReleaseBuffer(BufferHandle* bufferHandle) override;

        ImageHandle* CreateImage(const std::string&& name = "Anonymous") override;
        void ReleaseImage(ImageHandle* imageHandle) override;

        RHISampler* CreateSampler(RHISamplerDesc&& desc) override;
        void ReleaseSampler(RHISampler* sampler) override;

    private:
        RHIVkDevice* m_Device;
    };
}
