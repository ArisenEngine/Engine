#pragma once
#include "Base/FoundationMinimal.h"
#include "../Core/RHICommon.h"
#include "../Handles/RHIHandle.h"
#include <string>
#include "../Descriptors/RHIResourceDescriptors.h"
#include "../Queues/RHIQueueType.h"

namespace ArisenEngine::RHI
{
    class RHISampler;
    struct RHISamplerDesc;
    class RHIShaderProgram;
    struct RHIShaderProgramDesc;
    class RHICommandBufferPool;
    struct RenderPassDescriptor; // Assuming this exists or will be added
    struct RHIFrameBufferDescriptor; // Assuming this exists or will be added

    class RHIFactory
    {
    public:
        virtual ~RHIFactory() noexcept = default;

        virtual RHIShaderProgramHandle CreateGPUProgram() = 0;
        virtual void ReleaseGPUProgram(RHIShaderProgramHandle handle) = 0;
        virtual bool AttachProgramByteCode(RHIShaderProgramHandle handle, RHIShaderProgramDesc&& desc) = 0;

        virtual RHICommandBufferPoolHandle CreateCommandBufferPool(RHIQueueType queueType = RHIQueueType::Graphics) = 0;
        virtual void ReleaseCommandBufferPool(RHICommandBufferPoolHandle handle) = 0;

        virtual RHIRenderPassHandle CreateRenderPass() = 0;
        virtual void ReleaseRenderPass(RHIRenderPassHandle renderPass) = 0;

        virtual RHIFrameBufferHandle CreateFrameBuffer() = 0;
        virtual void ReleaseFrameBuffer(RHIFrameBufferHandle frameBuffer) = 0;

        virtual RHIBufferHandle CreateBuffer(RHIBufferDescriptor&& desc, const String& name = "Anonymous") = 0;
        virtual void ReleaseBuffer(RHIBufferHandle bufferHandle) = 0;

        virtual RHIImageHandle CreateImage(RHIImageDescriptor&& desc, const String& name = "Anonymous") = 0;
        virtual void ReleaseImage(RHIImageHandle imageHandle) = 0;

        virtual RHIImageViewHandle CreateImageView(RHIImageHandle image, RHIImageViewDesc&& desc) = 0;
        virtual void ReleaseImageView(RHIImageViewHandle imageView) = 0;

        virtual RHISamplerHandle CreateSampler(RHISamplerDesc&& desc) = 0;
        virtual void ReleaseSampler(RHISamplerHandle sampler) = 0;

        virtual RHISemaphoreHandle CreateSemaphore() = 0;
        virtual void ReleaseSemaphore(RHISemaphoreHandle semaphore) = 0;

        virtual RHIFenceHandle CreateFence(bool signaled = false) = 0;
        virtual void ReleaseFence(RHIFenceHandle fence) = 0;

        virtual RHIAccelerationStructureHandle CreateAccelerationStructure(const String& name = "Anonymous") = 0;
        virtual void ReleaseAccelerationStructure(RHIAccelerationStructureHandle handle) = 0;
    };
}

