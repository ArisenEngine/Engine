#pragma once
#include "../../Common/CommandHeaders.h"
#include "../RHICommon.h"
#include "../Handles/RHIHandle.h"
#include <string>
#include "../ResourceDescriptors.h"

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

        virtual RHIGPUProgramHandle CreateGPUProgram() = 0;
        virtual void ReleaseGPUProgram(RHIGPUProgramHandle handle) = 0;
        virtual bool AttachProgramByteCode(RHIGPUProgramHandle handle, GPUProgramDesc&& desc) = 0;

        virtual RHICommandBufferPoolHandle CreateCommandBufferPool() = 0;
        virtual void ReleaseCommandBufferPool(RHICommandBufferPoolHandle handle) = 0;

        virtual RHIRenderPassHandle CreateRenderPass() = 0;
        virtual void ReleaseRenderPass(RHIRenderPassHandle renderPass) = 0;

        virtual RHIFrameBufferHandle CreateFrameBuffer() = 0;
        virtual void ReleaseFrameBuffer(RHIFrameBufferHandle frameBuffer) = 0;

        virtual RHIBufferHandle CreateBuffer(BufferDescriptor&& desc, const std::string&& name = "Anonymous") = 0;
        virtual void ReleaseBuffer(RHIBufferHandle bufferHandle) = 0;

        virtual RHIImageHandle CreateImage(ImageDescriptor&& desc, const std::string&& name = "Anonymous") = 0;
        virtual void ReleaseImage(RHIImageHandle imageHandle) = 0;

        virtual RHIImageViewHandle CreateImageView(RHIImageHandle image, ImageViewDesc&& desc) = 0;
        virtual void ReleaseImageView(RHIImageViewHandle imageView) = 0;

        virtual RHISamplerHandle CreateSampler(RHISamplerDesc&& desc) = 0;
        virtual void ReleaseSampler(RHISamplerHandle sampler) = 0;

        virtual RHISemaphoreHandle CreateSemaphore() = 0;
        virtual void ReleaseSemaphore(RHISemaphoreHandle semaphore) = 0;

        virtual RHIFenceHandle CreateFence(bool signaled = false) = 0;
        virtual void ReleaseFence(RHIFenceHandle fence) = 0;
    };
}
