#include "RHICommandBuffer.h"

namespace ArisenEngine::RHI
{
    void RHICommandBuffer::BeginRenderPass(RenderPassBeginDesc&& desc)
    {
        // For variable data (ClearValues), we need to be careful.
        // We record the command, then the clearing values.
        RecordCommand(ERHICommandType::BeginRenderPass, RHICmdBeginRenderPass{
            desc.renderPass,
            desc.frameBuffer,
            desc.subpassContents,
            desc.clearValueCount
        }, desc.pClearValues, desc.clearValueCount * sizeof(RHIClearValue));
    }

    void RHICommandBuffer::EndRenderPass()
    {
        RecordCommand(ERHICommandType::EndRenderPass, RHICmdEndRenderPass{});
    }

    void RHICommandBuffer::BeginRendering(const RHIRenderingInfo& info)
    {
        // Deep serialization for RenderingInfo
        size_t colorSize = info.colorAttachmentCount * sizeof(RHIRenderingAttachmentInfo);
        size_t resolveSize = (info.pResolveAttachments != nullptr) ? (info.colorAttachmentCount * sizeof(RHIRenderingAttachmentInfo)) : 0;
        size_t depthSize = (info.pDepthAttachment != nullptr) ? sizeof(RHIRenderingAttachmentInfo) : 0;
        size_t stencilSize = (info.pStencilAttachment != nullptr) ? sizeof(RHIRenderingAttachmentInfo) : 0;
        
        UInt32 dynamicSize = static_cast<UInt32>(sizeof(RHIRenderingInfo) + colorSize + resolveSize + depthSize + stencilSize);

        // Calculate total size: Header + RHICmdBeginRendering + serialized data
        const size_t headerSize = sizeof(RHICmdHeader);
        const size_t cmdSize = sizeof(RHICmdBeginRendering);
        size_t currentSize = m_CommandStream.size();
        m_CommandStream.resize(currentSize + headerSize + cmdSize + dynamicSize);
        
        RHICmdHeader header{ ERHICommandType::BeginRendering };
        RHICmdBeginRendering cmd{ dynamicSize };
        
        uint8_t* ptr = m_CommandStream.data() + currentSize;
        std::memcpy(ptr, &header, headerSize); ptr += headerSize;
        std::memcpy(ptr, &cmd, cmdSize); ptr += cmdSize;
        
        // Copy the base info struct (pointers will be invalid but we'll fix them on Replay)
        uint8_t* infoBasePtr = ptr;
        std::memcpy(ptr, &info, sizeof(RHIRenderingInfo)); ptr += sizeof(RHIRenderingInfo);
        
        // Copy Attachment Arrays
        if (colorSize)   { std::memcpy(ptr, info.pColorAttachments, colorSize); ptr += colorSize; }
        if (resolveSize) { std::memcpy(ptr, info.pResolveAttachments, resolveSize); ptr += resolveSize; }
        if (depthSize)   { std::memcpy(ptr, info.pDepthAttachment, depthSize); ptr += depthSize; }
        if (stencilSize) { std::memcpy(ptr, info.pStencilAttachment, stencilSize); ptr += stencilSize; }
    }

    void RHICommandBuffer::EndRendering()
    {
        RecordCommand(ERHICommandType::EndRendering, RHICmdEndRendering{});
    }

    void RHICommandBuffer::Begin()
    {
        SetCurrentFrameIndex(0);
        RecordCommand(ERHICommandType::Begin, RHICmdBegin{ 0, 0, false });
        m_State = ECommandBufferState::Recording;
    }

    void RHICommandBuffer::Begin(UInt32 frameIndex, UInt32 commandBufferUsage, const RHICommandBufferInheritanceInfo* pInheritanceInfo)
    {
        SetCurrentFrameIndex(frameIndex);
        bool hasInheritance = (pInheritanceInfo != nullptr);
        RecordCommand(ERHICommandType::Begin, RHICmdBegin{ frameIndex, commandBufferUsage, hasInheritance }, 
            pInheritanceInfo, hasInheritance ? sizeof(RHICommandBufferInheritanceInfo) : 0);
        m_State = ECommandBufferState::Recording;
    }

    void RHICommandBuffer::End()
    {
        RecordCommand(ERHICommandType::End, RHICmdEnd{});
        m_State = ECommandBufferState::Executable;
    }

    void RHICommandBuffer::BindPipeline(RHIPipelineHandle pipeline)
    {
        RecordCommand(ERHICommandType::BindPipeline, RHICmdBindPipeline{ pipeline });
    }

    void RHICommandBuffer::Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance, UInt32 firstBinding)
    {
        RecordCommand(ERHICommandType::Draw, RHICmdDraw{ vertexCount, instanceCount, firstVertex, firstInstance, firstBinding });
    }

    void RHICommandBuffer::DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset, UInt32 firstInstance, UInt32 firstBinding)
    {
        RecordCommand(ERHICommandType::DrawIndexed, RHICmdDrawIndexed{ indexCount, instanceCount, firstIndex, vertexOffset, firstInstance, firstBinding });
    }

    void RHICommandBuffer::DrawIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride)
    {
        RecordCommand(ERHICommandType::DrawIndirect, RHICmdDrawIndirect{ buffer, offset, drawCount, stride });
    }

    void RHICommandBuffer::DrawIndexedIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride)
    {
        RecordCommand(ERHICommandType::DrawIndexedIndirect, RHICmdDrawIndexedIndirect{ buffer, offset, drawCount, stride });
    }
    
    void RHICommandBuffer::Dispatch(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ)
    {
        RecordCommand(ERHICommandType::Dispatch, RHICmdDispatch{ groupCountX, groupCountY, groupCountZ });
    }

    void RHICommandBuffer::DrawMeshTasks(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ)
    {
        RecordCommand(ERHICommandType::DrawMeshTasks, RHICmdDrawMeshTasks{ groupCountX, groupCountY, groupCountZ });
    }

    void RHICommandBuffer::BindVertexBuffers(RHIBufferHandle buffer, UInt64 offset)
    {
        RecordCommand(ERHICommandType::BindVertexBuffers, RHICmdBindVertexBuffers{ buffer, offset });
    }

    void RHICommandBuffer::BindIndexBuffer(RHIBufferHandle indexBuffer, UInt64 offset, EIndexType type)
    {
        RecordCommand(ERHICommandType::BindIndexBuffer, RHICmdBindIndexBuffer{ indexBuffer, offset, type });
    }

    void RHICommandBuffer::SetViewport(Float32 x, Float32 y, Float32 width, Float32 height, Float32 minDepth, Float32 maxDepth)
    {
        RecordCommand(ERHICommandType::SetViewport, RHICmdSetViewport{ x, y, width, height, minDepth, maxDepth });
    }

    void RHICommandBuffer::SetViewport(Float32 x, Float32 y, Float32 width, Float32 height)
    {
        SetViewport(x, y, width, height, 0.0f, 1.0f);
    }

    void RHICommandBuffer::SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width, UInt32 height)
    {
        RecordCommand(ERHICommandType::SetScissor, RHICmdSetScissor{ offsetX, offsetY, width, height });
    }

    void RHICommandBuffer::SetLineWidth(Float32 lineWidth)
    {
        RecordCommand(ERHICommandType::SetLineWidth, RHICmdSetLineWidth{ lineWidth });
    }

    void RHICommandBuffer::SetDepthBias(Float32 depthBiasConstantFactor, Float32 depthBiasClamp, Float32 depthBiasSlopeFactor)
    {
        RecordCommand(ERHICommandType::SetDepthBias, RHICmdSetDepthBias{ depthBiasConstantFactor, depthBiasClamp, depthBiasSlopeFactor });
    }

    void RHICommandBuffer::SetBlendConstants(const Float32 blendConstants[4])
    {
        RHICmdSetBlendConstants cmd;
        std::memcpy(cmd.blendConstants, blendConstants, sizeof(cmd.blendConstants));
        RecordCommand(ERHICommandType::SetBlendConstants, cmd);
    }

    void RHICommandBuffer::SetStencilReference(UInt32 faceMask, UInt32 reference)
    {
        RecordCommand(ERHICommandType::SetStencilReference, RHICmdSetStencilReference{ faceMask, reference });
    }

    void RHICommandBuffer::SetCullMode(ECullModeFlagBits cullMode)
    {
        RecordCommand(ERHICommandType::SetCullMode, RHICmdSetCullMode{ cullMode });
    }

    void RHICommandBuffer::SetFrontFace(EFrontFace frontFace)
    {
        RecordCommand(ERHICommandType::SetFrontFace, RHICmdSetFrontFace{ frontFace });
    }

    void RHICommandBuffer::SetPrimitiveTopology(EPrimitiveTopology topology)
    {
        RecordCommand(ERHICommandType::SetPrimitiveTopology, RHICmdSetPrimitiveTopology{ topology });
    }

    void RHICommandBuffer::SetDepthTestEnable(bool enable)
    {
        RecordCommand(ERHICommandType::SetDepthTestEnable, RHICmdSetDepthTestEnable{ enable });
    }
    
    void RHICommandBuffer::SetDepthWriteEnable(bool enable)
    {
        RecordCommand(ERHICommandType::SetDepthWriteEnable, RHICmdSetDepthWriteEnable{ enable });
    }

    void RHICommandBuffer::SetDepthCompareOp(ECompareOp depthCompareOp)
    {
        RecordCommand(ERHICommandType::SetDepthCompareOp, RHICmdSetDepthCompareOp{ depthCompareOp });
    }

    void RHICommandBuffer::SetStencilTestEnable(bool enable)
    {
        RecordCommand(ERHICommandType::SetStencilTestEnable, RHICmdSetStencilTestEnable{ enable });
    }

    void RHICommandBuffer::SetStencilOp(UInt32 faceMask, EStencilOp failOp, EStencilOp passOp, EStencilOp depthFailOp, ECompareOp compareOp)
    {
        RecordCommand(ERHICommandType::SetStencilOp, RHICmdSetStencilOp{ faceMask, failOp, passOp, depthFailOp, compareOp });
    }

    void RHICommandBuffer::CopyBuffer(RHIBufferHandle src, UInt64 srcOffset, RHIBufferHandle dst, UInt64 dstOffset, UInt64 size)
    {
        RecordCommand(ERHICommandType::CopyBuffer, RHICmdCopyBuffer{ src, srcOffset, dst, dstOffset, size });
    }
    
    void RHICommandBuffer::ExecuteCommands(Containers::Vector<RHICommandBuffer*>&& secondaryBuffers)
    {
        // TODO: Handle secondary buffers.
        // For now, not implemented.
    }
    
    void RHICommandBuffer::BindDescriptorSets(EPipelineBindPoint bindPoint, UInt32 firstSet, Containers::Vector<std::shared_ptr<RHIDescriptorSet>>& descriptorsets, UInt32 dynamicOffsetCount, const UInt32* pDynamicOffsets)
    {
        // This is the old path. We might want to deprecate or implement it.
        // Implement by serializing handles.
        // Note: vector of shared_ptrs ... complex for POD.
        // Assume simplified usage or just ignore for now if not used in core path (Bindless uses other overload).
    }

    void RHICommandBuffer::BindDescriptorSets(EPipelineBindPoint bindPoint, UInt32 firstSet, RHIDescriptorPoolHandle poolHandle, UInt32 poolId)
    {
        RecordCommand(ERHICommandType::BindDescriptorSets, RHICmdBindDescriptorSetsPool{ bindPoint, firstSet, poolHandle, poolId, 0, false });
    }

    void RHICommandBuffer::BindDescriptorSet(EPipelineBindPoint bindPoint, UInt32 firstSet, RHIDescriptorPoolHandle poolHandle, UInt32 poolId, UInt32 setIndex)
    {
        RecordCommand(ERHICommandType::BindDescriptorSets, RHICmdBindDescriptorSetsPool{ bindPoint, firstSet, poolHandle, poolId, setIndex, true });
    }

    void RHICommandBuffer::PushConstants(UInt32 offset, UInt32 size, const void* data, UInt32 stageFlags)
    {
        RecordCommand(ERHICommandType::PushConstants, RHICmdPushConstants{ offset, size, stageFlags }, data, size);
    }
    
    void RHICommandBuffer::CopyBufferToImage(RHIBufferHandle srcBuffer, RHIImageHandle dst, EImageLayout dstImageLayout, Containers::Vector<RHIBufferImageCopy>&& regions)
    {
        RecordCommand(ERHICommandType::CopyBufferToImage, 
            RHICmdCopyBufferToImage{ srcBuffer, dst, dstImageLayout, (UInt32)regions.size() },
            regions.data(), regions.size() * sizeof(RHIBufferImageCopy));
    }

    void RHICommandBuffer::TransitionImageLayout(RHIImageHandle image, EImageLayout targetLayout)
    {
        RecordCommand(ERHICommandType::TransitionImageLayout, RHICmdTransitionImageLayout{ image, IMAGE_LAYOUT_UNDEFINED, targetLayout });
    }

    void RHICommandBuffer::TransitionImageLayout(RHIImageHandle image, EImageLayout oldLayout, EImageLayout targetLayout)
    {
        RecordCommand(ERHICommandType::TransitionImageLayout, RHICmdTransitionImageLayout{ image, oldLayout, targetLayout });
    }
    
    void RHICommandBuffer::CopyImage(RHIImageHandle src, EImageLayout srcLayout, RHIImageHandle dst, EImageLayout dstLayout, UInt32 regionCount, const RHIImageCopy* pRegions)
    {
        RecordCommand(ERHICommandType::CopyImage,
             RHICmdCopyImage{ src, srcLayout, dst, dstLayout, regionCount },
             pRegions, regionCount * sizeof(RHIImageCopy));
    }
    
    void RHICommandBuffer::GenerateMipmaps(RHIImageHandle image)
    {
        RecordCommand(ERHICommandType::GenerateMipmaps, RHICmdGenerateMipmaps{ image });
    }
    
    void RHICommandBuffer::BuildAccelerationStructures(UInt32 infoCount, const RHIAccelerationStructureBuildGeometryInfo* pInfos, const RHIAccelerationStructureBuildRangeInfo* const* ppBuildRangeInfos)
    {
        // Calculate total size for serialization
        size_t totalSize = 0;
        for (UInt32 i = 0; i < infoCount; ++i)
        {
            totalSize += sizeof(RHIAccelerationStructureBuildGeometryInfo);
            totalSize += pInfos[i].geometryCount * sizeof(RHIAccelerationStructureGeometryData);
            totalSize += pInfos[i].geometryCount * sizeof(RHIAccelerationStructureBuildRangeInfo);
        }

        const size_t headerSize = sizeof(RHICmdHeader);
        const size_t cmdSize = sizeof(RHICmdBuildAccelerationStructures);
        size_t currentSize = m_CommandStream.size();
        m_CommandStream.resize(currentSize + headerSize + cmdSize + totalSize);

        RHICmdHeader header{ ERHICommandType::BuildAccelerationStructures };
        RHICmdBuildAccelerationStructures cmd{ infoCount, (UInt32)totalSize };

        uint8_t* ptr = m_CommandStream.data() + currentSize;
        std::memcpy(ptr, &header, headerSize); ptr += headerSize;
        std::memcpy(ptr, &cmd, cmdSize); ptr += cmdSize;

        for (UInt32 i = 0; i < infoCount; ++i)
        {
            // Copy Info
            std::memcpy(ptr, &pInfos[i], sizeof(RHIAccelerationStructureBuildGeometryInfo));
            ptr += sizeof(RHIAccelerationStructureBuildGeometryInfo);

            // Copy Geometries
            size_t geomsSize = pInfos[i].geometryCount * sizeof(RHIAccelerationStructureGeometryData);
            std::memcpy(ptr, pInfos[i].pGeometries, geomsSize);
            ptr += geomsSize;

            // Copy Ranges
            size_t rangesSize = pInfos[i].geometryCount * sizeof(RHIAccelerationStructureBuildRangeInfo);
            std::memcpy(ptr, ppBuildRangeInfos[i], rangesSize);
            ptr += rangesSize;
        }
    }
    
    void RHICommandBuffer::TraceRays(const RHITraceRaysDescriptor& desc)
    {
        RecordCommand(ERHICommandType::TraceRays, RHICmdTraceRays{ desc });
    }

    void RHICommandBuffer::SetFragmentShadingRate(EShadingRate rate, EShadingRateCombiner combinerOp[2])
    {
        RecordCommand(ERHICommandType::SetFragmentShadingRate, RHICmdSetFragmentShadingRate{ rate, {combinerOp[0], combinerOp[1]} });
    }

    void RHICommandBuffer::BeginDebugLabel(const char* label, const Float32 color[4])
    {
        UInt32 len = (UInt32)strlen(label) + 1;
        Float32 defaultColor[4] = { 1.0f, 1.0f, 1.0f, 1.0f };
        const Float32* pColor = color ? color : defaultColor;
        RecordCommand(ERHICommandType::BeginDebugLabel, RHICmdBeginDebugLabel{ {pColor[0], pColor[1], pColor[2], pColor[3]}, len }, label, len);
    }

    void RHICommandBuffer::EndDebugLabel()
    {
         RecordCommand(ERHICommandType::EndDebugLabel, RHICmdEndDebugLabel{});
    }

    void RHICommandBuffer::InsertDebugMarker(const char* label, const Float32 color[4])
    {
        UInt32 len = (UInt32)strlen(label) + 1;
        Float32 defaultColor[4] = { 1.0f, 1.0f, 1.0f, 1.0f };
        const Float32* pColor = color ? color : defaultColor;
        RecordCommand(ERHICommandType::InsertDebugMarker, RHICmdInsertDebugMarker{ {pColor[0], pColor[1], pColor[2], pColor[3]}, len }, label, len);
    }
    
    // PipelineBarrier requires special handling for 3 vectors.
    void RHICommandBuffer::PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
            const RHIMemoryBarrier* pMemoryBarriers, UInt32 memoryBarrierCount,
            const RHIImageMemoryBarrier* pImageMemoryBarriers, UInt32 imageMemoryBarrierCount,
            const RHIBufferMemoryBarrier* pBufferMemoryBarriers, UInt32 bufferMemoryBarrierCount)
    {
        // Combine all data into one stream append?
        // Header -> MemoryBarriers -> ImageBarriers -> BufferBarriers
        
        const size_t headerSize = sizeof(RHICmdHeader);
        const size_t cmdSize = sizeof(RHICmdPipelineBarrier);
        const size_t memSize = memoryBarrierCount * sizeof(RHIMemoryBarrier);
        const size_t imgSize = imageMemoryBarrierCount * sizeof(RHIImageMemoryBarrier);
        const size_t bufSize = bufferMemoryBarrierCount * sizeof(RHIBufferMemoryBarrier);
        
        size_t currentSize = m_CommandStream.size();
        m_CommandStream.resize(currentSize + headerSize + cmdSize + memSize + imgSize + bufSize);
        
        RHICmdHeader header{ERHICommandType::PipelineBarrier};
        RHICmdPipelineBarrier cmd{ srcStage, dstStage, dependency, memoryBarrierCount, imageMemoryBarrierCount, bufferMemoryBarrierCount };
        
        uint8_t* ptr = m_CommandStream.data() + currentSize;
        std::memcpy(ptr, &header, headerSize); ptr += headerSize;
        std::memcpy(ptr, &cmd, cmdSize); ptr += cmdSize;
        if (memSize) { std::memcpy(ptr, pMemoryBarriers, memSize); ptr += memSize; }
        if (imgSize) { std::memcpy(ptr, pImageMemoryBarriers, imgSize); ptr += imgSize; }
        if (bufSize) { std::memcpy(ptr, pBufferMemoryBarriers, bufSize); ptr += bufSize; }
    }
    
    void RHICommandBuffer::PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
                        const RHIMemoryBarrier* pMemoryBarriers, UInt32 memoryBarrierCount)
    {
        PipelineBarrier(srcStage, dstStage, dependency, pMemoryBarriers, memoryBarrierCount, nullptr, 0, nullptr, 0);
    }

    void RHICommandBuffer::PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
                        const RHIImageMemoryBarrier* pImageMemoryBarriers, UInt32 imageMemoryBarrierCount)
    {
         PipelineBarrier(srcStage, dstStage, dependency, nullptr, 0, pImageMemoryBarriers, imageMemoryBarrierCount, nullptr, 0);
    }

    void RHICommandBuffer::PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
                        const RHIBufferMemoryBarrier* pBufferMemoryBarriers, UInt32 bufferMemoryBarrierCount)
    {
         PipelineBarrier(srcStage, dstStage, dependency, nullptr, 0, nullptr, 0, pBufferMemoryBarriers, bufferMemoryBarrierCount);
    }


}
