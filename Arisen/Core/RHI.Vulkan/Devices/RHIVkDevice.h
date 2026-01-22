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
#include "RHI/Utils/RHIResourcePool.h"
#include "../Handles/RHIVkResourcePools.h"
#include <mutex>
#include <memory>
#include <functional>
#include "RHI/Synchronization/FrameSyncTracker.h"

namespace ArisenEngine::RHI
{
    class RHIVkCommandBufferPool;
    class RHIVkDeferredDeletion;
    class IRHIQueue;
    class RHIVkBindlessManager;
    struct RHIVkBufferPoolItem;
    struct RHIVkImagePoolItem;
    struct RHIVkImageViewPoolItem;
    struct RHIVkSamplerPoolItem;
    struct RHIVkRenderPassPoolItem;
    struct RHIVkFrameBufferPoolItem;
    struct RHIVkSemaphorePoolItem;
    struct RHIVkPipelinePoolItem;
    struct RHIVkFencePoolItem;
}

namespace ArisenEngine::RHI
{
    class RHIVkFactory;
    class RHIVkMemoryAllocator;

    class RHIVkDevice final: public RHIDevice
    {
        
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDevice)
        ~RHIVkDevice() noexcept override;
        void* GetHandle() const override { return m_VkDevice; }
        RHIVkDevice(RHIInstance* instance, Surface* surface, VkQueue graphicQueue, VkQueue presentQueue, VkDevice device, VkPhysicalDeviceMemoryProperties memoryProperties, UInt32 graphicsFamilyIndex);

        void DeviceWaitIdle() const override;
        void GraphicQueueWaitIdle() const override;

        RHIFactory* GetFactory() const override;
        UInt32 GetMaxFramesInFlight() const override;

        GPUPipelineManager* GetGPUPipelineManager() const override
        {
            return m_GPUPipelineManager;
        }

        DescriptorPool* GetDescriptorPool() const override
        {
            return m_DescriptorPool;
        }

        RHIMemoryAllocator* GetMemoryAllocator() const override;

        void Submit(RHICommandBuffer* commandBuffer, UInt32 frameIndex) override;
        IRHIQueue* GetQueue(RHIQueueType type) override;
        void DeferredDelete(RHIQueueType queue, RHIGpuTicket ticket, RHIDeferredDeleteItem item) override;
        UInt32 FindMemoryType(UInt32 typeFilter, UInt32 properties) override;

        void SetResolution(UInt32 width, UInt32 height) override;

        RHIFenceHandle GetFrameFence(UInt32 frameIndex) override;
        void WaitFrameFence(UInt32 frameIndex) override;
        void ResetFrameFence(UInt32 frameIndex) override;

        virtual UInt32 RegisterBindlessResource(RHIImageViewHandle image) override;
        UInt32 RegisterBindlessResource(RHIBufferHandle buffer) override;
        UInt32 RegisterBindlessResource(RHISamplerHandle sampler) override;

        RHIVkBindlessManager* GetBindlessManager() const { return m_BindlessManager; }
        UInt32 GetGraphicsFamilyIndex() const { return m_GraphicsFamilyIndex; }
    private:

        friend class RHIVkInstance;
        RHIVkGPUPipelineManager* m_GPUPipelineManager;
        RHIVkDescriptorPool* m_DescriptorPool;
        RHIVkMemoryAllocator* m_MemoryAllocator;
        RHIVkBindlessManager* m_BindlessManager;
        RHIVkFactory* m_Factory;
        VkQueue m_VkGraphicQueue;
        VkQueue m_VkPresentQueue;
        VkDevice m_VkDevice;
        UInt32 m_GraphicsFamilyIndex;
        VkPhysicalDeviceMemoryProperties m_VkPhysicalDeviceMemoryProperties;
        std::mutex m_SubmitMutex;
        


        std::unique_ptr<IRHIDeferredDeletionQueue> m_DeferredDeletion;
        std::unique_ptr<RHIResourceRegistry> m_ResourceRegistry;
        std::atomic<UInt32> m_CurrentFrameIndex {0};
        std::unique_ptr<IRHIQueue> m_GraphicsQueue;
        std::unique_ptr<FrameSyncTracker> m_FrameSync;

        // Specialized resource pools for handle-based architecture
        std::unique_ptr<RHIResourcePool<RHIBufferHandle, RHIVkBufferPoolItem>> m_BufferPool;
        std::unique_ptr<RHIResourcePool<RHIImageHandle, RHIVkImagePoolItem>> m_ImagePool;
        std::unique_ptr<RHIResourcePool<RHIImageViewHandle, RHIVkImageViewPoolItem>> m_ImageViewPool;
        std::unique_ptr<RHIResourcePool<RHISamplerHandle, RHIVkSamplerPoolItem>> m_SamplerPool;
        std::unique_ptr<RHIResourcePool<RHIRenderPassHandle, RHIVkRenderPassPoolItem>> m_RenderPassPool;
        std::unique_ptr<RHIResourcePool<RHIFrameBufferHandle, RHIVkFrameBufferPoolItem>> m_FrameBufferPool;
        std::unique_ptr<RHIResourcePool<RHISemaphoreHandle, RHIVkSemaphorePoolItem>> m_SemaphorePool;
        std::unique_ptr<RHIResourcePool<RHIPipelineHandle, RHIVkPipelinePoolItem>> m_PipelinePool;
        std::unique_ptr<RHIResourcePool<RHIFenceHandle, RHIVkFencePoolItem>> m_FencePool;

    public:
        // Deferred destruction (GPU-safe): enqueue on producer threads, flush on the frame fence.
        void EnqueueDeferredDestroy(RHIGpuTicket ticket, RHIDeferredDeleteItem item);
        void EnqueueDeferredDestroy(RHIGpuTicket ticket, std::function<void()>&& fn);
        void FlushDeferredDestroys(RHIGpuTicket ticket);

        // Modern resource system entry point
        RHIResourceRegistry* GetResourceRegistry() const { return m_ResourceRegistry.get(); }
        UInt32 GetCurrentFrameIndex() const { return m_CurrentFrameIndex.load(std::memory_order_acquire); }
        RHIGpuTicket GetCompletedSubmitId() const override;
        void Update() override;
        void WaitQueueTicket(RHIGpuTicket ticket) override;

        // Handle-based operations
        bool AllocBuffer(RHIBufferHandle handle, BufferDescriptor&& desc);
        bool AllocBufferDeviceMemory(RHIBufferHandle handle, UInt32 memoryPropertiesBits);
        void FreeBuffer(RHIBufferHandle handle);
        void BufferMemoryCopy(RHIBufferHandle handle, const void* src, UInt32 offset);

        bool AllocImage(RHIImageHandle handle, ImageDescriptor&& desc);
        bool AllocImageDeviceMemory(RHIImageHandle handle, UInt32 memoryPropertiesBits);
        void FreeImage(RHIImageHandle handle);

        bool AllocImageView(RHIImageViewHandle handle, RHIImageHandle imageHandle, ImageViewDesc&& desc);
        void FreeImageView(RHIImageViewHandle handle);

        // Pool Accessors
        RHIResourcePool<RHIBufferHandle, RHIVkBufferPoolItem>* GetBufferPool() const { return m_BufferPool.get(); }
        RHIResourcePool<RHIImageHandle, RHIVkImagePoolItem>* GetImagePool() const { return m_ImagePool.get(); }
        RHIResourcePool<RHIImageViewHandle, RHIVkImageViewPoolItem>* GetImageViewPool() const { return m_ImageViewPool.get(); }
        RHIResourcePool<RHISamplerHandle, RHIVkSamplerPoolItem>* GetSamplerPool() const { return m_SamplerPool.get(); }
        RHIResourcePool<RHIRenderPassHandle, RHIVkRenderPassPoolItem>* GetRenderPassPool() const { return m_RenderPassPool.get(); }
        RHIResourcePool<RHIFrameBufferHandle, RHIVkFrameBufferPoolItem>* GetFrameBufferPool() const { return m_FrameBufferPool.get(); }
        RHIResourcePool<RHISemaphoreHandle, RHIVkSemaphorePoolItem>* GetSemaphorePool() const { return m_SemaphorePool.get(); }
        RHIResourcePool<RHIPipelineHandle, RHIVkPipelinePoolItem>* GetPipelinePool() const { return m_PipelinePool.get(); }
        RHIResourcePool<RHIFenceHandle, RHIVkFencePoolItem>* GetFencePool() const { return m_FencePool.get(); }

        // Cached Function Pointers
        PFN_vkCmdBeginRenderingKHR vkCmdBeginRenderingKHR = nullptr;
        PFN_vkCmdEndRenderingKHR vkCmdEndRenderingKHR = nullptr;
        PFN_vkCmdPipelineBarrier2KHR vkCmdPipelineBarrier2KHR = nullptr;

    };
}
