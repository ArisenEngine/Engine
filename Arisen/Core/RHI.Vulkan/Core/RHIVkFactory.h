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

        RHICommandBufferPoolHandle CreateCommandBufferPool(RHIQueueType queueType = RHIQueueType::Graphics) override;
        void ReleaseCommandBufferPool(RHICommandBufferPoolHandle handle) override;

        RHIRenderPassHandle CreateRenderPass() override;
        void ReleaseRenderPass(RHIRenderPassHandle renderPass) override;

        RHIFrameBufferHandle CreateFrameBuffer() override;
        void ReleaseFrameBuffer(RHIFrameBufferHandle RHIFrameBuffer) override;

        RHIBufferHandle CreateBuffer(RHIBufferDescriptor&& desc, const String& name = "Anonymous") override;
        void ReleaseBuffer(RHIBufferHandle bufferHandle) override;

        RHIImageHandle CreateImage(RHIImageDescriptor&& desc, const String& name = "Anonymous") override;
        void ReleaseImage(RHIImageHandle imageHandle) override;

        RHIMemoryPoolHandle CreateMemoryPool(UInt64 size, UInt32 usageBits) override;
        void ReleaseMemoryPool(RHIMemoryPoolHandle handle) override;

        RHIBufferHandle CreateBufferAliased(RHIBufferDescriptor&& desc, RHIMemoryPoolHandle pool, UInt64 offset,
                                            const String& name = "Anonymous") override;
        RHIImageHandle CreateImageAliased(RHIImageDescriptor&& desc, RHIMemoryPoolHandle pool, UInt64 offset,
                                          const String& name = "Anonymous") override;

        RHIImageViewHandle CreateImageView(RHIImageHandle image, RHIImageViewDesc&& desc) override;
        void ReleaseImageView(RHIImageViewHandle imageView) override;

        RHISamplerHandle CreateSampler(RHISamplerDesc&& desc) override;
        void ReleaseSampler(RHISamplerHandle sampler) override;

        RHISemaphoreHandle CreateSemaphore() override;
        RHISemaphoreHandle CreateTimelineSemaphore(uint64_t initialValue = 0) override;
        void ReleaseSemaphore(RHISemaphoreHandle semaphore) override;



        RHIAccelerationStructureHandle CreateAccelerationStructure(const String& name = "Anonymous") override;
        void ReleaseAccelerationStructure(RHIAccelerationStructureHandle handle) override;

        // Resource management and query methods
        void BufferMemoryCopy(RHIBufferHandle handle, const void* src, UInt64 size, UInt64 offset = 0) override;
        RHIGpuTicket BufferMemoryCopyAsync(RHIBufferHandle handle, const void* src, UInt64 size, UInt64 offset = 0) override;
        RHIGpuTicket FlushTransfers() override;
        void UpdateTransfers() override;
        void* MapBuffer(RHIBufferHandle handle) override;
        void UnmapBuffer(RHIBufferHandle handle) override;
        UInt64 GetBufferSize(RHIBufferHandle handle) override;
        UInt64 GetBufferOffset(RHIBufferHandle handle) override;
        UInt64 GetBufferRange(RHIBufferHandle handle) override;
        UInt64 GetBufferDeviceAddress(RHIBufferHandle handle) override;
        RHIImageViewHandle FindImageViewForImage(RHIImageHandle imageHandle) override;
        EFormat GetImageViewFormat(RHIImageViewHandle handle) override;
        UInt32 GetImageViewWidth(RHIImageViewHandle handle) override;
        UInt32 GetImageViewHeight(RHIImageViewHandle handle) override;

        void SetGPUProgramSpecializationConstant(RHIShaderProgramHandle handle, UInt32 constantID, UInt32 size,
                                                 const void* data) override;

        UInt32 RegisterBindlessResource(RHIImageViewHandle image) override;
        UInt32 RegisterBindlessResource(RHIBufferHandle buffer) override;
        UInt32 RegisterBindlessResource(RHISamplerHandle sampler) override;

    private:
        RHIVkDevice* m_Device;
    };
}
