#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Devices/RHIDevice.h"
#include "RHI/Resources/RHIDeferredDeletionQueue.h"
#include "RHI/Resources/RHIResourceRegistry.h"
#include "../Surfaces/RHIVkSurface.h"
#include "../CommandBuffer/RHIVkCommandBufferPool.h"
#include "../Program/RHIVkGPUPipelineManager.h"
#include "../Program/RHIVkGPUProgram.h"
#include "../Program/RHIVkDescriptorPool.h"
#include "RHI/Resources/RHIResourcePool.h"
#include "../Handles/RHIVkResourcePools.h"
#include <mutex>
#include <memory>
#include <functional>
#include "../Program/RHIVkGPURenderPass.h"
#include "RHI/Synchronization/FrameSyncTracker.h"

namespace ArisenEngine::RHI
{
    class RHIVkCommandBufferPool;
    class RHIVkDeferredDeletion;
    class IRHIQueue;
    class RHIVkBindlessManager;
    class RHIVkMemoryAllocator;
    class RHIVkBindlessManager; // Forward decl
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
    class RHIVkDevice final : public RHIDevice
    {
    public:
        friend class RHIVkFactory;
        friend class RHIVkCommandBuffer; // Needs pool access
        friend class RHIVkDescriptorPool; // Needs pool access
        friend class RHIVkBindlessManager; // Needs pool access
        friend class RHIVkGPURenderPass; // Might need access
        friend class RHIVkFrameBuffer; // Needs pool access
        friend class RHIVkSwapChain; // Needs pool access
        friend class RHIVkGPUPipelineManager; // Needs pool access
        friend class RHIVkGPUPipelineStateObject; // Needs program pool access
        friend class RHINativeBridge; // Bridge for NativeExports
        friend class RHIVkCommandBufferPool; // Needs family index
        friend class RHIVkQueue; // Needs family index

        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDevice)
        ~RHIVkDevice() noexcept override;
        void* GetHandle() const override { return m_VkDevice; }
        RHIVkDevice(RHIInstance* instance, Surface* surface, VkQueue graphicQueue, VkQueue presentQueue,
                    VkDevice device, VkPhysicalDeviceMemoryProperties memoryProperties, UInt32 graphicsFamilyIndex);

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

        RHIGpuTicket Submit(RHICommandBuffer* commandBuffer, UInt32 frameIndex) override;
        IRHIQueue* GetQueue(RHIQueueType type) override;
        void DeferredDelete(RHIQueueType queue, RHIGpuTicket ticket, RHIDeferredDeleteItem item) override;
        UInt32 FindMemoryType(UInt32 typeFilter, UInt32 properties) override;

        void SetResolution(UInt32 width, UInt32 height) override;


        virtual UInt32 RegisterBindlessResource(RHIImageViewHandle image) override;
        UInt32 RegisterBindlessResource(RHIBufferHandle buffer) override;
        UInt32 RegisterBindlessResource(RHISamplerHandle sampler) override;

    private:
        RHIVkBindlessManager* GetBindlessManager() const { return m_BindlessManager; }
        UInt32 GetGraphicsFamilyIndex() const { return m_GraphicsFamilyIndex; }
        std::mutex& GetSubmitMutex() { return m_SubmitMutex; }

        UInt32 GetCurrentFrameIndex() const { return m_CurrentFrameIndex.load(std::memory_order_acquire); }
        RHIGpuTicket GetCompletedSubmitTicket() const override;
        void WaitQueueTicket(RHIGpuTicket ticket) override;

    private:
        // Internal methods hidden from public interface
        void EnqueueDeferredDestroy(RHIGpuTicket ticket, RHIDeferredDeleteItem item);
        void EnqueueDeferredDestroy(RHIGpuTicket ticket, std::function<void()>&& fn);
        RHIResourceRegistry* GetResourceRegistry() const { return m_ResourceRegistry.get(); } // Made private

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
        std::atomic<UInt32> m_CurrentFrameIndex{0};
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

        std::unique_ptr<RHIResourcePool<RHIGPUProgramHandle, RHIVkGPUProgramPoolItem>> m_GPUProgramPool;
        std::unique_ptr<RHIResourcePool<RHICommandBufferPoolHandle, RHIVkCommandBufferPoolItem>>
        m_CommandBufferPoolPool;
        std::unique_ptr<RHIResourcePool<RHICommandBufferHandle, RHIVkCommandBufferItem>> m_CommandBufferPool;

    public:
        // Handle-based operations
    private:
        bool AllocBuffer(RHIBufferHandle handle, BufferDescriptor&& desc) override;
        bool AllocBufferDeviceMemory(RHIBufferHandle handle, UInt32 memoryPropertiesBits) override;
        void ReleaseBuffer(RHIBufferHandle handle) override;
        void BufferMemoryCopy(RHIBufferHandle handle, const void* src, UInt32 offset) override;
        UInt64 GetBufferSize(RHIBufferHandle handle) override;
        UInt64 GetBufferOffset(RHIBufferHandle handle) override;
        UInt64 GetBufferRange(RHIBufferHandle handle) override;

        bool AllocImage(RHIImageHandle handle, ImageDescriptor&& desc) override;
        bool AllocImageDeviceMemory(RHIImageHandle handle, UInt32 memoryPropertiesBits) override;
        void ReleaseImage(RHIImageHandle handle) override;

        bool AllocImageView(RHIImageViewHandle handle, RHIImageHandle imageHandle, ImageViewDesc&& desc) override;
        void ReleaseImageView(RHIImageViewHandle handle) override;
        RHIImageViewHandle FindImageViewForImage(RHIImageHandle imageHandle) override;

        void ReleaseSampler(RHISamplerHandle handle) override;
        void ReleaseSemaphore(RHISemaphoreHandle handle) override;
        void ReleaseFence(RHIFenceHandle handle) override;
        void ReleaseRenderPass(RHIRenderPassHandle handle) override;
        void ReleaseFrameBuffer(RHIFrameBufferHandle handle) override;
        void ReleasePipeline(RHIPipelineHandle handle) override;

        void ReleaseGPUProgram(RHIGPUProgramHandle handle);
        void ReleaseCommandBufferPool(RHICommandBufferPoolHandle handle);
        void ReleaseCommandBuffer(RHICommandBufferHandle handle);

        bool AllocFrameBuffer(RHIFrameBufferHandle handle, UInt32 frameIndex, RHIImageViewHandle viewHandle,
                              RHIRenderPassHandle renderPassHandle) override;
        void WaitFence(RHIFenceHandle handle) override;
        void ResetFence(RHIFenceHandle handle) override;

    public:
        // Pool Accessors (Restricted)
    private:
        RHIResourcePool<RHIBufferHandle, RHIVkBufferPoolItem>* GetBufferPool() const { return m_BufferPool.get(); }
        RHIResourcePool<RHIImageHandle, RHIVkImagePoolItem>* GetImagePool() const { return m_ImagePool.get(); }

        RHIResourcePool<RHIImageViewHandle, RHIVkImageViewPoolItem>* GetImageViewPool() const
        {
            return m_ImageViewPool.get();
        }

        RHIResourcePool<RHISamplerHandle, RHIVkSamplerPoolItem>* GetSamplerPool() const { return m_SamplerPool.get(); }

        RHIResourcePool<RHIRenderPassHandle, RHIVkRenderPassPoolItem>* GetRenderPassPool() const
        {
            return m_RenderPassPool.get();
        }

        RHIResourcePool<RHIFrameBufferHandle, RHIVkFrameBufferPoolItem>* GetFrameBufferPool() const
        {
            return m_FrameBufferPool.get();
        }

        RHIResourcePool<RHISemaphoreHandle, RHIVkSemaphorePoolItem>* GetSemaphorePool() const
        {
            return m_SemaphorePool.get();
        }

        RHIResourcePool<RHIPipelineHandle, RHIVkPipelinePoolItem>* GetPipelinePool() const
        {
            return m_PipelinePool.get();
        }

        RHIResourcePool<RHIFenceHandle, RHIVkFencePoolItem>* GetFencePool() const { return m_FencePool.get(); }

        RHIResourcePool<RHIGPUProgramHandle, RHIVkGPUProgramPoolItem>* GetGPUProgramPool() const
        {
            return m_GPUProgramPool.get();
        }

        RHIResourcePool<RHICommandBufferPoolHandle, RHIVkCommandBufferPoolItem>* GetCommandBufferPoolPool() const
        {
            return m_CommandBufferPoolPool.get();
        }

        RHIResourcePool<RHICommandBufferHandle, RHIVkCommandBufferItem>* GetCommandBufferPool() const
        {
            return m_CommandBufferPool.get();
        }

    public:

    private:
        // Cached Function Pointers
        PFN_vkCmdBeginRenderingKHR vkCmdBeginRenderingKHR = nullptr;
        PFN_vkCmdEndRenderingKHR vkCmdEndRenderingKHR = nullptr;
        PFN_vkCmdPipelineBarrier2KHR vkCmdPipelineBarrier2KHR = nullptr;

    private:
        // Internal low-level destruction (Vulkan/Memory only, via Registry)
        void FreeBufferInternal(RHIBufferHandle handle);
        void FreeImageInternal(RHIImageHandle handle);
        void FreeImageViewInternal(RHIImageViewHandle handle);
        void FreeSamplerInternal(RHISamplerHandle handle);
        void FreeSemaphoreInternal(RHISemaphoreHandle handle);
        void FreeFenceInternal(RHIFenceHandle handle);
        void FreeRenderPassInternal(RHIRenderPassHandle handle);
        void FreeFrameBufferInternal(RHIFrameBufferHandle handle);
        void FreePipelineInternal(RHIPipelineHandle handle);
    };
}
