#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Devices/RHIDevice.h"
#include "RHI/Utils/RHIDeferredDeletionQueue.h"
#include "RHI/Utils/RHIResourceRegistry.h"
#include "../Surfaces/RHIVkSurface.h"
#include "../CommandBuffer/RHIVkCommandBufferPool.h"
#include "../Program/RHIVkGPUPipelineManager.h"
#include "../Program/RHIVkGPUProgram.h"
#include "../Program/RHIVkDescriptorPool.h"
#include <mutex>
#include <memory>
#include <functional>
#include "RHI/Synchronization/FrameSyncTracker.h"

namespace ArisenEngine::RHI
{
    class RHIVkCommandBufferPool;
    class RHIVkDeferredDeletion;
    class IRHIQueue;
}

namespace ArisenEngine::RHI
{
    class RHIVkDevice final: public RHIDevice
    {
        
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDevice)
        ~RHIVkDevice() noexcept override;
        void* GetHandle() const override { return m_VkDevice; }
        RHIVkDevice(RHIInstance* instance, Surface* surface, VkQueue graphicQueue, VkQueue presentQueue, VkDevice device, VkPhysicalDeviceMemoryProperties memoryProperties);

        void DeviceWaitIdle() const override;
        void GraphicQueueWaitIdle() const override;
        UInt32 CreateGPUProgram() override;
        GPUProgram* GetGPUProgram(UInt32 programId) override;
        void DestroyGPUProgram(UInt32 programId) override;
        bool AttachProgramByteCode(UInt32 programId, GPUProgramDesc&& desc) override;

        UInt32 CreateCommandBufferPool() override;
        RHICommandBufferPool* GetCommandBufferPool(UInt32 id) override;

        GPURenderPass* GetRenderPass() override;
        void ReleaseRenderPass(GPURenderPass* renderPass) override;

        FrameBuffer* GetFrameBuffer() override;
        void ReleaseFrameBuffer(FrameBuffer* frameBuffer) override;

        std::shared_ptr<BufferHandle> GetBufferHandle(const std::string&& name = "Anonymous") override;
        void ReleaseBufferHandle(std::shared_ptr<BufferHandle> bufferHandle) override;
       
        std::shared_ptr<ImageHandle> GetImageHandle(const std::string && name = "Anonymous") override;
        void ReleaseImageHandle(std::shared_ptr<ImageHandle> imageHandle) override;

        GPUPipelineManager* GetGPUPipelineManager() const override
        {
            return m_GPUPipelineManager;
        }

        DescriptorPool* GetDescriptorPool() const override
        {
            return m_DescriptorPool;
        }

        RHISampler* CreateSampler(RHISamplerDesc&& desc) override;

        void Submit(RHICommandBuffer* commandBuffer, UInt32 frameIndex) override;
        IRHIQueue* GetQueue(RHIQueueType type) override;
        void DeferredDelete(RHIQueueType queue, RHIGpuTicket ticket, RHIDeferredDeleteItem item) override;
        UInt32 FindMemoryType(UInt32 typeFilter, UInt32 properties) override;

        void SetResolution(UInt32 width, UInt32 height) override;

        RHIFence* GetFrameFence(UInt32 frameIndex) override;
        void WaitFrameFence(UInt32 frameIndex) override;
        void ResetFrameFence(UInt32 frameIndex) override;
    private:

        friend class RHIVkInstance;
        RHIVkGPUPipelineManager* m_GPUPipelineManager;
        RHIVkDescriptorPool* m_DescriptorPool;
        VkQueue m_VkGraphicQueue;
        VkQueue m_VkPresentQueue;
        VkDevice m_VkDevice;
        VkPhysicalDeviceMemoryProperties m_VkPhysicalDeviceMemoryProperties;
        std::mutex m_SubmitMutex;
        
        Containers::Map<UInt32, std::unique_ptr<RHIVkGPUProgram>> m_GPUPrograms;
        Containers::Map<UInt32, std::unique_ptr<RHIVkCommandBufferPool>> m_CommandBufferPools;
        Containers::Vector<std::shared_ptr<BufferHandle>> m_BufferHandles;
        Containers::Vector<std::shared_ptr<ImageHandle>> m_ImageHandles;
        std::unique_ptr<IRHIDeferredDeletionQueue> m_DeferredDeletion;
        std::unique_ptr<RHIResourceRegistry> m_ResourceRegistry;
        std::atomic<UInt32> m_CurrentFrameIndex {0};
        std::unique_ptr<IRHIQueue> m_GraphicsQueue;
        std::unique_ptr<FrameSyncTracker> m_FrameSync;

    public:
        // Deferred destruction (GPU-safe): enqueue on producer threads, flush on the frame fence.
        void EnqueueDeferredDestroy(UInt32 frameIndex, RHIDeferredDeleteItem item);
        void EnqueueDeferredDestroy(UInt32 frameIndex, std::function<void()>&& fn);
        void FlushDeferredDestroys(UInt32 frameIndex);

        // Modern resource system entry point
        RHIResourceRegistry* GetResourceRegistry() const { return m_ResourceRegistry.get(); }
        UInt32 GetCurrentFrameIndex() const { return m_CurrentFrameIndex.load(std::memory_order_acquire); }
        RHIGpuTicket GetCompletedSubmitId() const override;
        void Update() override;
        void WaitQueueTicket(RHIGpuTicket ticket) override;

    };
}
