#pragma once
#include "../../Common/CommandHeaders.h"
#include "../RHICommon.h"
#include "RHI/Enums/Memory/EMemoryPropertyFlagBits.h"
#include "RHI/Program/DescriptorPool.h"
#include "RHI/DeviceLimits.h"
#include "RHI/Queues/IRHIQueue.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/ResourceDescriptors.h"

namespace ArisenEngine::RHI
{
    class RHISampler;
    struct RHISamplerDesc;
}

namespace ArisenEngine::RHI
{
    class RHICommandBuffer;
    class GPUProgram;
}

namespace ArisenEngine::RHI
{
    class GPUPipelineManager;
    class RHIInstance;
    class Surface;
    class RHICommandBufferPool;
    class GPURenderPass;
    class FrameBuffer;
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

        Surface* GetSurface() const { return m_Surface; }
        RHIInstance* GetInstance() const { return m_Instance; }
        virtual UInt32 GetMaxFramesInFlight() const = 0;
        
        virtual void* GetHandle() const = 0;
        virtual void DeviceWaitIdle() const = 0;
        virtual void GraphicQueueWaitIdle() const = 0;

        virtual RHIFactory* GetFactory() const = 0;

        virtual GPUPipelineManager* GetGPUPipelineManager() const = 0;

        virtual DescriptorPool* GetDescriptorPool() const = 0;

        virtual class RHIMemoryAllocator* GetMemoryAllocator() const = 0;

        virtual void Submit(RHICommandBuffer* commandBuffer, UInt32 frameIndex) = 0;

        // Optional per-frame update hook for GPU completion polling / automatic GC.
        // Default: no-op.
        virtual void Update() {}

        virtual RHIGpuTicket GetCompletedSubmitId() const { return 0; }
        virtual void WaitQueueTicket(RHIGpuTicket ticket) { (void)ticket; }

        // Optional: expose backend queues (graphics/compute/transfer/present).
        // Backends may return nullptr for unsupported queues.
        virtual IRHIQueue* GetQueue(RHIQueueType type) { (void)type; return nullptr; }

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

        // Optional per-frame fence management (centralized sync ownership).
        // Default: no-op / null, for backends that do not use per-frame fences.
        virtual RHIFenceHandle GetFrameFence(UInt32 frameIndex) { (void)frameIndex; return RHIFenceHandle::Invalid(); }
        virtual void WaitFrameFence(UInt32 frameIndex) { (void)frameIndex; }
        virtual void ResetFrameFence(UInt32 frameIndex) { (void)frameIndex; }

        const RHIDeviceLimits GetDeviceLimits() const
        {
            return m_DeviceLimits;
        }

        // Bindless Resource Support
        virtual UInt32 RegisterBindlessResource(RHIImageViewHandle image) { (void)image; return 0xFFFFFFFF; }
        virtual UInt32 RegisterBindlessResource(RHIBufferHandle buffer) { (void)buffer; return 0xFFFFFFFF; }
        virtual UInt32 RegisterBindlessResource(RHISamplerHandle sampler) { (void)sampler; return 0xFFFFFFFF; }

        // Handle-based operations
        virtual bool AllocBuffer(RHIBufferHandle handle, BufferDescriptor&& desc) { return false; }
        virtual bool AllocBufferDeviceMemory(RHIBufferHandle handle, UInt32 memoryPropertiesBits) { return false; }
        virtual void ReleaseBuffer(RHIBufferHandle handle) {}
        virtual void BufferMemoryCopy(RHIBufferHandle handle, const void* src, UInt32 offset) {}
        virtual UInt64 GetBufferSize(RHIBufferHandle handle) { return 0ULL; }
        virtual UInt64 GetBufferOffset(RHIBufferHandle handle) { return 0ULL; }
        virtual UInt64 GetBufferRange(RHIBufferHandle handle) { return 0ULL; }

        virtual bool AllocImage(RHIImageHandle handle, ImageDescriptor&& desc) { return false; }
        virtual bool AllocImageDeviceMemory(RHIImageHandle handle, UInt32 memoryPropertiesBits) { return false; }
        virtual void ReleaseImage(RHIImageHandle handle) {}

        virtual bool AllocImageView(RHIImageViewHandle handle, RHIImageHandle imageHandle, ImageViewDesc&& desc) { return false; }
        virtual void ReleaseImageView(RHIImageViewHandle handle) = 0;
        virtual RHIImageViewHandle FindImageViewForImage(RHIImageHandle imageHandle) = 0;

        virtual void ReleaseSampler(RHISamplerHandle handle) = 0;
        virtual void ReleaseSemaphore(RHISemaphoreHandle handle) = 0;
        virtual void ReleaseFence(RHIFenceHandle handle) = 0;
        virtual void ReleaseRenderPass(RHIRenderPassHandle handle) = 0;
        virtual void ReleaseFrameBuffer(RHIFrameBufferHandle handle) = 0;
        virtual void ReleasePipeline(RHIPipelineHandle handle) = 0;
        
        virtual bool AllocFrameBuffer(RHIFrameBufferHandle handle, UInt32 frameIndex, RHIImageViewHandle viewHandle, RHIRenderPassHandle renderPassHandle) = 0;
        
    protected:
        
        RHIInstance* m_Instance;
        Surface* m_Surface;
        RHIDeviceLimits m_DeviceLimits;
        RHIDevice(RHIInstance* instance, Surface* surface): m_Instance(instance), m_Surface(surface) {}
    private:
        
    };
}
