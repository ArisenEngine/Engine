#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Enums/Pipeline/EShaderStage.h"

namespace ArisenEngine::RHI
{
    typedef struct RHIShaderProgramDesc
    {
        size_t codeSize;
        void* byteCode;
        const char* entry;
        const char* name;
        EShaderStage stage;
    } RHIShaderProgramDesc;

    enum class ERHIObjectType
    {
        Buffer,
        Image,
        ImageView,
        Sampler,
        RenderPass,
        FrameBuffer,
        Semaphore,
        Fence,
        GPUPipeline,
        GPUProgram,
        CommandBuffer,
        CommandBufferPool,
        DescriptorPool,
        DescriptorSet,
        Unknown
    };
    
}

