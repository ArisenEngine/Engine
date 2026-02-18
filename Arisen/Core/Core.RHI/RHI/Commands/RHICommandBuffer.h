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

        void BindDescriptorSets(EPipelineBindPoint bindPoint, UInt32 firstSet, RHIDescriptorPool* pool, UInt32 poolId);
        void BindDescriptorSet(EPipelineBindPoint bindPoint, UInt32 firstSet, RHIDescriptorPool* pool, UInt32 poolId, UInt32 setIndex);

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
        virtual void TrackDescriptorPoolUse(RHIDescriptorPool* pool, UInt32 poolId)
        {
            RecordCommand<RHICmdTrackDescriptorPoolUse>(ERHICommandType::TrackDescriptorPoolUse, { pool, poolId });
        }

    public:
        // TODO(Perf-P2): Replay<Executor> 模板在公共头文件中展开了 ~400 行代码。
        // 每个包含此头文件的 TU 都会重新实例化这个巨型 switch。建议：
        // 方案A: 移至 RHICommandBuffer.inl 仅在后端 .cpp 中 include（推荐）。
        // 方案B: 用虚函数 + visitor 模式替代模板，但会增加虚调用开销。
        // TODO(CppSharp-P1): CppSharp 无法处理 C++ 模板方法。Replay 不应导出。
        // 确保 CppSharp 配置中通过 IgnoreClassMethodWithName 跳过此方法。
        template<typename Executor>
        void Replay(Executor& executor)
        {
            size_t offset = 0;
            while (offset < m_CommandStream.size())
            {
                const RHICmdHeader* header = reinterpret_cast<const RHICmdHeader*>(m_CommandStream.data() + offset);
                offset += sizeof(RHICmdHeader);

                switch (header->type)
                {
                    case ERHICommandType::BeginRenderPass:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdBeginRenderPass*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdBeginRenderPass);
                        const auto* clearValues = reinterpret_cast<const RHIClearValue*>(m_CommandStream.data() + offset);
                        offset += cmd->clearValueCount * sizeof(RHIClearValue);
                        
                        RenderPassBeginDesc desc;
                        desc.renderPass = cmd->renderPass;
                        desc.frameBuffer = cmd->frameBuffer;
                        desc.subpassContents = cmd->subpassContents;
                        desc.clearValueCount = cmd->clearValueCount;
                        desc.pClearValues = clearValues;
                        executor.BeginRenderPass(std::move(desc));
                        break;
                    }
                    case ERHICommandType::EndRenderPass:
                    {
                        offset += sizeof(RHICmdEndRenderPass);
                        executor.EndRenderPass();
                        break;
                    }
                    case ERHICommandType::BeginRendering:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdBeginRendering*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdBeginRendering);
                        
                        // Copy to stack to fix pointers
                        RHIRenderingInfo info = *reinterpret_cast<const RHIRenderingInfo*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHIRenderingInfo);
                        
                        // Reconstruct pointers pointing into the stream
                        if (info.colorAttachmentCount > 0)
                        {
                            info.pColorAttachments = reinterpret_cast<const RHIRenderingAttachmentInfo*>(m_CommandStream.data() + offset);
                            offset += info.colorAttachmentCount * sizeof(RHIRenderingAttachmentInfo);
                        }
                        
                        if (info.pResolveAttachments != nullptr)
                        {
                            info.pResolveAttachments = reinterpret_cast<const RHIRenderingAttachmentInfo*>(m_CommandStream.data() + offset);
                            offset += info.colorAttachmentCount * sizeof(RHIRenderingAttachmentInfo);
                        }
                        
                        if (info.pDepthAttachment != nullptr)
                        {
                            info.pDepthAttachment = reinterpret_cast<const RHIRenderingAttachmentInfo*>(m_CommandStream.data() + offset);
                            offset += sizeof(RHIRenderingAttachmentInfo);
                        }
                        
                        if (info.pStencilAttachment != nullptr)
                        {
                            info.pStencilAttachment = reinterpret_cast<const RHIRenderingAttachmentInfo*>(m_CommandStream.data() + offset);
                            offset += sizeof(RHIRenderingAttachmentInfo);
                        }
                        
                        executor.BeginRendering(info);
                        break;
                    }
                    case ERHICommandType::EndRendering:
                    {
                        offset += sizeof(RHICmdEndRendering);
                        executor.EndRendering();
                        break;
                    }
                    case ERHICommandType::Begin:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdBegin*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdBegin);
                        const RHICommandBufferInheritanceInfo* pInheritanceInfo = nullptr;
                        if (cmd->hasInheritanceInfo)
                        {
                            pInheritanceInfo = reinterpret_cast<const RHICommandBufferInheritanceInfo*>(m_CommandStream.data() + offset);
                            offset += sizeof(RHICommandBufferInheritanceInfo);
                        }
                        executor.Begin(cmd->frameIndex, cmd->commandBufferUsage, pInheritanceInfo);
                        break;
                    }
                    case ERHICommandType::End:
                    {
                        offset += sizeof(RHICmdEnd);
                        executor.End();
                        break;
                    }
                    case ERHICommandType::BindPipeline:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdBindPipeline*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdBindPipeline);
                        executor.BindPipeline(cmd->pipeline);
                        break;
                    }
                    case ERHICommandType::Draw:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdDraw*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdDraw);
                        executor.Draw(cmd->vertexCount, cmd->instanceCount, cmd->firstVertex, cmd->firstInstance, cmd->firstBinding);
                        break;
                    }
                    case ERHICommandType::DrawIndexed:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdDrawIndexed*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdDrawIndexed);
                        executor.DrawIndexed(cmd->indexCount, cmd->instanceCount, cmd->firstIndex, cmd->vertexOffset, cmd->firstInstance, cmd->firstBinding);
                        break;
                    }
                    case ERHICommandType::DrawIndirect:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdDrawIndirect*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdDrawIndirect);
                        executor.DrawIndirect(cmd->buffer, cmd->offset, cmd->drawCount, cmd->stride);
                        break;
                    }
                    case ERHICommandType::DrawIndexedIndirect:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdDrawIndexedIndirect*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdDrawIndexedIndirect);
                        executor.DrawIndexedIndirect(cmd->buffer, cmd->offset, cmd->drawCount, cmd->stride);
                        break;
                    }
                    case ERHICommandType::Dispatch:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdDispatch*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdDispatch);
                        executor.Dispatch(cmd->groupCountX, cmd->groupCountY, cmd->groupCountZ);
                        break;
                    }
                    case ERHICommandType::DrawMeshTasks:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdDrawMeshTasks*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdDrawMeshTasks);
                        executor.DrawMeshTasks(cmd->groupCountX, cmd->groupCountY, cmd->groupCountZ);
                        break;
                    }
                    case ERHICommandType::BindVertexBuffers:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdBindVertexBuffers*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdBindVertexBuffers);
                        executor.BindVertexBuffers(cmd->buffer, cmd->offset);
                        break;
                    }
// Replay member fixes
                    case ERHICommandType::BindIndexBuffer:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdBindIndexBuffer*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdBindIndexBuffer);
                        executor.BindIndexBuffer(cmd->indexBuffer, cmd->offset, cmd->type);
                        break;
                    }
                    case ERHICommandType::BindDescriptorSets:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdBindDescriptorSetsPool*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdBindDescriptorSetsPool);
                        executor.BindDescriptorSets(cmd->bindPoint, cmd->firstSet, cmd->pool, cmd->poolId, cmd->setIndex, cmd->isSingleSet);
                        break;
                    }
                    case ERHICommandType::CopyBuffer:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdCopyBuffer*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdCopyBuffer);
                        executor.CopyBuffer(cmd->src, cmd->srcOffset, cmd->dst, cmd->dstOffset, cmd->size);
                        break;
                    }
                    case ERHICommandType::PushConstants:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdPushConstants*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdPushConstants);
                        const void* pData = m_CommandStream.data() + offset;
                        offset += cmd->size;
                        executor.PushConstants(cmd->offset, cmd->size, pData, cmd->stageFlags);
                        break;
                    }
                    case ERHICommandType::CopyBufferToImage:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdCopyBufferToImage*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdCopyBufferToImage);
                        const auto* pRegions = reinterpret_cast<const RHIBufferImageCopy*>(m_CommandStream.data() + offset);
                        offset += cmd->regionCount * sizeof(RHIBufferImageCopy);
                        executor.CopyBufferToImage(cmd->srcBuffer, cmd->dst, cmd->dstImageLayout, cmd->regionCount, pRegions);
                        break;
                    }
                    case ERHICommandType::PipelineBarrier:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdPipelineBarrier*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdPipelineBarrier);
                        const auto* pMem = reinterpret_cast<const RHIMemoryBarrier*>(m_CommandStream.data() + offset);
                        offset += cmd->memoryBarrierCount * sizeof(RHIMemoryBarrier);
                        const auto* pImg = reinterpret_cast<const RHIImageMemoryBarrier*>(m_CommandStream.data() + offset);
                        offset += cmd->imageMemoryBarrierCount * sizeof(RHIImageMemoryBarrier);
                        const auto* pBuf = reinterpret_cast<const RHIBufferMemoryBarrier*>(m_CommandStream.data() + offset);
                        offset += cmd->bufferMemoryBarrierCount * sizeof(RHIBufferMemoryBarrier);
                        executor.PipelineBarrier(*cmd, pMem, pImg, pBuf);
                        break;
                    }
                    case ERHICommandType::TransitionImageLayout:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdTransitionImageLayout*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdTransitionImageLayout);
                        executor.TransitionImageLayout(cmd->image, cmd->oldLayout, cmd->targetLayout);
                        break;
                    }
                    case ERHICommandType::CopyImage:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdCopyImage*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdCopyImage);
                        const auto* pRegions = reinterpret_cast<const RHIImageCopy*>(m_CommandStream.data() + offset);
                        offset += cmd->regionCount * sizeof(RHIImageCopy);
                        executor.CopyImage(cmd->src, cmd->srcLayout, cmd->dst, cmd->dstLayout, cmd->regionCount, pRegions);
                        break;
                    }
                    case ERHICommandType::GenerateMipmaps:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdGenerateMipmaps*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdGenerateMipmaps);
                        executor.GenerateMipmaps(cmd->image);
                        break;
                    }
                    case ERHICommandType::BuildAccelerationStructures:
                    {
                         const auto* cmd = reinterpret_cast<const RHICmdBuildAccelerationStructures*>(m_CommandStream.data() + offset);
                         offset += sizeof(RHICmdBuildAccelerationStructures);
                         
                         const uint8_t* dataStart = m_CommandStream.data() + offset;
                         const uint8_t* currentPtr = dataStart;

                         Containers::Vector<RHIAccelerationStructureBuildGeometryInfo> infos;
                         infos.resize(cmd->infoCount);
                         Containers::Vector<const RHIAccelerationStructureBuildRangeInfo*> rangePtrs;
                         rangePtrs.resize(cmd->infoCount);

                         for (UInt32 i = 0; i < cmd->infoCount; ++i)
                         {
                             // Reconstruct Info
                             std::memcpy(&infos[i], currentPtr, sizeof(RHIAccelerationStructureBuildGeometryInfo));
                             currentPtr += sizeof(RHIAccelerationStructureBuildGeometryInfo);

                             // Patch Geometry Pointer
                             infos[i].pGeometries = reinterpret_cast<const RHIAccelerationStructureGeometryData*>(currentPtr);
                             currentPtr += infos[i].geometryCount * sizeof(RHIAccelerationStructureGeometryData);

                             // Patch Range Pointer
                             rangePtrs[i] = reinterpret_cast<const RHIAccelerationStructureBuildRangeInfo*>(currentPtr);
                             currentPtr += infos[i].geometryCount * sizeof(RHIAccelerationStructureBuildRangeInfo);
                         }

                         executor.BuildAccelerationStructures(cmd->infoCount, infos.data(), rangePtrs.data());
                         offset += cmd->totalDataSize;
                         break;
                    }
                    case ERHICommandType::TraceRays:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdTraceRays*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdTraceRays);
                        executor.TraceRays(cmd->desc);
                        break;
                    }
                    case ERHICommandType::SetFragmentShadingRate:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetFragmentShadingRate*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdSetFragmentShadingRate);
                        EShadingRateCombiner combiners[2] = { cmd->combinerOp[0], cmd->combinerOp[1] };
                        executor.SetFragmentShadingRate(cmd->rate, combiners);
                        break;
                    }
                    // Debug
                    case ERHICommandType::BeginDebugLabel:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdBeginDebugLabel*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdBeginDebugLabel);
                        const char* label = reinterpret_cast<const char*>(m_CommandStream.data() + offset);
                        offset += cmd->labelLen;
                        executor.BeginDebugLabel(label, cmd->color);
                        break;
                    }
                    case ERHICommandType::EndDebugLabel:
                    {
                        offset += sizeof(RHICmdEndDebugLabel);
                        executor.EndDebugLabel();
                        break;
                    }
                    case ERHICommandType::InsertDebugMarker:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdInsertDebugMarker*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdInsertDebugMarker);
                        const char* label = reinterpret_cast<const char*>(m_CommandStream.data() + offset);
                        offset += cmd->labelLen;
                        executor.InsertDebugMarker(label, cmd->color);
                        break;
                    }
                    // Dynamic States
                    case ERHICommandType::SetViewport:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetViewport*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdSetViewport);
                        executor.SetViewport(cmd->x, cmd->y, cmd->width, cmd->height, cmd->minDepth, cmd->maxDepth);
                        break;
                    }
                    case ERHICommandType::SetScissor:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetScissor*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdSetScissor);
                        executor.SetScissor(cmd->offsetX, cmd->offsetY, cmd->width, cmd->height);
                        break;
                    }
                    case ERHICommandType::SetLineWidth:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetLineWidth*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdSetLineWidth);
                        executor.SetLineWidth(cmd->lineWidth);
                        break;
                    }
                    case ERHICommandType::SetDepthBias:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetDepthBias*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdSetDepthBias);
                        executor.SetDepthBias(cmd->depthBiasConstantFactor, cmd->depthBiasClamp, cmd->depthBiasSlopeFactor);
                        break;
                    }
                    case ERHICommandType::SetBlendConstants:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetBlendConstants*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdSetBlendConstants);
                        executor.SetBlendConstants(cmd->blendConstants);
                        break;
                    }
                    case ERHICommandType::SetStencilReference:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetStencilReference*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdSetStencilReference);
                        executor.SetStencilReference(cmd->faceMask, cmd->reference);
                        break;
                    }
                    case ERHICommandType::SetCullMode:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetCullMode*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdSetCullMode);
                        executor.SetCullMode(cmd->cullMode);
                        break;
                    }
                    case ERHICommandType::SetFrontFace:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetFrontFace*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdSetFrontFace);
                        executor.SetFrontFace(cmd->frontFace);
                        break;
                    }
                    case ERHICommandType::SetPrimitiveTopology:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetPrimitiveTopology*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdSetPrimitiveTopology);
                        executor.SetPrimitiveTopology(cmd->topology);
                        break;
                    }
                    case ERHICommandType::SetDepthTestEnable:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetDepthTestEnable*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdSetDepthTestEnable);
                        executor.SetDepthTestEnable(cmd->enable);
                        break;
                    }
                    case ERHICommandType::SetDepthWriteEnable:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetDepthWriteEnable*>(m_CommandStream.data() + offset);
                        offset += sizeof(RHICmdSetDepthWriteEnable);
                        executor.SetDepthWriteEnable(cmd->enable);
                        break;
                    }
                    case ERHICommandType::SetDepthCompareOp:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetDepthCompareOp*>(&m_CommandStream[offset]);
                        offset += sizeof(RHICmdSetDepthCompareOp);
                        executor.SetDepthCompareOp(cmd->depthCompareOp);
                        break;
                    }
                    case ERHICommandType::SetStencilTestEnable:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetStencilTestEnable*>(&m_CommandStream[offset]);
                        offset += sizeof(RHICmdSetStencilTestEnable);
                        executor.SetStencilTestEnable(cmd->enable);
                        break;
                    }
                    case ERHICommandType::SetStencilOp:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdSetStencilOp*>(&m_CommandStream[offset]);
                        offset += sizeof(RHICmdSetStencilOp);
                        executor.SetStencilOp(cmd->faceMask, cmd->failOp, cmd->passOp, cmd->depthFailOp, cmd->compareOp);
                        break;
                    }
                    case ERHICommandType::TrackDescriptorPoolUse:
                    {
                        const auto* cmd = reinterpret_cast<const RHICmdTrackDescriptorPoolUse*>(&m_CommandStream[offset]);
                        offset += sizeof(RHICmdTrackDescriptorPoolUse);
                        executor.TrackDescriptorPoolUse(cmd->pool, cmd->poolId);
                        break;
                    }
                    default:
                        // Unknown command, skip or break
                        break;
                }
            }
        }

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

