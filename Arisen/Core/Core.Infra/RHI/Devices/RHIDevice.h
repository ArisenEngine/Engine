#pragma once
#include "../../Common/CommandHeaders.h"
#include "../RHICommon.h"
#include "RHI/Enums/Memory/EMemoryPropertyFlagBits.h"
#include "RHI/Program/DescriptorPool.h"
#include "RHI/DeviceLimits.h"
#include "RHI/Queues/IRHIQueue.h"

namespace ArisenEngine::RHI
{
    class BufferHandle;
    class ImageHandle;
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
        
        virtual void* GetHandle() const = 0;
        virtual void DeviceWaitIdle() const = 0;
        virtual void GraphicQueueWaitIdle() const = 0;
        virtual UInt32 CreateGPUProgram() = 0;
        virtual GPUProgram* GetGPUProgram(UInt32 programId) = 0;
        virtual void DestroyGPUProgram(UInt32 programId) = 0;
        virtual bool AttachProgramByteCode(UInt32 programId, GPUProgramDesc&& desc) = 0;
        virtual UInt32 CreateCommandBufferPool() = 0;
        virtual RHICommandBufferPool* GetCommandBufferPool(UInt32 id) = 0;

        virtual std::shared_ptr<GPURenderPass> GetRenderPass() = 0;
        virtual void ReleaseRenderPass(std::shared_ptr<GPURenderPass> renderPass) = 0;

        virtual std::shared_ptr<FrameBuffer> GetFrameBuffer() = 0;
        virtual void ReleaseFrameBuffer(std::shared_ptr<FrameBuffer> frameBuffer) = 0;

        virtual std::shared_ptr<BufferHandle> GetBufferHandle(const std::string && name = "Anonymous") = 0;
        virtual void ReleaseBufferHandle(std::shared_ptr<BufferHandle> bufferHandle) = 0;

        virtual std::shared_ptr<ImageHandle> GetImageHandle(const std::string && name = "Anonymous") = 0;
        virtual void ReleaseImageHandle(std::shared_ptr<ImageHandle> imageHandle) = 0;

        virtual GPUPipelineManager* GetGPUPipelineManager() const = 0;

        virtual DescriptorPool* GetDescriptorPool() const = 0;

        virtual void Submit(RHICommandBuffer* commandBuffer, UInt32 frameIndex) = 0;

        // Optional per-frame update hook for GPU completion polling / automatic GC.
        // Default: no-op.
        virtual void Update() {}

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

        // Resource creation: keep creation on the device rather than a global factory.
        // NOTE: The returned pointer is an opaque handle for FFI; destruction must go through the exported Destroy/Release API.
        virtual RHISampler* CreateSampler(RHISamplerDesc&& desc) = 0;

        virtual UInt32 FindMemoryType(UInt32 typeFilter, UInt32 properties) = 0;

        virtual void SetResolution(UInt32 width, UInt32 height) = 0;

        // Optional per-frame fence management (centralized sync ownership).
        // Default: no-op / null, for backends that do not use per-frame fences.
        virtual RHIFence* GetFrameFence(UInt32 frameIndex) { (void)frameIndex; return nullptr; }
        virtual void WaitFrameFence(UInt32 frameIndex) { (void)frameIndex; }
        virtual void ResetFrameFence(UInt32 frameIndex) { (void)frameIndex; }

        const RHIDeviceLimits GetDeviceLimits() const
        {
            return m_DeviceLimits;
        }
        
    protected:
        
        RHIInstance* m_Instance;
        Surface* m_Surface;
        RHIDeviceLimits m_DeviceLimits;
        RHIDevice(RHIInstance* instance, Surface* surface): m_Instance(instance), m_Surface(surface) {}
    private:
        
    };
}
