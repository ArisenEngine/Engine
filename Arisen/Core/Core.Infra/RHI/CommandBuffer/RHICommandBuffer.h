#pragma once
#include "../../Common/CommandHeaders.h"
#include "../Program/GPURenderPass.h"
#include "../Surfaces/FrameBuffer.h"
#include "RHI/Devices/RHIDevice.h"
#include "RHI/Enums/Pipeline/ECommandBufferUsageFlagBits.h"
#include "RHI/Enums/Pipeline/EIndexType.h"
#include "RHI/Enums/Pipeline/EPipelineBindPoint.h"
#include "RHI/Enums/Pipeline/EPipelineStageFlag.h"
#include "RHI/Enums/Subpass/EDependencyFlag.h"
#include "RHI/Enums/Subpass/ESubpassContents.h"
#include "RHI/Handles/BufferHandle.h"
#include "RHI/Memory/BufferImageCopy.h"
#include "RHI/Synchronization/RHIBufferMemoryBarrier.h"
#include "RHI/Synchronization/RHIImageMemoryBarrier.h"
#include "RHI/Synchronization/RHIMemoryBarrier.h"

namespace ArisenEngine::RHI
{
    class RHIDescriptorSet;
    class RHIFence;
    class DescriptorPool;
}

namespace ArisenEngine::RHI
{
    class RHISemaphore;
}

namespace ArisenEngine::RHI
{
    class GPUPipeline;
    class RHIDevice;
    class RHICommandBufferPool;
    class FrameBuffer;
    class Viewport;
    class ImageHandle;
    class BufferHandle;
    class GPUPipelineManager;

    typedef struct RenderPassBeginDesc
    {
        GPURenderPass* renderPass;
        FrameBuffer* frameBuffer;
        ESubpassContents subpassContents;
    } RenderPassBeginDesc;

    struct RHIRenderingAttachmentInfo
    {
        ImageView* imageView;
        EImageLayout imageLayout;
        AttachmentLoadOp loadOp;
        AttachmentStoreOp storeOp;
        // clear value
        union
        {
            float float32[4];
            int32_t int32[4];
            uint32_t uint32[4];
        } clearValue;
    };

    struct RHIRenderingInfo
    {
        Containers::Vector<RHIRenderingAttachmentInfo> colorAttachments;
        std::optional<RHIRenderingAttachmentInfo> depthAttachment;
        std::optional<RHIRenderingAttachmentInfo> stencilAttachment;
        UInt32 layerCount;
        struct
        {
            SInt32 x;
            SInt32 y;
            UInt32 width;
            UInt32 height;
        } renderArea;
    };
    
    
    class RHICommandBuffer
    {
       
        
    public:
        enum class ECommandState : UInt8
        {
            ReadyForBegin,
            IsInsideBegin,
            IsInsideRenderPass,
            ReadyForSubmit,
            NotAllocated,
            NeedReset,
        };
        
        NO_COPY_NO_MOVE_NO_DEFAULT(RHICommandBuffer)

        RHICommandBuffer(RHIDevice* device, RHICommandBufferPool* pool):
        m_CommandBufferPool(pool), m_Device(device), m_State(ECommandState::NotAllocated)
        {
            
        }
        
        virtual ~RHICommandBuffer()
        {
            m_CommandBufferPool = nullptr;
            m_Device = nullptr;
        }
        
        RHICommandBufferPool* GetOwner() const
        {
            return m_CommandBufferPool;
        };

        void SetLastSubmitId(RHIGpuTicket id) { m_LastSubmitId = id; }
        RHIGpuTicket GetLastSubmitId() const { return m_LastSubmitId; }
        
        virtual void* GetHandle() const = 0;
        virtual void* GetHandlerPointer() = 0;

        // Command Interface
        virtual void BeginRenderPass(UInt32 frameIndex, RenderPassBeginDesc&& desc) = 0;
        virtual void EndRenderPass() = 0;

        virtual void BeginRendering(const RHIRenderingInfo& info) = 0;
        virtual void EndRendering() = 0;
        
        virtual void Begin(UInt32 frameIndex) = 0;
        virtual void Begin(UInt32 frameIndex, UInt32 commandBufferUsage) = 0;
        virtual void End() = 0;
        
        virtual void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height, Float32 minDepth, Float32 maxDepth) = 0;
        virtual void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height) = 0;
        virtual void SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width, UInt32 height) = 0;

        virtual void BindPipeline(UInt32 frameIndex, GPUPipeline* pipeline) = 0;
        virtual void Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance, UInt32 firstBinding) = 0;
        virtual void DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset, UInt32 firstInstance,  UInt32 firstBinding) = 0;
        virtual void BindVertexBuffers(BufferHandle* buffer, UInt64 offset) = 0;
        virtual void BindIndexBuffer(BufferHandle* indexBuffer, UInt64 offset, EIndexType type) = 0;
        
        virtual void WaitSemaphore(RHISemaphore* semaphore, EPipelineStageFlag stage) = 0;
        virtual void SignalSemaphore(RHISemaphore* semaphore) = 0;
        virtual void InjectFence(RHIFence* fence) = 0;

        virtual void CopyBuffer(BufferHandle const * src, UInt64 srcOffset, BufferHandle const * dst, UInt64 dstOffset, UInt64 size) = 0;
        
        virtual void BindDescriptorSets(UInt32 frameIndex, EPipelineBindPoint bindPoint,
    UInt32 firstSet, Containers::Vector<std::shared_ptr<RHIDescriptorSet>>& descriptorsets, UInt32 dynamicOffsetCount, const UInt32* pDynamicOffsets) = 0;
        virtual void WaitForFence(UInt32 frameIndex) = 0;

        virtual void CopyBufferToImage(BufferHandle const * srcBuffer, ImageHandle const * dst,
            EImageLayout dstImageLayout, Containers::Vector<BufferImageCopy>&& regions) = 0;
        virtual void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
    Containers::Vector<RHIMemoryBarrier>&& memoryBarriers,
    Containers::Vector<RHIImageMemoryBarrier> && imageMemoryBarriers,
    Containers::Vector<RHIBufferMemoryBarrier> && bufferMemoryBarriers) = 0;
        virtual void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
   Containers::Vector<RHIMemoryBarrier>&& memoryBarriers) = 0;
        virtual void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
   Containers::Vector<RHIImageMemoryBarrier> && imageMemoryBarriers) = 0;
        virtual void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
    Containers::Vector<RHIBufferMemoryBarrier> && bufferMemoryBarriers) = 0;

        // Optional hook: allow the backend to track per-submit resource usage (descriptor pools, etc).
        // Default: no-op.
        virtual void TrackDescriptorPoolUse(DescriptorPool* pool, UInt32 poolId)
        {
            (void)pool;
            (void)poolId;
        }
    public:

        const bool ReadyForSubmit() const;

    protected:
        friend RHICommandBufferPool;
        virtual void Reset() = 0;
        virtual void Release() = 0;
        virtual void ReadyForBegin(UInt32 frameIndex) = 0;
        virtual void DoBegin() = 0;
        RHICommandBufferPool* m_CommandBufferPool;
        RHIDevice* m_Device;
        ECommandState m_State;
        RHIGpuTicket m_LastSubmitId { 0 };
        
    };

    inline const bool RHICommandBuffer::ReadyForSubmit() const
    {
        return m_State == ECommandState::ReadyForSubmit;
    }
}
