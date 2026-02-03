#pragma once
#include "Base/FoundationMinimal.h"
#include "../RenderPass/RHIRenderPass.h"
#include "../RenderPass/RHIFrameBuffer.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Enums/Pipeline/ECommandBufferUsageFlagBits.h"
#include "RHI/Enums/Pipeline/EIndexType.h"
#include "RHI/Enums/Pipeline/EPipelineBindPoint.h"
#include "RHI/Enums/Pipeline/EPipelineStageFlag.h"
#include "RHI/Enums/Subpass/EDependencyFlag.h"
#include "RHI/Enums/Subpass/ESubpassContents.h"
#include "RHI/Commands/RHIBufferImageCopy.h"
#include "RHI/Sync/RHIBufferMemoryBarrier.h"
#include "RHI/Sync/RHIImageMemoryBarrier.h"
#include "RHI/Sync/RHIMemoryBarrier.h"
#include "../Handles/RHIHandle.h"

namespace ArisenEngine::RHI
{
    class RHIDescriptorSet;
    class RHIFence;
    class RHIDescriptorPool;
}

namespace ArisenEngine::RHI
{
    class RHISemaphore;
}

namespace ArisenEngine::RHI
{
    class RHIPipeline;
    class RHIDevice;
    class RHICommandBufferPool;
    class RHIFrameBuffer;
    class RHIViewport;
    class RHIPipelineCache;

    struct RHIClearValue
    {
        union
        {
            float color[4];
            struct
            {
                float depth;
                uint32_t stencil;
            } depthStencil;
        };
    };

    typedef struct RenderPassBeginDesc
    {
        RHIRenderPassHandle renderPass;
        RHIFrameBufferHandle frameBuffer;
        ESubpassContents subpassContents;
        UInt32 clearValueCount;
        const RHIClearValue* pClearValues;
    } RenderPassBeginDesc;

    struct RHIRenderingAttachmentInfo
    {
        RHIImageViewHandle imageView;
        EImageLayout imageLayout;
        EAttachmentLoadOp loadOp;
        EAttachmentStoreOp storeOp;
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
        const RHIRenderingAttachmentInfo* pColorAttachments;
        UInt32 colorAttachmentCount;
        const RHIRenderingAttachmentInfo* pResolveAttachments;
        const RHIRenderingAttachmentInfo* pDepthAttachment;
        const RHIRenderingAttachmentInfo* pStencilAttachment;
        UInt32 layerCount;
        struct
        {
            SInt32 x;
            SInt32 y;
            UInt32 width;
            UInt32 height;
        } RHIRenderArea;
    };
    
    
    class RHICommandBuffer
    {
       
        
    public:
        enum class ECommandBufferState : UInt8
        {
            Initial,      // Allocated but not recording.
            Recording,    // Between Begin and End.
            RecordingPass,// Between BeginRenderPass/BeginRendering and End.
            Executable,   // Ready to be submitted.
        };
        
        NO_COPY_NO_MOVE_NO_DEFAULT(RHICommandBuffer)

        RHICommandBuffer(RHIDevice* device, RHICommandBufferPool* pool):
        m_CommandBufferPool(pool), m_Device(device), m_State(ECommandBufferState::Initial)
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

    protected:
        void SetLatestSubmitTicket(RHIGpuTicket id) { m_LatestSubmitTicket = id; }
        RHIGpuTicket GetLatestSubmitTicket() const { return m_LatestSubmitTicket; }
        
    public:
        const bool ReadyForSubmit() const;

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

        virtual void BindPipeline(UInt32 frameIndex, RHIPipelineHandle pipeline) = 0;
        virtual void Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance, UInt32 firstBinding) = 0;
        virtual void DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset, UInt32 firstInstance,  UInt32 firstBinding) = 0;
        virtual void DrawMeshTasks(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ) = 0;
        virtual void Dispatch(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ) = 0;
        virtual void BindVertexBuffers(RHIBufferHandle buffer, UInt64 offset) = 0;
        virtual void BindIndexBuffer(RHIBufferHandle indexBuffer, UInt64 offset, EIndexType type) = 0;
        
        virtual void WaitSemaphore(RHISemaphoreHandle semaphore, EPipelineStageFlag stage) = 0;
        virtual void SignalSemaphore(RHISemaphoreHandle semaphore) = 0;

        virtual void CopyBuffer(RHIBufferHandle src, UInt64 srcOffset, RHIBufferHandle dst, UInt64 dstOffset, UInt64 size) = 0;
        
        virtual void BindDescriptorSets(UInt32 frameIndex, EPipelineBindPoint bindPoint,
    UInt32 firstSet, Containers::Vector<std::shared_ptr<RHIDescriptorSet>>& descriptorsets, UInt32 dynamicOffsetCount, const UInt32* pDynamicOffsets) = 0;

        virtual void CopyBufferToImage(RHIBufferHandle srcBuffer, RHIImageHandle dst,
            EImageLayout dstImageLayout, Containers::Vector<RHIBufferImageCopy>&& regions) = 0;
        virtual void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
            const RHIMemoryBarrier* pMemoryBarriers, UInt32 memoryBarrierCount,
            const RHIImageMemoryBarrier* pImageMemoryBarriers, UInt32 imageMemoryBarrierCount,
            const RHIBufferMemoryBarrier* pBufferMemoryBarriers, UInt32 bufferMemoryBarrierCount) = 0;
        virtual void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
            const RHIMemoryBarrier* pMemoryBarriers, UInt32 memoryBarrierCount) = 0;
        virtual void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
            const RHIImageMemoryBarrier* pImageMemoryBarriers, UInt32 imageMemoryBarrierCount) = 0;
        virtual void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
            const RHIBufferMemoryBarrier* pBufferMemoryBarriers, UInt32 bufferMemoryBarrierCount) = 0;

        virtual void GenerateMipmaps(RHIImageHandle image) = 0;

        // Vector-based overloads (delegating to pointer-based ones)
        void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
            Containers::Vector<RHIMemoryBarrier>&& memoryBarriers,
            Containers::Vector<RHIImageMemoryBarrier>&& imageMemoryBarriers,
            Containers::Vector<RHIBufferMemoryBarrier>&& bufferMemoryBarriers)
        {
            PipelineBarrier(srcStage, dstStage, dependency,
                memoryBarriers.data(), static_cast<UInt32>(memoryBarriers.size()),
                imageMemoryBarriers.data(), static_cast<UInt32>(imageMemoryBarriers.size()),
                bufferMemoryBarriers.data(), static_cast<UInt32>(bufferMemoryBarriers.size()));
        }

        void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
            Containers::Vector<RHIMemoryBarrier>&& memoryBarriers)
        {
            PipelineBarrier(srcStage, dstStage, dependency, memoryBarriers.data(), static_cast<UInt32>(memoryBarriers.size()));
        }

        void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
            Containers::Vector<RHIImageMemoryBarrier>&& imageMemoryBarriers)
        {
            PipelineBarrier(srcStage, dstStage, dependency, imageMemoryBarriers.data(), static_cast<UInt32>(imageMemoryBarriers.size()));
        }

        void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
            Containers::Vector<RHIBufferMemoryBarrier>&& bufferMemoryBarriers)
        {
            PipelineBarrier(srcStage, dstStage, dependency, bufferMemoryBarriers.data(), static_cast<UInt32>(bufferMemoryBarriers.size()));
        }

        // Optional hook: allow the backend to track per-submit resource usage (descriptor pools, etc).
        // Default: no-op.
        virtual void TrackDescriptorPoolUse(RHIDescriptorPool* pool, UInt32 poolId)
        {
            (void)pool;
            (void)poolId;
        }
    protected:

    protected:
        friend class RHICommandBufferPool;
        friend class RHIVkCommandBufferPool;
        friend class RHIVkQueue; // Added for tracking access
        virtual void ResetInternal() = 0;

    private:
        RHICommandBufferPool* m_CommandBufferPool;
        RHIDevice* m_Device;
        ECommandBufferState m_State;
        RHIGpuTicket m_LatestSubmitTicket { 0 };
        
    protected:
        // Protected accessors for members needed by derived classes if any
        RHICommandBufferPool* GetPool() const { return m_CommandBufferPool; }
        RHIDevice* GetDevice() const { return m_Device; }
        ECommandBufferState GetState() const { return m_State; }
        void SetState(ECommandBufferState state) { m_State = state; }
    };

    inline const bool RHICommandBuffer::ReadyForSubmit() const
    {
        return m_State == ECommandBufferState::Executable;
    }
}

