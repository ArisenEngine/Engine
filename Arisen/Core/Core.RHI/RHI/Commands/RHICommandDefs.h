#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Enums/Pipeline/EPipelineBindPoint.h"
#include "RHI/Enums/Pipeline/EPipelineStageFlag.h"
#include "RHI/Enums/Subpass/EDependencyFlag.h"
#include "RHI/Enums/Pipeline/EIndexType.h"
#include "RHI/Enums/Pipeline/ECullMode.h"
#include "RHI/Enums/Pipeline/EFrontFace.h"
#include "RHI/Enums/Pipeline/EPrimitiveTopology.h"
#include "RHI/Enums/Sampler/ECompareOp.h"
#include "RHI/Pipeline/RHIDepthStencilState.h"
#include "RHI/Enums/Pipeline/EShadingRate.h"
#include "RHI/Enums/Pipeline/EShadingRateCombiner.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Commands/RHIBufferImageCopy.h"
#include "RHI/Sync/RHIBufferMemoryBarrier.h"
#include "RHI/Sync/RHIImageMemoryBarrier.h"
#include "RHI/Sync/RHIMemoryBarrier.h"
#include "RHI/Commands/RHIImageCopy.h"
#include "RHI/Resources/RHIAccelerationStructure.h"
#include "../RenderPass/RHIFrameBuffer.h" // Needed for handles? No, handles are in RHIHandle.h

namespace ArisenEngine::RHI
{
    struct RHIDeviceAddressRegion
    {
        UInt64 deviceAddress{0};
        UInt64 stride{0};
        UInt64 size{0};
    };

    struct RHITraceRaysDescriptor
    {
        RHIDeviceAddressRegion raygenShaderRecord;
        RHIDeviceAddressRegion missShaderTable;
        RHIDeviceAddressRegion hitShaderTable;
        RHIDeviceAddressRegion callableShaderTable;
        UInt32 width{1};
        UInt32 height{1};
        UInt32 depth{1};
    };

    enum class ERHICommandType : UInt8
    {
        BeginRenderPass,
        EndRenderPass,
        BeginRendering,
        EndRendering,
        Begin,
        End,
        ExecuteCommands,
        SetViewport,
        SetScissor,
        SetLineWidth,
        SetDepthBias,
        SetBlendConstants,
        SetStencilReference,
        SetCullMode,
        SetFrontFace,
        SetPrimitiveTopology,
        SetDepthTestEnable,
        SetDepthWriteEnable,
        SetDepthCompareOp,
        SetStencilTestEnable,
        SetStencilOp,
        BindPipeline,
        Draw,
        DrawIndexed,
        DrawIndirect,
        DrawIndexedIndirect,
        Dispatch,
        DrawMeshTasks,
        BindVertexBuffers,
        BindIndexBuffer,
        CopyBuffer,
        BindDescriptorSets,
        PushConstants,
        CopyBufferToImage,
        PipelineBarrier,
        TransitionImageLayout,
        CopyImage,
        GenerateMipmaps,
        BuildAccelerationStructures,
        TraceRays,
        SetFragmentShadingRate,
        BeginDebugLabel,
        EndDebugLabel,
        InsertDebugMarker,
        TrackDescriptorPoolUse,

        BindDescriptorBuffers,
        SetDescriptorBufferOffsets,

        // Count
        Count
    };

    struct RHICmdHeader
    {
        ERHICommandType type;
    };

    // --- Command Structs ---

    struct RHICmdBeginRenderPass
    {
        RHIRenderPassHandle renderPass;
        RHIFrameBufferHandle frameBuffer;
        ESubpassContents subpassContents;
        UInt32 clearValueCount;
        // Note: Variable length data (clear values) should be handled carefully. 
        // For simplicity in POD, we might need a pointer or offset, but since we are recording to a linear stream,
        // we can store variable data immediately after the struct.
        // For now, let's assume we copy the clear values into the stream after this struct.
    };

    struct RHICmdEndRenderPass
    {
    };

    struct RHICmdBeginRendering
    {
        // Similar to BeginRenderPass, RenderingInfo has pointers.
        // We will need to Serialize the data pointed to by RenderingInfo into the stream.
        // This struct serves as a header, followed by the serialized data.
        UInt32 dynamicSize; // Size of variable data following this command
    };

    struct RHICmdEndRendering
    {
    };

    struct RHICmdBegin
    {
        UInt32 frameIndex;
        UInt32 commandBufferUsage;
        bool hasInheritanceInfo; // If true, InheritanceInfo follows
    };

    struct RHICmdEnd
    {
    };

    struct RHICmdExecuteCommands
    {
        UInt32 count;
        // Followed by count * RHICommandBuffer* (or handles)
    };

    struct RHICmdSetViewport
    {
        Float32 x, y, width, height, minDepth, maxDepth;
    };

    struct RHICmdSetScissor
    {
        UInt32 offsetX, offsetY, width, height;
    };

    struct RHICmdSetLineWidth
    {
        Float32 lineWidth;
    };

    struct RHICmdSetDepthBias
    {
        Float32 depthBiasConstantFactor;
        Float32 depthBiasClamp;
        Float32 depthBiasSlopeFactor;
    };

    struct RHICmdSetBlendConstants
    {
        Float32 blendConstants[4];
    };

    struct RHICmdSetStencilReference
    {
        UInt32 faceMask;
        UInt32 reference;
    };

    struct RHICmdSetCullMode
    {
        ECullModeFlagBits cullMode;
    };

    struct RHICmdSetFrontFace
    {
        EFrontFace frontFace;
    };

    struct RHICmdSetPrimitiveTopology
    {
        EPrimitiveTopology topology;
    };

    struct RHICmdSetDepthTestEnable
    {
        bool enable;
    };

    struct RHICmdSetDepthWriteEnable
    {
        bool enable;
    };

    struct RHICmdSetDepthCompareOp
    {
        ECompareOp depthCompareOp;
    };

    struct RHICmdSetStencilTestEnable
    {
        bool enable;
    };

    struct RHICmdSetStencilOp
    {
        UInt32 faceMask;
        EStencilOp failOp;
        EStencilOp passOp;
        EStencilOp depthFailOp;
        ECompareOp compareOp;
    };

    struct RHICmdBindPipeline
    {
        RHIPipelineHandle pipeline;
    };

    struct RHICmdDraw
    {
        UInt32 vertexCount;
        UInt32 instanceCount;
        UInt32 firstVertex;
        UInt32 firstInstance;
        UInt32 firstBinding;
    };

    struct RHICmdDrawIndexed
    {
        UInt32 indexCount;
        UInt32 instanceCount;
        UInt32 firstIndex;
        UInt32 vertexOffset;
        UInt32 firstInstance;
        UInt32 firstBinding;
    };

    struct RHICmdDrawIndirect
    {
        RHIBufferHandle buffer;
        UInt64 offset;
        UInt32 drawCount;
        UInt32 stride;
    };

    struct RHICmdDrawIndexedIndirect
    {
        RHIBufferHandle buffer;
        UInt64 offset;
        UInt32 drawCount;
        UInt32 stride;
    };

    struct RHICmdDispatch
    {
        UInt32 groupCountX;
        UInt32 groupCountY;
        UInt32 groupCountZ;
    };

    struct RHICmdDrawMeshTasks
    {
        UInt32 groupCountX;
        UInt32 groupCountY;
        UInt32 groupCountZ;
    };

    struct RHICmdBindVertexBuffers
    {
        RHIBufferHandle buffer;
        UInt64 offset;
    };

    struct RHICmdBindIndexBuffer
    {
        RHIBufferHandle indexBuffer;
        UInt64 offset;
        EIndexType type;
    };

    struct RHICmdCopyBuffer
    {
        RHIBufferHandle src;
        UInt64 srcOffset;
        RHIBufferHandle dst;
        UInt64 dstOffset;
        UInt64 size;
    };

    struct RHICmdBindDescriptorSets
    {
        EPipelineBindPoint bindPoint;
        UInt32 firstSet;
        UInt32 descriptorSetCount; // Number of descriptor sets (pointers/handles) following
        UInt32 dynamicOffsetCount; // Number of dynamic offsets following
        // Followed by:
        // - descriptorSetCount * RHIDescriptorSet* (or handles)
        // - dynamicOffsetCount * UInt32
    };

    // For the pool-based bind (bindless optimization)
    struct RHICmdBindDescriptorSetsPool
    {
        EPipelineBindPoint bindPoint;
        UInt32 firstSet;
        RHIDescriptorPoolHandle poolHandle;
        UInt32 poolId;
        UInt32 setIndex; // Used for BindDescriptorSet (singular)
        bool isSingleSet; // Differentiates Transmit vs TransmitOne
    };

    struct RHICmdPushConstants
    {
        UInt32 offset;
        UInt32 size;
        UInt32 stageFlags;
        // Followed by 'size' bytes of data
    };

    struct RHICmdCopyBufferToImage
    {
        RHIBufferHandle srcBuffer;
        RHIImageHandle dst;
        EImageLayout dstImageLayout;
        UInt32 regionCount;
        // Followed by regionCount * RHIBufferImageCopy
    };

    struct RHICmdPipelineBarrier
    {
        EPipelineStageFlag srcStage;
        EPipelineStageFlag dstStage;
        UInt32 dependency;
        UInt32 memoryBarrierCount;
        UInt32 imageMemoryBarrierCount;
        UInt32 bufferMemoryBarrierCount;
        // Followed by:
        // - memoryBarrierCount * RHIMemoryBarrier
        // - imageMemoryBarrierCount * RHIImageMemoryBarrier
        // - bufferMemoryBarrierCount * RHIBufferMemoryBarrier
    };

    struct RHICmdTransitionImageLayout
    {
        RHIImageHandle image;
        EImageLayout oldLayout; // Can be Undefined if not provided
        EImageLayout targetLayout;
    };

    struct RHICmdCopyImage
    {
        RHIImageHandle src;
        EImageLayout srcLayout;
        RHIImageHandle dst;
        EImageLayout dstLayout;
        UInt32 regionCount;
        // Followed by regionCount * RHIImageCopy
    };

    struct RHICmdGenerateMipmaps
    {
        RHIImageHandle image;
    };

    struct RHICmdBuildAccelerationStructures
    {
        UInt32 infoCount;
        // This is complex due to pointers in the info structs. 
        // For simplicity, we might need to deep copy the info and range info into the stream.
        // For Phase 1, we might keep this as a lambda or specialized command if too complex for POD.
        // Let's assume we serialize the data.
        UInt32 totalDataSize; // Size of all serialized data following
    };

    struct RHICmdTraceRays
    {
        RHITraceRaysDescriptor desc;
    };

    struct RHICmdSetFragmentShadingRate
    {
        EShadingRate rate;
        EShadingRateCombiner combinerOp[2];
    };

    struct RHICmdBeginDebugLabel
    {
        Float32 color[4];
        UInt32 labelLen;
        // Followed by label string (null terminated or length based)
    };

    struct RHICmdEndDebugLabel
    {
    };

    struct RHICmdInsertDebugMarker
    {
        Float32 color[4];
        UInt32 labelLen;
        // Followed by label string
    };

    struct RHICmdTrackDescriptorPoolUse
    {
        RHIDescriptorPoolHandle poolHandle;
        UInt32 poolId;
    };

    struct RHICmdBindDescriptorBuffers
    {
        UInt32 bufferCount;
        // Followed by bufferCount * RHIBufferHandle
    };

    struct RHICmdSetDescriptorBufferOffsets
    {
        EPipelineBindPoint bindPoint;
        RHIPipelineHandle pipeline;
        UInt32 firstSet;
        UInt32 setCount;
        // Followed by:
        // - setCount * UInt32 (bufferIndices)
        // - setCount * UInt64 (offsets)
    };
} // namespace ArisenEngine::RHI
