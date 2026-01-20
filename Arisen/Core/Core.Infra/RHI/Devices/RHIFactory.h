#pragma once
#include "../../Common/CommandHeaders.h"
#include "../RHICommon.h"
#include <string>

namespace ArisenEngine::RHI
{
    class BufferHandle;
    class ImageHandle;
    class RHISampler;
    struct RHISamplerDesc;
    class GPUProgram;
    struct GPUProgramDesc;
    class RHICommandBufferPool;
    class GPURenderPass;
    class FrameBuffer;

    class RHIFactory
    {
    public:
        virtual ~RHIFactory() noexcept = default;

        virtual GPUProgram* CreateGPUProgram() = 0;
        virtual void ReleaseGPUProgram(GPUProgram* program) = 0;
        virtual bool AttachProgramByteCode(GPUProgram* program, GPUProgramDesc&& desc) = 0;

        virtual RHICommandBufferPool* CreateCommandBufferPool() = 0;
        virtual void ReleaseCommandBufferPool(RHICommandBufferPool* pool) = 0;

        virtual GPURenderPass* CreateRenderPass() = 0;
        virtual void ReleaseRenderPass(GPURenderPass* renderPass) = 0;

        virtual FrameBuffer* CreateFrameBuffer() = 0;
        virtual void ReleaseFrameBuffer(FrameBuffer* frameBuffer) = 0;

        virtual BufferHandle* CreateBuffer(const std::string&& name = "Anonymous") = 0;
        virtual void ReleaseBuffer(BufferHandle* bufferHandle) = 0;

        virtual ImageHandle* CreateImage(const std::string&& name = "Anonymous") = 0;
        virtual void ReleaseImage(ImageHandle* imageHandle) = 0;

        virtual RHISampler* CreateSampler(RHISamplerDesc&& desc) = 0;
        virtual void ReleaseSampler(RHISampler* sampler) = 0;
    };
}
