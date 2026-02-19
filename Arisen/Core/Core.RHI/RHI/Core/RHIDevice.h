#pragma once
#include "Base/FoundationMinimal.h"
#include "RHICommon.h"
#include "RHI/Enums/Memory/EMemoryPropertyFlagBits.h"
#include "RHI/Descriptors/RHIDescriptorPool.h"
#include "RHI/Definitions/DeviceLimits.h"
#include "RHI/Queues/RHIQueue.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Definitions/CoreRHICommon.h"
#include "RHI/Descriptors/RHIResourceDescriptors.h"
#include "RHIFactory.h"
#include "RayTracingExtension.h"
#include "../Sync/RHISyncPrimitive.h"
#include "RHI/Core/RHIInspector.h"
#include "RHI/Descriptors/RHIDescriptorHeap.h"
#include "RHI/Descriptors/RHIBindlessDescriptorTable.h"

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
    class RHIMemoryAllocator;

    enum class ERHIAccelerationStructureType;
    struct RHIAccelerationStructureBuildGeometryInfo;
    struct RHIAccelerationStructureBuildSizesInfo;

    /**
     * @brief Internal interface for RHI backends.
     * Contains methods that should not be exposed directly to the user.
     * 
     * TODO(CppSharp): IRHIBackend 不需要导出到 C#。确保 CppSharp 配置中跳过此类。
     * 它是纯内部后端接口，上层管线不应直接使用。
     */
    class IRHIBackend
    {
    public:
        virtual ~IRHIBackend() = default;

        // Internal Allocation / Release (Moved from RHIDevice)
        // Internal Allocation / Release (Moved from RHIDevice)
        virtual bool AllocBuffer(RHIBufferHandle handle, RHIBufferDescriptor&& desc) = 0;
        virtual bool AllocBufferDeviceMemory(RHIBufferHandle handle) = 0;
        virtual void ReleaseBuffer(RHIBufferHandle handle) = 0;

        virtual bool AllocImage(RHIImageHandle handle, RHIImageDescriptor&& desc) = 0;
        virtual bool AllocImageDeviceMemory(RHIImageHandle handle) = 0;
        virtual void ReleaseImage(RHIImageHandle handle) = 0;

        virtual bool AllocMemoryPool(RHIMemoryPoolHandle handle, UInt64 size, UInt32 usageBits) = 0;
        virtual void ReleaseMemoryPool(RHIMemoryPoolHandle handle) = 0;

        virtual bool AllocBufferAliased(RHIBufferHandle handle, RHIBufferDescriptor&& desc, RHIMemoryPoolHandle pool, UInt64 offset) = 0;
        virtual bool AllocImageAliased(RHIImageHandle handle, RHIImageDescriptor&& desc, RHIMemoryPoolHandle pool, UInt64 offset) = 0;

        virtual bool AllocImageView(RHIImageViewHandle handle, RHIImageHandle imageHandle, RHIImageViewDesc&& desc) = 0;
        virtual void ReleaseImageView(RHIImageViewHandle handle) = 0;

        virtual void ReleaseSampler(RHISamplerHandle handle) = 0;
        virtual void ReleaseSemaphore(RHISemaphoreHandle handle) = 0;
        virtual void ReleaseFence(RHIFenceHandle handle) = 0;
        virtual void ReleaseRenderPass(RHIRenderPassHandle handle) = 0;
        virtual void ReleaseFrameBuffer(RHIFrameBufferHandle handle) = 0;
        virtual void ReleasePipeline(RHIPipelineHandle handle) = 0;
        
        virtual void ReleaseAccelerationStructure(RHIAccelerationStructureHandle handle) = 0;
        virtual bool AllocAccelerationStructure(RHIAccelerationStructureHandle handle, ERHIAccelerationStructureType type, UInt64 size, RHIBufferHandle buffer, UInt64 offset) = 0;
        virtual bool AllocFrameBuffer(RHIFrameBufferHandle handle, UInt32 frameIndex, RHIImageViewHandle viewHandle, RHIRenderPassHandle renderPassHandle) = 0;
    };

    class RHI_DLL RHIDevice
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
        
        // CppSharp: exclude from binding — backend-only void* accessors.
        virtual void* GetHandle() const = 0;
        virtual void DeviceWaitIdle() const = 0;
        virtual void* GetGraphicsQueue() = 0;
        virtual void* GetComputeQueue() = 0;
        virtual void* GetPresentQueue() = 0;
        virtual void GraphicQueueWaitIdle() const = 0;

        virtual RHIFactory* GetFactory() const = 0;
        virtual RHISyncPrimitive* GetSync() const = 0;
        virtual RayTracingExtension* GetRayTracing() const = 0;

        virtual RHIPipelineCache* GetPipelineCache() const = 0;

        virtual RHIDescriptorPool* GetDescriptorPool() const = 0;
        virtual RHIDescriptorPoolHandle GetDescriptorPoolHandle() const = 0;

        virtual RHIMemoryAllocator* GetMemoryAllocator() const = 0;

        virtual RHIGpuTicket Submit(RHICommandBufferHandle commandBuffer, const struct RHISubmitDescriptor* descriptor = nullptr) = 0;

        // Descriptor Heap & Bindless Table
        virtual RHIDescriptorHeap* CreateDescriptorHeap(EDescriptorHeapType type, UInt32 descriptorCount) = 0;
        virtual RHIBindlessDescriptorTable* CreateBindlessDescriptorTable(RHIDescriptorHeap* heap) = 0;

        // Handle Resolution
        virtual RHICommandBuffer* GetCommandBuffer(RHICommandBufferHandle handle) = 0;

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


        virtual void SetResolution(UInt32 width, UInt32 height) = 0;
        virtual void SetObjectName(ERHIObjectType type, UInt64 handle, const char* name) = 0;

        virtual const RHIResourceStats& GetResourceStats() const = 0;

        const RHIDeviceLimits GetDeviceLimits() const

        {
            return m_DeviceLimits;
        }


    public:
    protected:
        virtual UInt32 FindMemoryType(UInt32 typeFilter, UInt32 properties) = 0;

        RHIInstance* m_Instance;
        RHISurface* m_Surface;
        RHIDeviceLimits m_DeviceLimits;
        RHIDevice(RHIInstance* instance, RHISurface* surface): m_Instance(instance), m_Surface(surface) {}
    private:
        
    };
}

