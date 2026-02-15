#pragma once
#include "Base/FoundationMinimal.h"
#include "RHICommon.h"
#include "RHI/Enums/Memory/EMemoryPropertyFlagBits.h"
#include "RHI/Descriptors/RHIDescriptorPool.h"
#include "RHI/Definitions/DeviceLimits.h"
#include "RHI/Queues/RHIQueue.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Descriptors/RHIResourceDescriptors.h"
#include "RHI/Core/RHIInspector.h"

namespace ArisenEngine::RHI
{
    class RHISampler;
    struct RHISamplerDesc;
    class RHICommandBufferPool;
    struct RHIAccelerationStructureBuildGeometryInfo;
    struct RHIAccelerationStructureBuildSizesInfo;
    enum class ERHIAccelerationStructureType;
}

namespace ArisenEngine::RHI
{
    class RHICommandBuffer;
    class RHIShaderProgram;
}

namespace ArisenEngine::RHI
{
    class RHIPipelineCache;
    class RHIInstance;
    class RHISurface;
    class RHICommandBufferPool;
    class RHIRenderPass;
    class RHIFrameBuffer;
    class RHIFence;
    
    
    class RHIFactory;

    class RHIDevice
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIDevice)
        virtual ~RHIDevice() noexcept
        {
            m_Instance = nullptr;
            m_Surface = nullptr;
        }

        RHISurface* GetSurface() const { return m_Surface; }
        RHIInstance* GetInstance() const { return m_Instance; }
        virtual UInt32 GetMaxFramesInFlight() const = 0;
        
        virtual void* GetHandle() const = 0;
        virtual void DeviceWaitIdle() const = 0;
        virtual void* GetGraphicsQueue() = 0;
        virtual void* GetComputeQueue() = 0;
        virtual void* GetPresentQueue() = 0;
        virtual void GraphicQueueWaitIdle() const = 0;

        virtual RHIFactory* GetFactory() const = 0;

        virtual RHIPipelineCache* GetPipelineCache() const = 0;

        virtual RHIDescriptorPool* GetDescriptorPool() const = 0;

        virtual class RHIMemoryAllocator* GetMemoryAllocator() const = 0;

        virtual RHIGpuTicket Submit(RHICommandBuffer* commandBuffer, const struct RHISubmitDescriptor* descriptor = nullptr) = 0;

        // Optional per-frame update hook for GPU completion polling / automatic GC.
        // Default: no-op.
        // virtual void Update() {}  <-- REMOVED per user request (redundant)

        virtual RHIGpuTicket GetCompletedSubmitTicket() const { return 0; }
        virtual void WaitQueueTicket(RHIGpuTicket ticket) { (void)ticket; }

        // Optional: expose backend queues (graphics/compute/transfer/present).
        // Backends may return nullptr for unsupported queues.
        virtual RHIQueue* GetQueue(RHIQueueType type) { (void)type; return nullptr; }
        virtual RHICommandBufferPool* GetCommandBufferPool(RHICommandBufferPoolHandle handle) { (void)handle; return nullptr; }

        // Queue-scoped deferred delete helper. Backends can override to route to their deletion queue.
        // Default is immediate delete (safe for non-GPU objects).
        virtual void DeferredDelete(RHIQueueType queue, RHIGpuTicket ticket, RHIDeferredDeleteItem item)
        {
            (void)queue;
            (void)ticket;
            if (item.deleter && item.ptr) item.deleter(item.ptr);
        }

        virtual UInt32 FindMemoryType(UInt32 typeFilter, UInt32 properties) = 0;

        virtual void SetResolution(UInt32 width, UInt32 height) = 0;

        virtual const RHIResourceStats& GetResourceStats() const = 0;

        const RHIDeviceLimits GetDeviceLimits() const

        {
            return m_DeviceLimits;
        }

        // Bindless Resource Support
        virtual UInt32 RegisterBindlessResource(RHIImageViewHandle image) { (void)image; return 0xFFFFFFFF; }
        virtual UInt32 RegisterBindlessResource(RHIBufferHandle buffer) { (void)buffer; return 0xFFFFFFFF; }
        virtual UInt32 RegisterBindlessResource(RHISamplerHandle sampler) { (void)sampler; return 0xFFFFFFFF; }

        // Debug & Naming
        virtual void SetObjectName(ERHIObjectType type, UInt64 handle, const char* name) { (void)type; (void)handle; (void)name; }

    protected:
        friend class RHIFactory;

        // Handle-based operations
        virtual bool AllocBuffer(RHIBufferHandle handle, RHIBufferDescriptor&& desc) { return false; }
        virtual bool AllocBufferDeviceMemory(RHIBufferHandle handle, UInt32 memoryPropertiesBits) { return false; }
        virtual void ReleaseBuffer(RHIBufferHandle handle) {}
        
    public:
        virtual void BufferMemoryCopy(RHIBufferHandle handle, const void* src, UInt64 size, UInt64 offset = 0) {}
        virtual void* MapBuffer(RHIBufferHandle handle) { return nullptr; }
        virtual void UnmapBuffer(RHIBufferHandle handle) {}
        virtual UInt64 GetBufferSize(RHIBufferHandle handle) { return 0ULL; }
        virtual UInt64 GetBufferOffset(RHIBufferHandle handle) { return 0ULL; }
        virtual UInt64 GetBufferRange(RHIBufferHandle handle) { return 0ULL; }
        virtual UInt64 GetBufferDeviceAddress(RHIBufferHandle handle) { return 0ULL; }

    protected:
        virtual bool AllocImage(RHIImageHandle handle, RHIImageDescriptor&& desc) { return false; }
        virtual bool AllocImageDeviceMemory(RHIImageHandle handle, UInt32 memoryPropertiesBits) { return false; }
        virtual void ReleaseImage(RHIImageHandle handle) {}

        virtual bool AllocMemoryPool(RHIMemoryPoolHandle handle, UInt64 size, UInt32 usageBits) { return false; }
        virtual void ReleaseMemoryPool(RHIMemoryPoolHandle handle) {}

        virtual bool AllocBufferAliased(RHIBufferHandle handle, RHIBufferDescriptor&& desc, RHIMemoryPoolHandle pool, UInt64 offset) { return false; }
        virtual bool AllocImageAliased(RHIImageHandle handle, RHIImageDescriptor&& desc, RHIMemoryPoolHandle pool, UInt64 offset) { return false; }

        virtual bool AllocImageView(RHIImageViewHandle handle, RHIImageHandle imageHandle, RHIImageViewDesc&& desc) { return false; }
        virtual void ReleaseImageView(RHIImageViewHandle handle) = 0;

    public:
        virtual RHIImageViewHandle FindImageViewForImage(RHIImageHandle imageHandle) = 0;

    protected:
        virtual void ReleaseSampler(RHISamplerHandle handle) = 0;
        virtual void ReleaseSemaphore(RHISemaphoreHandle handle) = 0;
        virtual void ReleaseFence(RHIFenceHandle handle) = 0;
        virtual void ReleaseRenderPass(RHIRenderPassHandle handle) = 0;
        virtual void ReleaseFrameBuffer(RHIFrameBufferHandle handle) = 0;
        virtual void ReleasePipeline(RHIPipelineHandle handle) = 0;
    public:
        virtual void ReleaseAccelerationStructure(RHIAccelerationStructureHandle handle) = 0;
        virtual void GetAccelerationStructureBuildSizes(const RHIAccelerationStructureBuildGeometryInfo& buildInfo, const UInt32* pMaxPrimitiveCounts, RHIAccelerationStructureBuildSizesInfo* pSizeInfo) = 0;
        virtual bool AllocAccelerationStructure(RHIAccelerationStructureHandle handle, ERHIAccelerationStructureType type, UInt64 size, RHIBufferHandle buffer, UInt64 offset) = 0;
        virtual UInt64 GetAccelerationStructureDeviceAddress(RHIAccelerationStructureHandle handle) = 0;
        virtual void GetRayTracingShaderGroupHandles(RHIPipelineHandle pipeline, UInt32 firstGroup, UInt32 groupCount, UInt64 size, void* pData) = 0;

        virtual bool AllocFrameBuffer(RHIFrameBufferHandle handle, UInt32 frameIndex, RHIImageViewHandle viewHandle, RHIRenderPassHandle renderPassHandle) = 0;
        virtual void WaitFence(RHIFenceHandle handle) = 0;
        virtual void ResetFence(RHIFenceHandle handle) = 0;

        // Viewport & Scissor metadata (if needed, but usually set via Cmd)
        virtual RHI::EFormat GetImageViewFormat(RHIImageViewHandle handle) = 0;
        virtual UInt32 GetImageViewWidth(RHIImageViewHandle handle) = 0;
        virtual UInt32 GetImageViewHeight(RHIImageViewHandle handle) = 0;

        virtual void SetGPUProgramSpecializationConstant(RHIShaderProgramHandle handle, UInt32 constantID, UInt32 size, const void* data) = 0;

        virtual void WaitSemaphoreValue(RHISemaphoreHandle handle, UInt64 value) = 0;
        virtual void SignalSemaphoreValue(RHISemaphoreHandle handle, UInt64 value) = 0;
        virtual UInt64 GetSemaphoreValue(RHISemaphoreHandle handle) = 0;

    protected:
        
    protected:
        
        RHIInstance* m_Instance;
        RHISurface* m_Surface;
        RHIDeviceLimits m_DeviceLimits;
        RHIDevice(RHIInstance* instance, RHISurface* surface): m_Instance(instance), m_Surface(surface) {}
    private:
        
    };
}

