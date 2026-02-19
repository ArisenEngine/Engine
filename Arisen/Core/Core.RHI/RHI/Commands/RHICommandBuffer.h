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
#include "RHI/Commands/RHIImageCopy.h"
#include "RHI/Enums/Pipeline/ECullMode.h"
#include "RHI/Enums/Pipeline/EFrontFace.h"
#include "RHI/Enums/Pipeline/EPrimitiveTopology.h"
#include "RHI/Enums/Sampler/ECompareOp.h"
#include "RHI/Pipeline/RHIDepthStencilState.h"
#include "RHI/Enums/Pipeline/EShadingRate.h"
#include "RHI/Enums/Pipeline/EShadingRateCombiner.h"
#include "../Handles/RHIHandle.h"
#include "RHI/Definitions/CoreRHICommon.h"
#include "RHICommandDefs.h"

namespace ArisenEngine::RHI
{
    class RHIDescriptorSet;
    class RHIFence;
    class RHIDescriptorPool;
    struct RHIAccelerationStructureBuildGeometryInfo;
    struct RHIAccelerationStructureBuildRangeInfo;
    class IRHICommandExecutor;

}

namespace ArisenEngine::RHI
{
    class RHISemaphore;
}

namespace ArisenEngine::RHI
{
    class RHIPipeline;
    class RHIDevice;
    class RHIDescriptorSet;
    class RHIDescriptorPool;
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
    
    
    class RHI_DLL RHICommandBuffer
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

        RHICommandBuffer(RHIDevice* device, RHICommandBufferPool* pool, ECommandBufferLevel level = COMMAND_BUFFER_LEVEL_PRIMARY);
        virtual ~RHICommandBuffer() noexcept;
        
        RHICommandBufferPool* GetOwner() const
        {
            return m_CommandBufferPool;
        };

        virtual void* GetHandle() const = 0;
        virtual RHICommandBufferHandle GetRHIHandle() const { return m_Handle; }
        virtual void SetRHIHandle(RHICommandBufferHandle handle) { m_Handle = handle; }

    protected:
        void SetLatestSubmitTicket(RHIGpuTicket id) { m_LatestSubmitTicket = id; }
        RHIGpuTicket GetLatestSubmitTicket() const { return m_LatestSubmitTicket; }
        
    public:
        const bool ReadyForSubmit() const;

        // Command Interface
        // Command Interface
        void BeginRenderPass(RenderPassBeginDesc&& desc);
        void EndRenderPass();

        void BeginRendering(const RHIRenderingInfo& info);
        void EndRendering();
        
        void Begin();
        void Begin(UInt32 frameIndex, UInt32 commandBufferUsage = 0, const RHICommandBufferInheritanceInfo* pInheritanceInfo = nullptr);
        void End();

        void ExecuteCommands(Containers::Vector<RHICommandBuffer*>&& secondaryBuffers);
        
        void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height, Float32 minDepth, Float32 maxDepth);
        void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height);
        void SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width, UInt32 height);
        void SetLineWidth(Float32 lineWidth);
        void SetDepthBias(Float32 depthBiasConstantFactor, Float32 depthBiasClamp, Float32 depthBiasSlopeFactor);
        void SetBlendConstants(const Float32 blendConstants[4]);
        void SetStencilReference(UInt32 faceMask, UInt32 reference);
        
        // Extended dynamic states (Modern RHI)
        void SetCullMode(ECullModeFlagBits cullMode);
        void SetFrontFace(EFrontFace frontFace);
        void SetPrimitiveTopology(EPrimitiveTopology topology);
        void SetDepthTestEnable(bool enable);
        void SetDepthWriteEnable(bool enable);
        void SetDepthCompareOp(ECompareOp depthCompareOp);
        void SetStencilTestEnable(bool enable);
        void SetStencilOp(UInt32 faceMask, EStencilOp failOp, EStencilOp passOp, EStencilOp depthFailOp, ECompareOp compareOp);

        void BindPipeline(RHIPipelineHandle pipeline);
        void Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance, UInt32 firstBinding);
        void DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset, UInt32 firstInstance,  UInt32 firstBinding);
        void DrawIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride);
        void DrawIndexedIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride);
        void DrawMeshTasks(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ);
        void Dispatch(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ);
        void BindVertexBuffers(RHIBufferHandle buffer, UInt64 offset);
        void BindIndexBuffer(RHIBufferHandle indexBuffer, UInt64 offset, EIndexType type);
        
        // Synchronization moved to RHISubmitDescriptor
        // virtual void WaitSemaphore(RHISemaphoreHandle semaphore, EPipelineStageFlag stage) = 0;
        // virtual void SignalSemaphore(RHISemaphoreHandle semaphore) = 0;

        void CopyBuffer(RHIBufferHandle src, UInt64 srcOffset, RHIBufferHandle dst, UInt64 dstOffset, UInt64 size);
        
        void BindDescriptorSets(EPipelineBindPoint bindPoint,
    UInt32 firstSet, Containers::Vector<std::shared_ptr<RHIDescriptorSet>>& descriptorsets, UInt32 dynamicOffsetCount, const UInt32* pDynamicOffsets);

        void BindDescriptorSets(EPipelineBindPoint bindPoint, UInt32 firstSet, RHIDescriptorPoolHandle poolHandle, UInt32 poolId);
        void BindDescriptorSet(EPipelineBindPoint bindPoint, UInt32 firstSet, RHIDescriptorPoolHandle poolHandle, UInt32 poolId, UInt32 setIndex);

        void PushConstants(UInt32 offset, UInt32 size, const void* data, UInt32 stageFlags);

        void CopyBufferToImage(RHIBufferHandle srcBuffer, RHIImageHandle dst,
            EImageLayout dstImageLayout, Containers::Vector<RHIBufferImageCopy>&& regions);
        void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
            const RHIMemoryBarrier* pMemoryBarriers, UInt32 memoryBarrierCount,
            const RHIImageMemoryBarrier* pImageMemoryBarriers, UInt32 imageMemoryBarrierCount,
            const RHIBufferMemoryBarrier* pBufferMemoryBarriers, UInt32 bufferMemoryBarrierCount);
        void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
            const RHIMemoryBarrier* pMemoryBarriers, UInt32 memoryBarrierCount);
        void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
            const RHIImageMemoryBarrier* pImageMemoryBarriers, UInt32 imageMemoryBarrierCount);
        void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
            const RHIBufferMemoryBarrier* pBufferMemoryBarriers, UInt32 bufferMemoryBarrierCount);

        void TransitionImageLayout(RHIImageHandle image, EImageLayout targetLayout);
        void TransitionImageLayout(RHIImageHandle image, EImageLayout oldLayout, EImageLayout targetLayout);

        void CopyImage(RHIImageHandle src, EImageLayout srcLayout, RHIImageHandle dst, EImageLayout dstLayout, UInt32 regionCount, const RHIImageCopy* pRegions);

        void GenerateMipmaps(RHIImageHandle image);
        
        // Ray Tracing
        void BuildAccelerationStructures(UInt32 infoCount, const RHIAccelerationStructureBuildGeometryInfo* pInfos, const RHIAccelerationStructureBuildRangeInfo* const* ppBuildRangeInfos);
        void TraceRays(const RHITraceRaysDescriptor& desc);

        void SetFragmentShadingRate(EShadingRate rate, EShadingRateCombiner combinerOp[2]);

        // Debug Markers
        void BeginDebugLabel(const char* label, const Float32 color[4]);
        void EndDebugLabel();
        void InsertDebugMarker(const char* label, const Float32 color[4]);

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
        virtual void TrackDescriptorPoolUse(RHIDescriptorPoolHandle poolHandle, UInt32 poolId)
        {
            RecordCommand<RHICmdTrackDescriptorPoolUse>(ERHICommandType::TrackDescriptorPoolUse, { poolHandle, poolId });
        }

        void Replay(IRHICommandExecutor& executor);

    protected:
        Containers::Vector<UInt8> m_CommandStream;

        template<typename T>
        void RecordCommand(ERHICommandType type, const T& command)
        {
            const size_t headerSize = sizeof(RHICmdHeader);
            const size_t cmdSize = sizeof(T);
            size_t currentSize = m_CommandStream.size();
            m_CommandStream.resize(currentSize + headerSize + cmdSize);
            
            RHICmdHeader header{type};
            std::memcpy(m_CommandStream.data() + currentSize, &header, headerSize);
            std::memcpy(m_CommandStream.data() + currentSize + headerSize, &command, cmdSize);
        }

        // Overload for variable size data
        template<typename T>
        void RecordCommand(ERHICommandType type, const T& command, const void* extraData, size_t extraSize)
        {
             const size_t headerSize = sizeof(RHICmdHeader);
            const size_t cmdSize = sizeof(T);
            size_t currentSize = m_CommandStream.size();
            m_CommandStream.resize(currentSize + headerSize + cmdSize + extraSize);
            
            RHICmdHeader header{type};
            std::memcpy(m_CommandStream.data() + currentSize, &header, headerSize);
            std::memcpy(m_CommandStream.data() + currentSize + headerSize, &command, cmdSize);
            
            if (extraSize > 0)
            {
                std::memcpy(m_CommandStream.data() + currentSize + headerSize + cmdSize, extraData, extraSize);
            }
        }


    protected:
        // TODO(CppSharp-P1): friend class RHIVkCommandBufferPool / RHIVkQueue 是后端类型，
        // 不应出现在抽象 Core.RHI 头文件中。CppSharp 解析此头文件时会尝试解析这些类型。
        // 方案: 使用 protected virtual 方法替代 friend 访问，或在后端 .cpp 中使用 static_cast。
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
    // TODO(Interface-P2): 访问控制段混乱。多个 public:/protected: 段交替出现 (L279, L728, L734, L740, L745)。
    // 建议统一为: public → protected → private 各一个段，按职责分组方法。
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
        RHICommandBufferHandle m_Handle;
    };

    inline const bool RHICommandBuffer::ReadyForSubmit() const
    {
        return m_State == ECommandBufferState::Executable;
    }
}

