#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Enums/Pipeline/EPipelineBindPoint.h"
#include "RHI/Enums/Pipeline/EIndexType.h"
#include "RHI/Enums/Pipeline/EShadingRate.h"
#include "RHI/Enums/Pipeline/EShadingRateCombiner.h"

namespace ArisenEngine::RHI
{
    struct RenderPassBeginDesc;
    struct RHIRenderingInfo;
    struct RHICommandBufferInheritanceInfo;
    struct RHIBufferImageCopy;
    struct RHICmdPipelineBarrier;
    struct RHIMemoryBarrier;
    struct RHIImageMemoryBarrier;
    struct RHIBufferMemoryBarrier;
    struct RHIImageCopy;
    struct RHIAccelerationStructureBuildGeometryInfo;
    struct RHIAccelerationStructureBuildRangeInfo;
    struct RHITraceRaysDescriptor;

    /**
     * @brief Abstract interface for command execution.
     * This allows RHICommandBuffer::Replay to be implemented in a .cpp file.
     */
    class IRHICommandExecutor
    {
    public:
        virtual ~IRHICommandExecutor() = default;

        virtual void BeginRenderPass(RenderPassBeginDesc&& desc) = 0;
        virtual void EndRenderPass() = 0;
        virtual void BeginRendering(const RHIRenderingInfo& info) = 0;
        virtual void EndRendering() = 0;
        virtual void Begin(UInt32 frameIndex, UInt32 commandBufferUsage, const RHICommandBufferInheritanceInfo* pInheritanceInfo) = 0;
        virtual void End() = 0;
        virtual void BindPipeline(RHIPipelineHandle pipeline) = 0;
        virtual void Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance, UInt32 firstBinding) = 0;
        virtual void DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset, UInt32 firstInstance, UInt32 firstBinding) = 0;
        virtual void DrawIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride) = 0;
        virtual void DrawIndexedIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride) = 0;
        virtual void Dispatch(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ) = 0;
        virtual void DrawMeshTasks(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ) = 0;
        virtual void BindVertexBuffers(RHIBufferHandle buffer, UInt64 offset) = 0;
        virtual void BindIndexBuffer(RHIBufferHandle indexBuffer, UInt64 offset, EIndexType type) = 0;
        virtual void CopyBuffer(RHIBufferHandle src, UInt64 srcOffset, RHIBufferHandle dst, UInt64 dstOffset, UInt64 size) = 0;
        virtual void BindDescriptorSets(EPipelineBindPoint bindPoint, UInt32 firstSet, RHIDescriptorPoolHandle poolHandle, UInt32 poolId, UInt32 setIndex, bool isSingleSet) = 0;
        virtual void PushConstants(UInt32 offset, UInt32 size, const void* data, UInt32 stageFlags) = 0;
        virtual void CopyBufferToImage(RHIBufferHandle srcBuffer, RHIImageHandle dst, EImageLayout dstImageLayout, UInt32 regionCount, const RHIBufferImageCopy* pRegions) = 0;
        virtual void PipelineBarrier(const RHICmdPipelineBarrier& cmd, const RHIMemoryBarrier* pMem, const RHIImageMemoryBarrier* pImg, const RHIBufferMemoryBarrier* pBuf) = 0;
        virtual void TransitionImageLayout(RHIImageHandle image, EImageLayout oldLayout, EImageLayout targetLayout) = 0;
        virtual void CopyImage(RHIImageHandle src, EImageLayout srcLayout, RHIImageHandle dst, EImageLayout dstLayout, UInt32 regionCount, const RHIImageCopy* pRegions) = 0;
        virtual void GenerateMipmaps(RHIImageHandle image) = 0;
        virtual void BuildAccelerationStructures(UInt32 infoCount, const RHIAccelerationStructureBuildGeometryInfo* pInfos, const RHIAccelerationStructureBuildRangeInfo* const* ppBuildRangeInfos) = 0;
        virtual void TraceRays(const RHITraceRaysDescriptor& desc) = 0;
        virtual void SetFragmentShadingRate(EShadingRate rate, EShadingRateCombiner combinerOp[2]) = 0;
        virtual void BeginDebugLabel(const char* label, const Float32 color[4]) = 0;
        virtual void EndDebugLabel() = 0;
        virtual void InsertDebugMarker(const char* label, const Float32 color[4]) = 0;
        virtual void TrackDescriptorPoolUse(RHIDescriptorPoolHandle poolHandle, UInt32 poolId) = 0;

        // Dynamic States
        virtual void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height, Float32 minDepth, Float32 maxDepth) = 0;
        virtual void SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width, UInt32 height) = 0;
        virtual void SetLineWidth(Float32 lineWidth) = 0;
        virtual void SetDepthBias(Float32 depthBiasConstantFactor, Float32 depthBiasClamp, Float32 depthBiasSlopeFactor) = 0;
        virtual void SetBlendConstants(const Float32 blendConstants[4]) = 0;
        virtual void SetStencilReference(UInt32 faceMask, UInt32 reference) = 0;
        virtual void SetCullMode(ECullModeFlagBits cullMode) = 0;
        virtual void SetFrontFace(EFrontFace frontFace) = 0;
        virtual void SetPrimitiveTopology(EPrimitiveTopology topology) = 0;
        virtual void SetDepthTestEnable(bool enable) = 0;
        virtual void SetDepthWriteEnable(bool enable) = 0;
        virtual void SetDepthCompareOp(ECompareOp depthCompareOp) = 0;
        virtual void SetStencilTestEnable(bool enable) = 0;
        virtual void SetStencilOp(UInt32 faceMask, EStencilOp failOp, EStencilOp passOp, EStencilOp depthFailOp, ECompareOp compareOp) = 0;
    };
}
