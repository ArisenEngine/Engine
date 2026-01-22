#pragma once
#include "../../Common/CommandHeaders.h"
#include "../RHICommon.h"
#include "../Handles/RHIHandle.h"
#include <string>

namespace ArisenEngine::RHI
{
    class RHISampler;
    struct RHISamplerDesc;
    class GPUProgram;
    struct GPUProgramDesc;
    class RHICommandBufferPool;
    struct RenderPassDescriptor; // Assuming this exists or will be added
    struct FrameBufferDescriptor; // Assuming this exists or will be added

    class RHIFactory
    {
    public:
        virtual ~RHIFactory() noexcept = default;

        virtual GPUProgram* CreateGPUProgram() = 0;
        virtual void ReleaseGPUProgram(GPUProgram* program) = 0;
        virtual bool AttachProgramByteCode(GPUProgram* program, GPUProgramDesc&& desc) = 0;

        virtual RHICommandBufferPool* CreateCommandBufferPool() = 0;
        virtual void ReleaseCommandBufferPool(RHICommandBufferPool* pool) = 0;

        virtual RHIRenderPassHandle CreateRenderPass() = 0;
        virtual void ReleaseRenderPass(RHIRenderPassHandle renderPass) = 0;

        virtual RHIFrameBufferHandle CreateFrameBuffer() = 0;
        virtual void ReleaseFrameBuffer(RHIFrameBufferHandle frameBuffer) = 0;

        virtual RHIBufferHandle CreateBuffer(const std::string&& name = "Anonymous") = 0;
        virtual void ReleaseBuffer(RHIBufferHandle bufferHandle) = 0;

        virtual RHIImageHandle CreateImage(const std::string&& name = "Anonymous") = 0;
        virtual void ReleaseImage(RHIImageHandle imageHandle) = 0;

        virtual RHIImageViewHandle CreateImageView() = 0;
        virtual void ReleaseImageView(RHIImageViewHandle imageViewMap) = 0;

        virtual RHISamplerHandle CreateSampler(RHISamplerDesc&& desc) = 0;
        virtual void ReleaseSampler(RHISamplerHandle sampler) = 0;

        virtual RHISemaphoreHandle CreateSemaphore() = 0;
        virtual void ReleaseSemaphore(RHISemaphoreHandle semaphore) = 0;

        virtual RHIFenceHandle CreateFence(bool signaled = false) = 0;
        virtual void ReleaseFence(RHIFenceHandle fence) = 0;
    };
}
