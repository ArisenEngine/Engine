#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Core/RHIDevice.h"
#include "RHI/Resources/RHIDeferredDeletionQueue.h"
#include "RHI/Resources/RHIResourceRegistry.h"
#include "Definitions/RHIVkCommon.h"
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
#include "RHI/Sync/RHISyncPrimitive.h"
#include "RHI/Core/RayTracingExtension.h"

namespace ArisenEngine::RHI
{
    class RHIVkCommandBufferPool;
    class RHIVkDeferredDeletion;
    class RHIQueue;
    class RHIVkBindlessManager;
    class RHIVkMemoryAllocator;
    class RHIVkTransferManager;
    struct RHITransferManagerConfig;
    struct RHIVkBufferPoolItem;
    struct RHIVkImagePoolItem;
    struct RHIVkImageViewPoolItem;
    struct RHIVkSamplerPoolItem;
    struct RHIVkRenderPassPoolItem;
    struct RHIVkFrameBufferPoolItem;
    struct RHIVkSemaphorePoolItem;
    struct RHIVkPipelinePoolItem;

    struct RHIVkAccelerationStructurePoolItem;
    struct RHIVkMemoryPoolPoolItem;
    struct RHIVkDescriptorPoolPoolItem;
    struct RHIVkExecutor;
}

namespace ArisenEngine::RHI
{
    class RHI_VULKAN_DLL RHIVkDevice final : public RHIDevice, public IRHIBackend, public RHISyncPrimitive, public RayTracingExtension
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
        friend struct RHIVkExecutor; // Needs access to cached function pointers
        friend class RHIVkTransferManager; // Needs queue family indices and pools

        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDevice)
        ~RHIVkDevice() noexcept override;
        void* GetHandle() const override { return m_VkDevice; }
        void* GetGraphicsQueue() override { return m_VkGraphicQueue; }
        void* GetComputeQueue() override { return m_VkComputeQueue; }
        void* GetPresentQueue() override { return m_VkPresentQueue; }
        RHIVkDevice(RHIInstance* instance, RHISurface* surface, VkQueue graphicQueue, VkQueue presentQueue,
                    VkQueue computeQueue, VkQueue transferQueue,
                    VkDevice device, VkPhysicalDeviceMemoryProperties memoryProperties, UInt32 graphicsFamilyIndex,
                    UInt32 computeFamilyIndex, UInt32 transferFamilyIndex, UInt32 presentFamilyIndex);

        void DeviceWaitIdle() const override;
        void GraphicQueueWaitIdle() const override;
        void ComputeQueueWaitIdle() const override;
        void TransferQueueWaitIdle() const override;
        void PresentQueueWaitIdle() const override;
        void QueueWaitIdle(RHIQueueType type) const override;

        RHIFactory* GetFactory() const override;
        RHISyncPrimitive* GetSync() const override { return const_cast<RHIVkDevice*>(this); }
        RayTracingExtension* GetRayTracing() const override { return const_cast<RHIVkDevice*>(this); }
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

        RHIDescriptorPoolHandle GetDescriptorPoolHandle() const override { return m_DescriptorPoolHandle; }

        RHIMemoryAllocator* GetMemoryAllocator() const override;


        // Descriptor Heap & Bindless Table
        RHIDescriptorHeap* CreateDescriptorHeap(EDescriptorHeapType type, UInt32 descriptorCount) override;
        RHIBindlessDescriptorTable* CreateBindlessDescriptorTable(RHIDescriptorHeap* heap) override;

        RHICommandBuffer* GetCommandBuffer(RHICommandBufferHandle handle) override
        {
            auto* item = m_CommandBufferPool->Get(handle);
            return item ? (RHICommandBuffer*)item->commandBuffer : nullptr;
        }

        RHIQueue* GetQueue(RHIQueueType type) override;
        RHIQueue* GetQueueByFamilyIndex(UInt32 familyIndex);
        RHICommandBufferPool* GetCommandBufferPool(RHICommandBufferPoolHandle handle) override;
        void DeferredDelete(const RHIDeletionDependencies& deps, RHIDeferredDeleteItem item) override;
        UInt32 FindMemoryType(UInt32 typeFilter, UInt32 properties) override;

        void SetResolution(UInt32 width, UInt32 height) override;


        UInt32 RegisterBindlessResource(RHIImageViewHandle image);
        UInt32 RegisterBindlessResource(RHIBufferHandle buffer);
        UInt32 RegisterBindlessResource(RHISamplerHandle sampler);

        // Debug & Naming
        void SetObjectName(ERHIObjectType type, UInt64 handle, const char* name) override;

    private:
        RHIVkBindlessManager* GetBindlessManager() const { return m_BindlessManager; }
        UInt32 GetGraphicsFamilyIndex() const { return m_GraphicsFamilyIndex; }
        UInt32 GetTransferFamilyIndex() const { return m_TransferFamilyIndex; }
        UInt32 GetComputeFamilyIndex() const { return m_ComputeFamilyIndex; }


    private:
        // Internal methods hidden from public interface
        void EnqueueDeferredDestroy(const RHIDeletionDependencies& deps, RHIDeferredDeleteItem item);
        void EnqueueDeferredDestroy(const RHIDeletionDependencies& deps, std::function<void()>&& fn);
        RHIResourceRegistry* GetResourceRegistry() const { return m_ResourceRegistry.get(); } // Made private

        friend class RHIVkInstance;
        RHIVkGPUPipelineManager* m_GPUPipelineManager;
        RHIVkDescriptorPool* m_DescriptorPool;
        RHIDescriptorPoolHandle m_DescriptorPoolHandle;
        RHIVkMemoryAllocator* m_MemoryAllocator;
        RHIVkBindlessManager* m_BindlessManager;
        RHIVkFactory* m_Factory;
        VkQueue m_VkGraphicQueue;
        VkQueue m_VkPresentQueue;
        VkQueue m_VkComputeQueue;
        VkQueue m_VkTransferQueue;
        VkDevice m_VkDevice;
        UInt32 m_GraphicsFamilyIndex;
        UInt32 m_ComputeFamilyIndex;
        UInt32 m_TransferFamilyIndex;
        UInt32 m_PresentFamilyIndex;
        VkPhysicalDeviceMemoryProperties m_VkPhysicalDeviceMemoryProperties;

        RHIResourceStats m_Stats;

        std::unique_ptr<IRHIDeferredDeletionQueue> m_DeferredDeletion;

        std::unique_ptr<RHIResourceRegistry> m_ResourceRegistry;
        std::unique_ptr<RHIQueue> m_GraphicsQueue;
        std::unique_ptr<RHIQueue> m_ComputeQueue;
        std::unique_ptr<RHIQueue> m_TransferQueue;
        std::unique_ptr<RHIQueue> m_PresentQueue;
        std::unique_ptr<FrameSyncTracker> m_FrameSync;
        std::unique_ptr<RHIVkTransferManager> m_TransferManager;

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


        std::unique_ptr<RHIResourcePool<RHIShaderProgramHandle, RHIVkGPUProgramPoolItem>> m_GPUProgramPool;
        std::unique_ptr<RHIResourcePool<RHICommandBufferPoolHandle, RHIVkCommandBufferPoolItem>>
        m_CommandBufferPoolPool;
        std::unique_ptr<RHIResourcePool<RHICommandBufferHandle, RHIVkCommandBufferItem>> m_CommandBufferPool;
        std::unique_ptr<RHIResourcePool<RHIAccelerationStructureHandle, RHIVkAccelerationStructurePoolItem>>
        m_AccelerationStructurePool;
        std::unique_ptr<RHIResourcePool<RHIDescriptorPoolHandle, RHIVkDescriptorPoolPoolItem>> m_DescriptorPoolPool;

        // TODO(Design-P2): public: 立即跟随 private: 段，导致内外部接口边界模糊。
        // 考虑将 Pool Accessor 统一为一个 internal 访问级别，通过内部接口类暴露。
    public:
        // Handle-based operations
    private:
        bool AllocBuffer(RHIBufferHandle handle, RHIBufferDescriptor&& desc) override;
        bool AllocBufferDeviceMemory(RHIBufferHandle handle) override;
        void ReleaseBuffer(RHIBufferHandle handle) override;
        void BufferMemoryCopy(RHIBufferHandle handle, const void* src, UInt64 size, UInt64 offset = 0);
        RHIGpuTicket BufferMemoryCopyAsync(RHIBufferHandle handle, const void* src, UInt64 size, UInt64 offset = 0);
        RHIGpuTicket FlushTransfers();
        void UpdateTransfers();
        void* MapBuffer(RHIBufferHandle handle);
        void UnmapBuffer(RHIBufferHandle handle);
        UInt64 GetBufferSize(RHIBufferHandle handle);
        UInt64 GetBufferOffset(RHIBufferHandle handle);
        UInt64 GetBufferRange(RHIBufferHandle handle);
        UInt64 GetBufferDeviceAddress(RHIBufferHandle handle);

        bool AllocImage(RHIImageHandle handle, RHIImageDescriptor&& desc) override;
        bool AllocImageDeviceMemory(RHIImageHandle handle) override;
        void ReleaseImage(RHIImageHandle handle) override;

        bool AllocMemoryPool(RHIMemoryPoolHandle handle, UInt64 size, UInt32 usageBits) override;
        void ReleaseMemoryPool(RHIMemoryPoolHandle handle) override;

        bool AllocBufferAliased(RHIBufferHandle handle, RHIBufferDescriptor&& desc, RHIMemoryPoolHandle pool,
                                UInt64 offset) override;
        bool AllocImageAliased(RHIImageHandle handle, RHIImageDescriptor&& desc, RHIMemoryPoolHandle pool,
                               UInt64 offset) override;

        bool AllocImageView(RHIImageViewHandle handle, RHIImageHandle imageHandle, RHIImageViewDesc&& desc) override;
        void ReleaseImageView(RHIImageViewHandle handle) override;
        void ReleaseAccelerationStructure(RHIAccelerationStructureHandle handle) override;
        bool AllocAccelerationStructure(RHIAccelerationStructureHandle handle, ERHIAccelerationStructureType type,
                                        UInt64 size, RHIBufferHandle buffer, UInt64 offset) override;
        RHIImageViewHandle FindImageViewForImage(RHIImageHandle imageHandle);
        EFormat GetImageViewFormat(RHIImageViewHandle handle);
        UInt32 GetImageViewWidth(RHIImageViewHandle handle);
        UInt32 GetImageViewHeight(RHIImageViewHandle handle);

        void ReleaseSampler(RHISamplerHandle handle) override;
        void ReleaseSemaphore(RHISemaphoreHandle handle) override;

        void ReleaseRenderPass(RHIRenderPassHandle handle) override;
        void ReleaseFrameBuffer(RHIFrameBufferHandle handle) override;
        void ReleasePipeline(RHIPipelineHandle handle) override;

        void ReleaseGPUProgram(RHIShaderProgramHandle handle);
        void ReleaseCommandBufferPool(RHICommandBufferPoolHandle handle);
        void ReleaseCommandBuffer(RHICommandBufferHandle handle);

        bool AllocFrameBuffer(RHIFrameBufferHandle handle, UInt32 frameIndex, RHIImageViewHandle viewHandle,
                              RHIRenderPassHandle renderPassHandle) override;
        // RHISyncPrimitive implementation

        void WaitSemaphoreValue(RHISemaphoreHandle handle, UInt64 value) override;
        void SignalSemaphoreValue(RHISemaphoreHandle handle, UInt64 value) override;
        UInt64 GetSemaphoreValue(RHISemaphoreHandle handle) override;

        // RayTracingExtension implementation
        void GetAccelerationStructureBuildSizes(const RHIAccelerationStructureBuildGeometryInfo& buildInfo,
                                                const UInt32* pMaxPrimitiveCounts,
                                                RHIAccelerationStructureBuildSizesInfo* pSizeInfo) override;
        UInt64 GetAccelerationStructureDeviceAddress(RHIAccelerationStructureHandle handle) override;
        void GetRayTracingShaderGroupHandles(RHIPipelineHandle pipeline, UInt32 firstGroup, UInt32 groupCount,
                                             UInt64 size, void* pData) override;

        void SetGPUProgramSpecializationConstant(RHIShaderProgramHandle handle, UInt32 constantID, UInt32 size,
                                                 const void* data);

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

        RHIResourcePool<RHIAccelerationStructureHandle, RHIVkAccelerationStructurePoolItem>*
        GetAccelerationStructurePool() const
        {
            return m_AccelerationStructurePool.get();
        }

        RHIResourcePool<RHIMemoryPoolHandle, RHIVkMemoryPoolPoolItem>* GetMemoryPoolPool() const
        {
            return m_MemoryPoolPool.get();
        }

        RHIResourcePool<RHIDescriptorPoolHandle, RHIVkDescriptorPoolPoolItem>* GetDescriptorPoolPool() const
        {
            return m_DescriptorPoolPool.get();
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

        // Descriptor Buffers
        PFN_vkGetDescriptorSetLayoutSizeEXT vkGetDescriptorSetLayoutSizeEXT = nullptr;
        PFN_vkGetDescriptorSetLayoutBindingOffsetEXT vkGetDescriptorSetLayoutBindingOffsetEXT = nullptr;
        PFN_vkGetDescriptorEXT vkGetDescriptorEXT = nullptr;
        PFN_vkCmdBindDescriptorBuffersEXT vkCmdBindDescriptorBuffersEXT = nullptr;
        PFN_vkCmdSetDescriptorBufferOffsetsEXT vkCmdSetDescriptorBufferOffsetsEXT = nullptr;

        // VRS
        PFN_vkCmdSetFragmentShadingRateKHR vkCmdSetFragmentShadingRateKHR = nullptr;

        // Dynamic State (Extended)
        PFN_vkCmdSetCullModeEXT vkCmdSetCullModeEXT = nullptr;
        PFN_vkCmdSetFrontFaceEXT vkCmdSetFrontFaceEXT = nullptr;
        PFN_vkCmdSetPrimitiveTopologyEXT vkCmdSetPrimitiveTopologyEXT = nullptr;
        PFN_vkCmdSetDepthTestEnableEXT vkCmdSetDepthTestEnableEXT = nullptr;
        PFN_vkCmdSetDepthWriteEnableEXT vkCmdSetDepthWriteEnableEXT = nullptr;
        PFN_vkCmdSetDepthCompareOpEXT vkCmdSetDepthCompareOpEXT = nullptr;
        PFN_vkCmdSetStencilTestEnableEXT vkCmdSetStencilTestEnableEXT = nullptr;
        PFN_vkCmdSetStencilOpEXT vkCmdSetStencilOpEXT = nullptr;

    private:
        // Internal low-level destruction (Vulkan/Memory only, via Registry)
        void FreeBufferInternal(RHIBufferHandle handle);
        void FreeImageInternal(RHIImageHandle handle);
        void FreeImageViewInternal(RHIImageViewHandle handle);
        void FreeSamplerInternal(RHISamplerHandle handle);
        void FreeSemaphoreInternal(RHISemaphoreHandle handle);

        void FreeRenderPassInternal(RHIRenderPassHandle handle);
        void FreeFrameBufferInternal(RHIFrameBufferHandle handle);
        void FreePipelineInternal(RHIPipelineHandle handle);
        void FreeAccelerationStructureInternal(RHIAccelerationStructureHandle handle);
        void FreeMemoryPoolInternal(RHIMemoryPoolHandle handle);
    };
}
