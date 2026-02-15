#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Core/RHIDevice.h"
#include "RHI/Resources/RHIDeferredDeletionQueue.h"
#include "RHI/Resources/RHIResourceRegistry.h"
#include "Presentation/RHIVkSurface.h"
#include "Commands/RHIVkCommandBufferPool.h"
#include "Pipeline/RHIVkGPUPipelineManager.h"
#include "Pipeline/RHIVkGPUProgram.h"
#include "Descriptors/RHIVkDescriptorPool.h"
#include "RHI/Resources/RHIResourcePool.h"
#include "Handles/RHIVkResourcePools.h"
#include <mutex>
#include <memory>
#include <functional>
#include "RenderPass/RHIVkGPURenderPass.h"
#include "RHI/Sync/FrameSyncTracker.h"
#include "RHI/Core/RHIInspector.h"

namespace ArisenEngine::RHI

{
    class RHIVkCommandBufferPool;
    class RHIVkDeferredDeletion;
    class RHIQueue;
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
    struct RHIVkAccelerationStructurePoolItem;
    struct RHIVkMemoryPoolPoolItem;
}

namespace ArisenEngine::RHI
{
    class RHIVkDevice final : public RHIDevice
    {
    public:
        friend class RHIVkFactory;
        friend class RHIVkCommandBuffer; // Needs pool access
        friend class RHIVkGPUPipeline; // Needs device function pointers
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
        void* GetGraphicsQueue() override { return m_VkGraphicQueue; }
        void* GetComputeQueue() override { return m_VkComputeQueue; }
        void* GetPresentQueue() override { return m_VkPresentQueue; }
        RHIVkDevice(RHIInstance* instance, RHISurface* surface, VkQueue graphicQueue, VkQueue presentQueue, VkQueue computeQueue,
                    VkDevice device, VkPhysicalDeviceMemoryProperties memoryProperties, UInt32 graphicsFamilyIndex, UInt32 computeFamilyIndex);

        void DeviceWaitIdle() const override;
        void GraphicQueueWaitIdle() const override;

        RHIFactory* GetFactory() const override;
        UInt32 GetMaxFramesInFlight() const override;

        RHIPipelineCache* GetPipelineCache() const override
        {
            return m_GPUPipelineManager;
        }

        const RHIResourceStats& GetResourceStats() const override { return m_Stats; }

        RHIDescriptorPool* GetDescriptorPool() const override

        {
            return m_DescriptorPool;
        }

        RHIMemoryAllocator* GetMemoryAllocator() const override;

        RHIGpuTicket Submit(RHICommandBuffer* commandBuffer, const RHISubmitDescriptor* descriptor = nullptr) override;
        RHIQueue* GetQueue(RHIQueueType type) override;
        RHICommandBufferPool* GetCommandBufferPool(RHICommandBufferPoolHandle handle) override;
        void DeferredDelete(RHIQueueType queue, RHIGpuTicket ticket, RHIDeferredDeleteItem item) override;
        UInt32 FindMemoryType(UInt32 typeFilter, UInt32 properties) override;

        void SetResolution(UInt32 width, UInt32 height) override;


        virtual UInt32 RegisterBindlessResource(RHIImageViewHandle image) override;
        UInt32 RegisterBindlessResource(RHIBufferHandle buffer) override;
        UInt32 RegisterBindlessResource(RHISamplerHandle sampler) override;

        // Debug & Naming
        void SetObjectName(ERHIObjectType type, UInt64 handle, const char* name) override;

    private:
        RHIVkBindlessManager* GetBindlessManager() const { return m_BindlessManager; }
        UInt32 GetGraphicsFamilyIndex() const { return m_GraphicsFamilyIndex; }
        UInt32 GetComputeFamilyIndex() const { return m_ComputeFamilyIndex; }
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
        VkQueue m_VkComputeQueue;
        VkDevice m_VkDevice;
        UInt32 m_GraphicsFamilyIndex;
        UInt32 m_ComputeFamilyIndex;
        VkPhysicalDeviceMemoryProperties m_VkPhysicalDeviceMemoryProperties;
        std::mutex m_SubmitMutex;
        
        RHIResourceStats m_Stats;

        std::unique_ptr<IRHIDeferredDeletionQueue> m_DeferredDeletion;

        std::unique_ptr<RHIResourceRegistry> m_ResourceRegistry;
        std::atomic<UInt32> m_CurrentFrameIndex{0};
        std::unique_ptr<RHIQueue> m_GraphicsQueue;
        std::unique_ptr<RHIQueue> m_ComputeQueue;
        std::unique_ptr<FrameSyncTracker> m_FrameSync;

        // Specialized resource pools for handle-based architecture
        std::unique_ptr<RHIResourcePool<RHIBufferHandle, RHIVkBufferPoolItem>> m_BufferPool;
        std::unique_ptr<RHIResourcePool<RHIImageHandle, RHIVkImagePoolItem>> m_ImagePool;
        std::unique_ptr<RHIResourcePool<RHIMemoryPoolHandle, RHIVkMemoryPoolPoolItem>> m_MemoryPoolPool;
        std::unique_ptr<RHIResourcePool<RHIImageViewHandle, RHIVkImageViewPoolItem>> m_ImageViewPool;
        std::unique_ptr<RHIResourcePool<RHISamplerHandle, RHIVkSamplerPoolItem>> m_SamplerPool;
        std::unique_ptr<RHIResourcePool<RHIRenderPassHandle, RHIVkRenderPassPoolItem>> m_RenderPassPool;
        std::unique_ptr<RHIResourcePool<RHIFrameBufferHandle, RHIVkFrameBufferPoolItem>> m_FrameBufferPool;
        std::unique_ptr<RHIResourcePool<RHISemaphoreHandle, RHIVkSemaphorePoolItem>> m_SemaphorePool;
        std::unique_ptr<RHIResourcePool<RHIPipelineHandle, RHIVkPipelinePoolItem>> m_PipelinePool;
        std::unique_ptr<RHIResourcePool<RHIFenceHandle, RHIVkFencePoolItem>> m_FencePool;

        std::unique_ptr<RHIResourcePool<RHIShaderProgramHandle, RHIVkGPUProgramPoolItem>> m_GPUProgramPool;
        std::unique_ptr<RHIResourcePool<RHICommandBufferPoolHandle, RHIVkCommandBufferPoolItem>>
        m_CommandBufferPoolPool;
        std::unique_ptr<RHIResourcePool<RHICommandBufferHandle, RHIVkCommandBufferItem>> m_CommandBufferPool;
        std::unique_ptr<RHIResourcePool<RHIAccelerationStructureHandle, RHIVkAccelerationStructurePoolItem>> m_AccelerationStructurePool;

    public:
        // Handle-based operations
    private:
        bool AllocBuffer(RHIBufferHandle handle, RHIBufferDescriptor&& desc) override;
        bool AllocBufferDeviceMemory(RHIBufferHandle handle, UInt32 memoryPropertiesBits) override;
        void ReleaseBuffer(RHIBufferHandle handle) override;
        void BufferMemoryCopy(RHIBufferHandle handle, const void* src, UInt64 size, UInt64 offset = 0) override;
        void* MapBuffer(RHIBufferHandle handle) override;
        void UnmapBuffer(RHIBufferHandle handle) override;
        UInt64 GetBufferSize(RHIBufferHandle handle) override;
        UInt64 GetBufferOffset(RHIBufferHandle handle) override;
        UInt64 GetBufferRange(RHIBufferHandle handle) override;
        UInt64 GetBufferDeviceAddress(RHIBufferHandle handle) override;

        bool AllocImage(RHIImageHandle handle, RHIImageDescriptor&& desc) override;
        bool AllocImageDeviceMemory(RHIImageHandle handle, UInt32 memoryPropertiesBits) override;
        void ReleaseImage(RHIImageHandle handle) override;

        bool AllocMemoryPool(RHIMemoryPoolHandle handle, UInt64 size, UInt32 usageBits) override;
        void ReleaseMemoryPool(RHIMemoryPoolHandle handle) override;

        bool AllocBufferAliased(RHIBufferHandle handle, RHIBufferDescriptor&& desc, RHIMemoryPoolHandle pool, UInt64 offset) override;
        bool AllocImageAliased(RHIImageHandle handle, RHIImageDescriptor&& desc, RHIMemoryPoolHandle pool, UInt64 offset) override;

        bool AllocImageView(RHIImageViewHandle handle, RHIImageHandle imageHandle, RHIImageViewDesc&& desc) override;
        void ReleaseImageView(RHIImageViewHandle handle) override;
        RHIImageViewHandle FindImageViewForImage(RHIImageHandle imageHandle) override;

        void ReleaseSampler(RHISamplerHandle handle) override;
        void ReleaseSemaphore(RHISemaphoreHandle handle) override;
        void ReleaseFence(RHIFenceHandle handle) override;
        void ReleaseRenderPass(RHIRenderPassHandle handle) override;
        void ReleaseFrameBuffer(RHIFrameBufferHandle handle) override;
        void ReleasePipeline(RHIPipelineHandle handle) override;

        void ReleaseGPUProgram(RHIShaderProgramHandle handle);
        void ReleaseCommandBufferPool(RHICommandBufferPoolHandle handle);
        void ReleaseCommandBuffer(RHICommandBufferHandle handle);

        bool AllocFrameBuffer(RHIFrameBufferHandle handle, UInt32 frameIndex, RHIImageViewHandle viewHandle,
                              RHIRenderPassHandle renderPassHandle) override;
        void WaitFence(RHIFenceHandle handle) override;
        void ResetFence(RHIFenceHandle handle) override;

        // Acceleration Structure
        void GetAccelerationStructureBuildSizes(const RHIAccelerationStructureBuildGeometryInfo& buildInfo, const UInt32* pMaxPrimitiveCounts, RHIAccelerationStructureBuildSizesInfo* pSizeInfo) override;
        bool AllocAccelerationStructure(RHIAccelerationStructureHandle handle, ERHIAccelerationStructureType type, UInt64 size, RHIBufferHandle buffer, UInt64 offset) override;
        UInt64 GetAccelerationStructureDeviceAddress(RHIAccelerationStructureHandle handle) override;

        void GetRayTracingShaderGroupHandles(RHIPipelineHandle pipeline, UInt32 firstGroup, UInt32 groupCount, UInt64 size, void* pData) override;
        void ReleaseAccelerationStructure(RHIAccelerationStructureHandle handle) override;

        RHI::EFormat GetImageViewFormat(RHIImageViewHandle handle) override;
        UInt32 GetImageViewWidth(RHIImageViewHandle handle) override;
        UInt32 GetImageViewHeight(RHIImageViewHandle handle) override;

        void SetGPUProgramSpecializationConstant(RHIShaderProgramHandle handle, UInt32 constantID, UInt32 size, const void* data) override;

        void WaitSemaphoreValue(RHISemaphoreHandle handle, UInt64 value) override;
        void SignalSemaphoreValue(RHISemaphoreHandle handle, UInt64 value) override;
        UInt64 GetSemaphoreValue(RHISemaphoreHandle handle) override;

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

        RHIResourcePool<RHIShaderProgramHandle, RHIVkGPUProgramPoolItem>* GetGPUProgramPool() const
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

        RHIResourcePool<RHIAccelerationStructureHandle, RHIVkAccelerationStructurePoolItem>* GetAccelerationStructurePool() const
        {
            return m_AccelerationStructurePool.get();
        }

        RHIResourcePool<RHIMemoryPoolHandle, RHIVkMemoryPoolPoolItem>* GetMemoryPoolPool() const
        {
            return m_MemoryPoolPool.get();
        }

    public:

    private:
        // Cached Function Pointers
        PFN_vkCmdBeginRenderingKHR vkCmdBeginRenderingKHR = nullptr;
        PFN_vkCmdEndRenderingKHR vkCmdEndRenderingKHR = nullptr;
        PFN_vkCmdPipelineBarrier2KHR vkCmdPipelineBarrier2KHR = nullptr;
        PFN_vkCmdDrawMeshTasksEXT vkCmdDrawMeshTasksEXT = nullptr;

        // Debug Utils
        PFN_vkSetDebugUtilsObjectNameEXT vkSetDebugUtilsObjectNameEXT = nullptr;
        PFN_vkCmdBeginDebugUtilsLabelEXT vkCmdBeginDebugUtilsLabelEXT = nullptr;
        PFN_vkCmdEndDebugUtilsLabelEXT vkCmdEndDebugUtilsLabelEXT = nullptr;
        PFN_vkCmdInsertDebugUtilsLabelEXT vkCmdInsertDebugUtilsLabelEXT = nullptr;

        // Ray Tracing
        PFN_vkCreateAccelerationStructureKHR vkCreateAccelerationStructureKHR = nullptr;
        PFN_vkDestroyAccelerationStructureKHR vkDestroyAccelerationStructureKHR = nullptr;
        PFN_vkGetAccelerationStructureBuildSizesKHR vkGetAccelerationStructureBuildSizesKHR = nullptr;
        PFN_vkGetAccelerationStructureDeviceAddressKHR vkGetAccelerationStructureDeviceAddressKHR = nullptr;
        PFN_vkGetBufferDeviceAddressKHR vkGetBufferDeviceAddressKHR = nullptr;
        PFN_vkCmdBuildAccelerationStructuresKHR vkCmdBuildAccelerationStructuresKHR = nullptr;
        PFN_vkCmdTraceRaysKHR vkCmdTraceRaysKHR = nullptr;
        PFN_vkCreateRayTracingPipelinesKHR vkCreateRayTracingPipelinesKHR = nullptr;
        PFN_vkGetRayTracingShaderGroupHandlesKHR vkGetRayTracingShaderGroupHandlesKHR = nullptr;

        // VRS
        PFN_vkCmdSetFragmentShadingRateKHR vkCmdSetFragmentShadingRateKHR = nullptr;

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
        void FreeAccelerationStructureInternal(RHIAccelerationStructureHandle handle);
        void FreeMemoryPoolInternal(RHIMemoryPoolHandle handle);
    };
}




