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
#include "RHI/Enums/Pipeline/ECommandBufferLevel.h"
#include "RHI/Commands/RHIBufferImageCopy.h"
#include "RHI/Sync/RHIBufferMemoryBarrier.h"
#include "RHI/Sync/RHIImageMemoryBarrier.h"
#include "RHI/Sync/RHIMemoryBarrier.h"
#include "RHI/Enums/Pipeline/ECullMode.h"
#include "RHI/Enums/Pipeline/EFrontFace.h"
#include "RHI/Enums/Pipeline/EPrimitiveTopology.h"
#include "RHI/Enums/Sampler/ECompareOp.h"
#include "RHI/Pipeline/RHIDepthStencilState.h"
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

    struct RHICommandBufferInheritanceInfo
    {
        RHIRenderPassHandle renderPass;
        UInt32 subpass;
        RHIFrameBufferHandle frameBuffer;
        bool occlusionQueryEnable = false;
        UInt32 occlusionQueryFlags = 0;
        UInt32 pipelineStatistics;
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

        RHICommandBuffer(RHIDevice* device, RHICommandBufferPool* pool, ECommandBufferLevel level = COMMAND_BUFFER_LEVEL_PRIMARY):
        m_CommandBufferPool(pool), m_Device(device), m_State(ECommandBufferState::Initial), m_Level(level)
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

        virtual void* GetHandle() const = 0;

    protected:
        void SetLatestSubmitTicket(RHIGpuTicket id) { m_LatestSubmitTicket = id; }
        RHIGpuTicket GetLatestSubmitTicket() const { return m_LatestSubmitTicket; }
        
    public:
        const bool ReadyForSubmit() const;

        // Command Interface
        virtual void BeginRenderPass(RenderPassBeginDesc&& desc) = 0;
        virtual void EndRenderPass() = 0;

        virtual void BeginRendering(const RHIRenderingInfo& info) = 0;
        virtual void EndRendering() = 0;
        
        virtual void Begin() = 0;
        virtual void Begin(UInt32 frameIndex, UInt32 commandBufferUsage = 0, const RHICommandBufferInheritanceInfo* pInheritanceInfo = nullptr) = 0;
        virtual void End() = 0;

        virtual void ExecuteCommands(Containers::Vector<RHICommandBuffer*>&& secondaryBuffers) = 0;
        
        virtual void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height, Float32 minDepth, Float32 maxDepth) = 0;
        virtual void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height) = 0;
        virtual void SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width, UInt32 height) = 0;
        virtual void SetLineWidth(Float32 lineWidth) = 0;
        virtual void SetDepthBias(Float32 depthBiasConstantFactor, Float32 depthBiasClamp, Float32 depthBiasSlopeFactor) = 0;
        virtual void SetBlendConstants(const Float32 blendConstants[4]) = 0;
        virtual void SetStencilReference(UInt32 faceMask, UInt32 reference) = 0;
        
        // Extended dynamic states (Modern RHI)
        virtual void SetCullMode(ECullModeFlagBits cullMode) = 0;
        virtual void SetFrontFace(EFrontFace frontFace) = 0;
        virtual void SetPrimitiveTopology(EPrimitiveTopology topology) = 0;
        virtual void SetDepthTestEnable(bool enable) = 0;
        virtual void SetDepthWriteEnable(bool enable) = 0;
        virtual void SetDepthCompareOp(ECompareOp depthCompareOp) = 0;
        virtual void SetStencilTestEnable(bool enable) = 0;
        virtual void SetStencilOp(UInt32 faceMask, EStencilOp failOp, EStencilOp passOp, EStencilOp depthFailOp, ECompareOp compareOp) = 0;

        virtual void BindPipeline(RHIPipelineHandle pipeline) = 0;
        virtual void Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance, UInt32 firstBinding) = 0;
        virtual void DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset, UInt32 firstInstance,  UInt32 firstBinding) = 0;
        virtual void DrawIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride) = 0;
        virtual void DrawIndexedIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride) = 0;
        virtual void DrawMeshTasks(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ) = 0;
        virtual void Dispatch(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ) = 0;
        virtual void BindVertexBuffers(RHIBufferHandle buffer, UInt64 offset) = 0;
        virtual void BindIndexBuffer(RHIBufferHandle indexBuffer, UInt64 offset, EIndexType type) = 0;
        
        // Synchronization moved to RHISubmitDescriptor
        // virtual void WaitSemaphore(RHISemaphoreHandle semaphore, EPipelineStageFlag stage) = 0;
        // virtual void SignalSemaphore(RHISemaphoreHandle semaphore) = 0;

        virtual void CopyBuffer(RHIBufferHandle src, UInt64 srcOffset, RHIBufferHandle dst, UInt64 dstOffset, UInt64 size) = 0;
        
        virtual void BindDescriptorSets(EPipelineBindPoint bindPoint,
    UInt32 firstSet, Containers::Vector<std::shared_ptr<RHIDescriptorSet>>& descriptorsets, UInt32 dynamicOffsetCount, const UInt32* pDynamicOffsets) = 0;

        virtual void PushConstants(UInt32 offset, UInt32 size, const void* data, UInt32 stageFlags) = 0;

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

        virtual void TransitionImageLayout(RHIImageHandle image, EImageLayout targetLayout) = 0;
        virtual void TransitionImageLayout(RHIImageHandle image, EImageLayout oldLayout, EImageLayout targetLayout) = 0;

        virtual void GenerateMipmaps(RHIImageHandle image) = 0;

        // Debug Markers
        virtual void BeginDebugLabel(const char* label, const Float32 color[4]) = 0;
        virtual void EndDebugLabel() = 0;
        virtual void InsertDebugMarker(const char* label, const Float32 color[4]) = 0;

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

    protected:
        // Protected accessors for members needed by derived classes if any
        RHICommandBufferPool* GetPool() const { return m_CommandBufferPool; }
        RHIDevice* GetDevice() const { return m_Device; }
        ECommandBufferState GetState() const { return m_State; }
        void SetState(ECommandBufferState state) { m_State = state; }
    public:
        ECommandBufferLevel GetLevel() const { return m_Level; }
        
        void SetCurrentFrameIndex(UInt32 index) { m_CurrentFrameIndex = index; }

    public:
        UInt32 GetCurrentFrameIndex() const { return m_CurrentFrameIndex; }

    private:
        RHICommandBufferPool* m_CommandBufferPool;
        RHIDevice* m_Device;
        ECommandBufferState m_State;
        ECommandBufferLevel m_Level;
        RHIGpuTicket m_LatestSubmitTicket { 0 };
        UInt32 m_CurrentFrameIndex { 0 };
    };

    inline const bool RHICommandBuffer::ReadyForSubmit() const
    {
        return m_State == ECommandBufferState::Executable;
    }
}

