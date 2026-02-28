#pragma once

#include "Base/FoundationMinimal.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine
{
    namespace RHI
    {
        /**
         * @brief Base structure for all RHI handles.
         * 32-bit index + 32-bit generation = 64-bit POD, blittable for C# interop.
         */
        template <typename T>
        struct RHIHandle
        {
            UInt32 index{0xFFFFFFFFu};
            UInt32 generation{0};

            bool IsValid() const { return index != 0xFFFFFFFFu; }

            bool operator==(const RHIHandle& other) const
            {
                return index == other.index && generation == other.generation;
            }

            bool operator!=(const RHIHandle& other) const { return !(*this == other); }

            static RHIHandle<T> Invalid() { return {0xFFFFFFFFu, 0}; }
        };

        // Specific tags to make handles type-safe in C++
        struct RHIBufferTag
        {
        };

        struct RHIImageTag
        {
        };

        struct RHISamplerTag
        {
        };

        struct RHIImageViewTag
        {
        };

        struct RHIShaderTag
        {
        };

        struct RHIPipelineTag
        {
        };

        struct RHICommandBufferTag
        {
        };

        struct RHIDescriptorSetTag
        {
        };

        struct RHIFenceTag
        {
        };

        struct RHISemaphoreTag
        {
        };

        struct RHIRenderPassTag
        {
        };

        struct RHIFrameBufferTag
        {
        };

        struct RHIShaderProgramTag
        {
        };

        struct RHICommandBufferPoolTag
        {
        };

        struct RHIAccelerationStructureTag
        {
        };

        struct RHIMemoryPoolTag
        {
        };

        struct RHIDescriptorPoolTag
        {
        };

        ARISEN_BIND_HANDLE(RHIBufferHandle)
        using RHIBufferHandle = RHIHandle<RHIBufferTag>;
        ARISEN_BIND_HANDLE(RHIImageHandle)
        using RHIImageHandle = RHIHandle<RHIImageTag>;
        ARISEN_BIND_HANDLE(RHIImageViewHandle)
        using RHIImageViewHandle = RHIHandle<RHIImageViewTag>;
        ARISEN_BIND_HANDLE(RHISamplerHandle)
        using RHISamplerHandle = RHIHandle<RHISamplerTag>;
        ARISEN_BIND_HANDLE(RHIShaderHandle)
        using RHIShaderHandle = RHIHandle<RHIShaderTag>;
        ARISEN_BIND_HANDLE(RHIPipelineHandle)
        using RHIPipelineHandle = RHIHandle<RHIPipelineTag>;
        ARISEN_BIND_HANDLE(RHICommandBufferHandle)
        using RHICommandBufferHandle = RHIHandle<RHICommandBufferTag>;
        ARISEN_BIND_HANDLE(RHIDescriptorSetHandle)
        using RHIDescriptorSetHandle = RHIHandle<RHIDescriptorSetTag>;
        ARISEN_BIND_HANDLE(RHIFenceHandle)
        using RHIFenceHandle = RHIHandle<RHIFenceTag>;
        ARISEN_BIND_HANDLE(RHISemaphoreHandle)
        using RHISemaphoreHandle = RHIHandle<RHISemaphoreTag>;
        ARISEN_BIND_HANDLE(RHIRenderPassHandle)
        using RHIRenderPassHandle = RHIHandle<RHIRenderPassTag>;
        ARISEN_BIND_HANDLE(RHIFrameBufferHandle)
        using RHIFrameBufferHandle = RHIHandle<RHIFrameBufferTag>;
        ARISEN_BIND_HANDLE(RHIShaderProgramHandle)
        using RHIShaderProgramHandle = RHIHandle<RHIShaderProgramTag>;
        ARISEN_BIND_HANDLE(RHICommandBufferPoolHandle)
        using RHICommandBufferPoolHandle = RHIHandle<RHICommandBufferPoolTag>;
        ARISEN_BIND_HANDLE(RHIAccelerationStructureHandle)
        using RHIAccelerationStructureHandle = RHIHandle<RHIAccelerationStructureTag>;
        ARISEN_BIND_HANDLE(RHIMemoryPoolHandle)
        using RHIMemoryPoolHandle = RHIHandle<RHIMemoryPoolTag>;
        ARISEN_BIND_HANDLE(RHIDescriptorPoolHandle)
        using RHIDescriptorPoolHandle = RHIHandle<RHIDescriptorPoolTag>;

        struct RHIResourceTag
        {
        };

        ARISEN_BIND_HANDLE(RHIResourceHandle)
        using RHIResourceHandle = RHIHandle<RHIResourceTag>;
    } // namespace RHI
} // namespace ArisenEngine
