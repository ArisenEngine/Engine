#include "RHICommandBuffer.h"
#include "IRHICommandExecutor.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "Logger/Logger.h"
#include "Profiler.h"

namespace ArisenEngine::RHI
{
    RHICommandBuffer::RHICommandBuffer(RHIDevice* device, RHICommandBufferPool* pool, ECommandBufferLevel level)
        : m_Device(device), m_CommandBufferPool(pool), m_Level(level), m_State(ECommandBufferState::Initial)
    {
    }

    RHICommandBuffer::~RHICommandBuffer() noexcept
    {
    }

    void RHICommandBuffer::BeginRenderPass(RenderPassBeginDesc&& desc)
    {
        ARISEN_PROFILE_ZONE("RHI::BeginRenderPass");
        RecordCommand<RHICmdBeginRenderPass>(ERHICommandType::BeginRenderPass,
                                             {
                                                 desc.renderPass, desc.frameBuffer, desc.subpassContents,
                                                 desc.clearValueCount
                                             },
                                             desc.pClearValues, desc.clearValueCount * sizeof(RHIClearValue));
    }

    void RHICommandBuffer::EndRenderPass()
    {
        ARISEN_PROFILE_ZONE("RHI::EndRenderPass");
        RecordCommand<RHICmdEndRenderPass>(ERHICommandType::EndRenderPass, {});
    }

    void RHICommandBuffer::BeginRendering(const RHIRenderingInfo& info)
    {
        ARISEN_PROFILE_ZONE("RHI::BeginRendering");
        // RenderingInfo has nested pointers. We need to serialize everything.
        size_t totalExtraSize = sizeof(RHIRenderingInfo);
        totalExtraSize += info.colorAttachmentCount * sizeof(RHIRenderingAttachmentInfo);
        if (info.pResolveAttachments) totalExtraSize += info.colorAttachmentCount * sizeof(RHIRenderingAttachmentInfo);
        if (info.pDepthAttachment) totalExtraSize += sizeof(RHIRenderingAttachmentInfo);
        if (info.pStencilAttachment) totalExtraSize += sizeof(RHIRenderingAttachmentInfo);

        const size_t headerSize = sizeof(RHICmdHeader);
        const size_t cmdSize = sizeof(RHICmdBeginRendering);
        size_t currentSize = m_CommandStream.size();
        m_CommandStream.resize(currentSize + headerSize + cmdSize + totalExtraSize);

        RHICmdHeader header{ERHICommandType::BeginRendering};
        RHICmdBeginRendering cmd{(UInt32)totalExtraSize};

        std::memcpy(m_CommandStream.data() + currentSize, &header, headerSize);
        std::memcpy(m_CommandStream.data() + currentSize + headerSize, &cmd, cmdSize);

        uint8_t* pDest = m_CommandStream.data() + currentSize + headerSize + cmdSize;
        std::memcpy(pDest, &info, sizeof(RHIRenderingInfo));
        pDest += sizeof(RHIRenderingInfo);

        if (info.colorAttachmentCount > 0)
        {
            std::memcpy(pDest, info.pColorAttachments, info.colorAttachmentCount * sizeof(RHIRenderingAttachmentInfo));
            pDest += info.colorAttachmentCount * sizeof(RHIRenderingAttachmentInfo);
        }

        if (info.pResolveAttachments)
        {
            std::memcpy(pDest, info.pResolveAttachments,
                        info.colorAttachmentCount * sizeof(RHIRenderingAttachmentInfo));
            pDest += info.colorAttachmentCount * sizeof(RHIRenderingAttachmentInfo);
        }

        if (info.pDepthAttachment)
        {
            std::memcpy(pDest, info.pDepthAttachment, sizeof(RHIRenderingAttachmentInfo));
            pDest += sizeof(RHIRenderingAttachmentInfo);
        }

        if (info.pStencilAttachment)
        {
            std::memcpy(pDest, info.pStencilAttachment, sizeof(RHIRenderingAttachmentInfo));
            pDest += sizeof(RHIRenderingAttachmentInfo);
        }
    }

    void RHICommandBuffer::EndRendering()
    {
        ARISEN_PROFILE_ZONE("RHI::EndRendering");
        RecordCommand<RHICmdEndRendering>(ERHICommandType::EndRendering, {});
    }

    void RHICommandBuffer::Begin(UInt32 frameIndex, UInt32 commandBufferUsage,
                                 const RHICommandBufferInheritanceInfo* pInheritanceInfo)
    {
        SetCurrentFrameIndex(frameIndex);
        RHICmdBegin cmd{frameIndex, commandBufferUsage, pInheritanceInfo != nullptr};
        if (pInheritanceInfo)
        {
            RecordCommand<RHICmdBegin>(ERHICommandType::Begin, cmd, pInheritanceInfo,
                                       sizeof(RHICommandBufferInheritanceInfo));
        }
        else
        {
            RecordCommand<RHICmdBegin>(ERHICommandType::Begin, cmd);
        }
        m_State = ECommandBufferState::Recording;
        ARISEN_PROFILE_ZONE("RHI::CommandBufferRecording");
    }

    void RHICommandBuffer::End()
    {
        ARISEN_PROFILE_ZONE("RHI::CommandBufferRecordingEnd");
        RecordCommand<RHICmdEnd>(ERHICommandType::End, {});
        m_State = ECommandBufferState::Executable;
    }

    void RHICommandBuffer::SetViewport(Float32 x, Float32 y, Float32 width, Float32 height, Float32 minDepth,
                                       Float32 maxDepth)
    {
        RecordCommand<RHICmdSetViewport>(ERHICommandType::SetViewport, {x, y, width, height, minDepth, maxDepth});
    }

    void RHICommandBuffer::SetViewport(Float32 x, Float32 y, Float32 width, Float32 height)
    {
        SetViewport(x, y, width, height, 0.0f, 1.0f);
    }

    void RHICommandBuffer::SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width, UInt32 height)
    {
        RecordCommand<RHICmdSetScissor>(ERHICommandType::SetScissor, {offsetX, offsetY, width, height});
    }

    void RHICommandBuffer::SetLineWidth(Float32 lineWidth)
    {
        RecordCommand<RHICmdSetLineWidth>(ERHICommandType::SetLineWidth, {lineWidth});
    }

    void RHICommandBuffer::SetDepthBias(Float32 depthBiasConstantFactor, Float32 depthBiasClamp,
                                        Float32 depthBiasSlopeFactor)
    {
        RecordCommand<RHICmdSetDepthBias>(ERHICommandType::SetDepthBias,
                                          {depthBiasConstantFactor, depthBiasClamp, depthBiasSlopeFactor});
    }

    void RHICommandBuffer::SetBlendConstants(const Float32 blendConstants[4])
    {
        RHICmdSetBlendConstants cmd;
        std::memcpy(cmd.blendConstants, blendConstants, sizeof(float) * 4);
        RecordCommand<RHICmdSetBlendConstants>(ERHICommandType::SetBlendConstants, cmd);
    }

    void RHICommandBuffer::SetStencilReference(UInt32 faceMask, UInt32 reference)
    {
        RecordCommand<RHICmdSetStencilReference>(ERHICommandType::SetStencilReference, {faceMask, reference});
    }

    void RHICommandBuffer::SetCullMode(ECullModeFlagBits cullMode)
    {
        RecordCommand<RHICmdSetCullMode>(ERHICommandType::SetCullMode, {cullMode});
    }

    void RHICommandBuffer::SetFrontFace(EFrontFace frontFace)
    {
        RecordCommand<RHICmdSetFrontFace>(ERHICommandType::SetFrontFace, {frontFace});
    }

    void RHICommandBuffer::SetPrimitiveTopology(EPrimitiveTopology topology)
    {
        RecordCommand<RHICmdSetPrimitiveTopology>(ERHICommandType::SetPrimitiveTopology, {topology});
    }

    void RHICommandBuffer::SetDepthTestEnable(bool enable)
    {
        RecordCommand<RHICmdSetDepthTestEnable>(ERHICommandType::SetDepthTestEnable, {enable});
    }

    void RHICommandBuffer::SetDepthWriteEnable(bool enable)
    {
        RecordCommand<RHICmdSetDepthWriteEnable>(ERHICommandType::SetDepthWriteEnable, {enable});
    }

    void RHICommandBuffer::SetDepthCompareOp(ECompareOp depthCompareOp)
    {
        RecordCommand<RHICmdSetDepthCompareOp>(ERHICommandType::SetDepthCompareOp, {depthCompareOp});
    }

    void RHICommandBuffer::SetStencilTestEnable(bool enable)
    {
        RecordCommand<RHICmdSetStencilTestEnable>(ERHICommandType::SetStencilTestEnable, {enable});
    }

    void RHICommandBuffer::SetStencilOp(UInt32 faceMask, EStencilOp failOp, EStencilOp passOp, EStencilOp depthFailOp,
                                        ECompareOp compareOp)
    {
        RecordCommand<RHICmdSetStencilOp>(ERHICommandType::SetStencilOp,
                                          {faceMask, failOp, passOp, depthFailOp, compareOp});
    }

    void RHICommandBuffer::BindPipeline(RHIPipelineHandle pipeline)
    {
        ARISEN_PROFILE_ZONE("RHI::BindPipeline");
        RecordCommand<RHICmdBindPipeline>(ERHICommandType::BindPipeline, {pipeline});
    }

    void RHICommandBuffer::Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance,
                                UInt32 firstBinding)
    {
        ARISEN_PROFILE_ZONE("RHI::Draw");
        RecordCommand<RHICmdDraw>(ERHICommandType::Draw, {
                                      vertexCount, instanceCount, firstVertex, firstInstance, firstBinding
                                  });
    }

    void RHICommandBuffer::DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset,
                                       UInt32 firstInstance, UInt32 firstBinding)
    {
        ARISEN_PROFILE_ZONE("RHI::DrawIndexed");
        RecordCommand<RHICmdDrawIndexed>(ERHICommandType::DrawIndexed, {
                                             indexCount, instanceCount, firstIndex, vertexOffset, firstInstance,
                                             firstBinding
                                         });
    }

    void RHICommandBuffer::DrawIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride)
    {
        RecordCommand<RHICmdDrawIndirect>(ERHICommandType::DrawIndirect, {buffer, offset, drawCount, stride});
    }

    void RHICommandBuffer::DrawIndexedIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride)
    {
        RecordCommand<RHICmdDrawIndexedIndirect>(ERHICommandType::DrawIndexedIndirect,
                                                 {buffer, offset, drawCount, stride});
    }

    void RHICommandBuffer::DrawMeshTasks(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ)
    {
        RecordCommand<RHICmdDrawMeshTasks>(ERHICommandType::DrawMeshTasks, {groupCountX, groupCountY, groupCountZ});
    }

    void RHICommandBuffer::Dispatch(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ)
    {
        ARISEN_PROFILE_ZONE("RHI::Dispatch");
        RecordCommand<RHICmdDispatch>(ERHICommandType::Dispatch, {groupCountX, groupCountY, groupCountZ});
    }

    void RHICommandBuffer::BindVertexBuffers(RHIBufferHandle buffer, UInt64 offset)
    {
        RecordCommand<RHICmdBindVertexBuffers>(ERHICommandType::BindVertexBuffers, {buffer, offset});
    }

    void RHICommandBuffer::BindIndexBuffer(RHIBufferHandle indexBuffer, UInt64 offset, EIndexType type)
    {
        RecordCommand<RHICmdBindIndexBuffer>(ERHICommandType::BindIndexBuffer, {indexBuffer, offset, type});
    }

    void RHICommandBuffer::CopyBuffer(RHIBufferHandle src, UInt64 srcOffset, RHIBufferHandle dst, UInt64 dstOffset,
                                      UInt64 size)
    {
        RecordCommand<RHICmdCopyBuffer>(ERHICommandType::CopyBuffer, {src, srcOffset, dst, dstOffset, size});
    }

    void RHICommandBuffer::BindDescriptorSets(EPipelineBindPoint bindPoint, UInt32 firstSet,
                                              RHIDescriptorPoolHandle poolHandle, UInt32 poolId)
    {
        RecordCommand<RHICmdBindDescriptorSetsPool>(ERHICommandType::BindDescriptorSets,
                                                    {bindPoint, firstSet, poolHandle, poolId, 0, false});
    }

    void RHICommandBuffer::BindDescriptorSet(EPipelineBindPoint bindPoint, UInt32 firstSet,
                                             RHIDescriptorPoolHandle poolHandle, UInt32 poolId, UInt32 setIndex)
    {
        RecordCommand<RHICmdBindDescriptorSetsPool>(ERHICommandType::BindDescriptorSets,
                                                    {bindPoint, firstSet, poolHandle, poolId, setIndex, true});
    }

    void RHICommandBuffer::PushConstants(UInt32 offset, UInt32 size, const void* data, UInt32 stageFlags)
    {
        RecordCommand<RHICmdPushConstants>(ERHICommandType::PushConstants, {offset, size, stageFlags}, data, size);
    }

    void RHICommandBuffer::BindDescriptorBuffers(Containers::Vector<RHIBufferHandle>&& buffers)
    {
        RecordCommand<RHICmdBindDescriptorBuffers>(ERHICommandType::BindDescriptorBuffers, {(UInt32)buffers.size()},
                                                   buffers.data(), buffers.size() * sizeof(RHIBufferHandle));
    }

    void RHICommandBuffer::SetDescriptorBufferOffsets(EPipelineBindPoint bindPoint, RHIPipelineHandle pipeline,
                                                      UInt32 firstSet,
                                                      Containers::Vector<UInt32>&& bufferIndices,
                                                      Containers::Vector<UInt64>&& offsets)
    {
        UInt32 setCount = (UInt32)bufferIndices.size();
        RHICmdSetDescriptorBufferOffsets cmd{bindPoint, pipeline, firstSet, setCount};

        const size_t headerSize = sizeof(RHICmdHeader);
        const size_t cmdSize = sizeof(RHICmdSetDescriptorBufferOffsets);
        const size_t extraSize = (setCount * sizeof(UInt32)) + (setCount * sizeof(UInt64));

        size_t currentSize = m_CommandStream.size();
        m_CommandStream.resize(currentSize + headerSize + cmdSize + extraSize);

        RHICmdHeader header{ERHICommandType::SetDescriptorBufferOffsets};
        std::memcpy(m_CommandStream.data() + currentSize, &header, headerSize);
        std::memcpy(m_CommandStream.data() + currentSize + headerSize, &cmd, cmdSize);

        uint8_t* pDest = m_CommandStream.data() + currentSize + headerSize + cmdSize;
        if (setCount > 0)
        {
            std::memcpy(pDest, bufferIndices.data(), setCount * sizeof(UInt32));
            pDest += setCount * sizeof(UInt32);
            std::memcpy(pDest, offsets.data(), setCount * sizeof(UInt64));
        }
    }

    void RHICommandBuffer::CopyBufferToImage(RHIBufferHandle srcBuffer, RHIImageHandle dst, EImageLayout dstImageLayout,
                                             Containers::Vector<RHIBufferImageCopy>&& regions)
    {
        RecordCommand<RHICmdCopyBufferToImage>(ERHICommandType::CopyBufferToImage,
                                               {srcBuffer, dst, dstImageLayout, (UInt32)regions.size()},
                                               regions.data(), regions.size() * sizeof(RHIBufferImageCopy));
    }

    void RHICommandBuffer::PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
                                           const RHIMemoryBarrier* pMemoryBarriers, UInt32 memoryBarrierCount,
                                           const RHIImageMemoryBarrier* pImageMemoryBarriers,
                                           UInt32 imageMemoryBarrierCount,
                                           const RHIBufferMemoryBarrier* pBufferMemoryBarriers,
                                           UInt32 bufferMemoryBarrierCount)
    {
        ARISEN_PROFILE_ZONE("RHI::PipelineBarrier");
        RHICmdPipelineBarrier cmd{
            srcStage, dstStage, dependency, memoryBarrierCount, imageMemoryBarrierCount, bufferMemoryBarrierCount
        };

        const size_t headerSize = sizeof(RHICmdHeader);
        const size_t cmdSize = sizeof(RHICmdPipelineBarrier);
        const size_t extraSize = (memoryBarrierCount * sizeof(RHIMemoryBarrier)) +
            (imageMemoryBarrierCount * sizeof(RHIImageMemoryBarrier)) +
            (bufferMemoryBarrierCount * sizeof(RHIBufferMemoryBarrier));

        size_t currentSize = m_CommandStream.size();
        m_CommandStream.resize(currentSize + headerSize + cmdSize + extraSize);

        RHICmdHeader header{ERHICommandType::PipelineBarrier};
        std::memcpy(m_CommandStream.data() + currentSize, &header, headerSize);
        std::memcpy(m_CommandStream.data() + currentSize + headerSize, &cmd, cmdSize);

        uint8_t* pDest = m_CommandStream.data() + currentSize + headerSize + cmdSize;
        if (memoryBarrierCount > 0)
        {
            std::memcpy(pDest, pMemoryBarriers, memoryBarrierCount * sizeof(RHIMemoryBarrier));
            pDest += memoryBarrierCount * sizeof(RHIMemoryBarrier);
        }
        if (imageMemoryBarrierCount > 0)
        {
            std::memcpy(pDest, pImageMemoryBarriers, imageMemoryBarrierCount * sizeof(RHIImageMemoryBarrier));
            pDest += imageMemoryBarrierCount * sizeof(RHIImageMemoryBarrier);
        }
        if (bufferMemoryBarrierCount > 0)
        {
            std::memcpy(pDest, pBufferMemoryBarriers, bufferMemoryBarrierCount * sizeof(RHIBufferMemoryBarrier));
        }
    }

    void RHICommandBuffer::PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
                                           const RHIMemoryBarrier* pMemoryBarriers, UInt32 memoryBarrierCount)
    {
        PipelineBarrier(srcStage, dstStage, dependency, pMemoryBarriers, memoryBarrierCount, nullptr, 0, nullptr, 0);
    }

    void RHICommandBuffer::PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
                                           const RHIImageMemoryBarrier* pImageMemoryBarriers,
                                           UInt32 imageMemoryBarrierCount)
    {
        PipelineBarrier(srcStage, dstStage, dependency, nullptr, 0, pImageMemoryBarriers, imageMemoryBarrierCount,
                        nullptr, 0);
    }

    void RHICommandBuffer::PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
                                           const RHIBufferMemoryBarrier* pBufferMemoryBarriers,
                                           UInt32 bufferMemoryBarrierCount)
    {
        PipelineBarrier(srcStage, dstStage, dependency, nullptr, 0, nullptr, 0, pBufferMemoryBarriers,
                        bufferMemoryBarrierCount);
    }

    void RHICommandBuffer::TransitionImageLayout(RHIImageHandle image, EImageLayout targetLayout)
    {
        TransitionImageLayout(image, IMAGE_LAYOUT_UNDEFINED, targetLayout);
    }

    void RHICommandBuffer::TransitionImageLayout(RHIImageHandle image, EImageLayout oldLayout,
                                                 EImageLayout targetLayout)
    {
        RecordCommand<RHICmdTransitionImageLayout>(ERHICommandType::TransitionImageLayout,
                                                   {image, oldLayout, targetLayout});
    }

    void RHICommandBuffer::CopyImage(RHIImageHandle src, EImageLayout srcLayout, RHIImageHandle dst,
                                     EImageLayout dstLayout, UInt32 regionCount, const RHIImageCopy* pRegions)
    {
        RecordCommand<RHICmdCopyImage>(ERHICommandType::CopyImage,
                                       {src, srcLayout, dst, dstLayout, regionCount},
                                       pRegions, regionCount * sizeof(RHIImageCopy));
    }

    void RHICommandBuffer::GenerateMipmaps(RHIImageHandle image)
    {
        RecordCommand<RHICmdGenerateMipmaps>(ERHICommandType::GenerateMipmaps, {image});
    }

    void RHICommandBuffer::BuildAccelerationStructures(UInt32 infoCount,
                                                       const RHIAccelerationStructureBuildGeometryInfo* pInfos,
                                                       const RHIAccelerationStructureBuildRangeInfo* const*
                                                       ppBuildRangeInfos)
    {
        // This is complex - calculate total size first
        size_t totalDataSize = 0;
        for (UInt32 i = 0; i < infoCount; ++i)
        {
            totalDataSize += sizeof(RHIAccelerationStructureBuildGeometryInfo);
            totalDataSize += pInfos[i].geometryCount * sizeof(RHIAccelerationStructureGeometryData);
            totalDataSize += pInfos[i].geometryCount * sizeof(RHIAccelerationStructureBuildRangeInfo);
        }

        RHICmdBuildAccelerationStructures cmd{infoCount, (UInt32)totalDataSize};

        const size_t headerSize = sizeof(RHICmdHeader);
        const size_t cmdSize = sizeof(RHICmdBuildAccelerationStructures);
        size_t currentSize = m_CommandStream.size();
        m_CommandStream.resize(currentSize + headerSize + cmdSize + totalDataSize);

        RHICmdHeader header{ERHICommandType::BuildAccelerationStructures};
        std::memcpy(m_CommandStream.data() + currentSize, &header, headerSize);
        std::memcpy(m_CommandStream.data() + currentSize + headerSize, &cmd, cmdSize);

        uint8_t* pDest = m_CommandStream.data() + currentSize + headerSize + cmdSize;
        for (UInt32 i = 0; i < infoCount; ++i)
        {
            // Copy Info
            std::memcpy(pDest, &pInfos[i], sizeof(RHIAccelerationStructureBuildGeometryInfo));
            pDest += sizeof(RHIAccelerationStructureBuildGeometryInfo);

            // Copy Geometry Data
            std::memcpy(pDest, pInfos[i].pGeometries,
                        pInfos[i].geometryCount * sizeof(RHIAccelerationStructureGeometryData));
            pDest += pInfos[i].geometryCount * sizeof(RHIAccelerationStructureGeometryData);

            // Copy Range Data
            std::memcpy(pDest, ppBuildRangeInfos[i],
                        pInfos[i].geometryCount * sizeof(RHIAccelerationStructureBuildRangeInfo));
            pDest += pInfos[i].geometryCount * sizeof(RHIAccelerationStructureBuildRangeInfo);
        }
    }

    void RHICommandBuffer::TraceRays(const RHITraceRaysDescriptor& desc)
    {
        ARISEN_PROFILE_ZONE("RHI::TraceRays");
        RecordCommand<RHICmdTraceRays>(ERHICommandType::TraceRays, {desc});
    }

    void RHICommandBuffer::SetFragmentShadingRate(EShadingRate rate, EShadingRateCombiner combinerOp[2])
    {
        RHICmdSetFragmentShadingRate cmd;
        cmd.rate = rate;
        cmd.combinerOp[0] = combinerOp[0];
        cmd.combinerOp[1] = combinerOp[1];
        RecordCommand<RHICmdSetFragmentShadingRate>(ERHICommandType::SetFragmentShadingRate, cmd);
    }

    void RHICommandBuffer::BeginDebugLabel(const char* label, const Float32 color[4])
    {
        UInt32 len = (UInt32)std::strlen(label) + 1;
        RHICmdBeginDebugLabel cmd;
        if (color)
        {
            std::memcpy(cmd.color, color, sizeof(float) * 4);
        }
        else
        {
            float defaultColor[4] = {1.0f, 1.0f, 1.0f, 1.0f};
            std::memcpy(cmd.color, defaultColor, sizeof(float) * 4);
        }
        cmd.labelLen = len;
        RecordCommand<RHICmdBeginDebugLabel>(ERHICommandType::BeginDebugLabel, cmd, label, len);
    }

    void RHICommandBuffer::EndDebugLabel()
    {
        RecordCommand<RHICmdEndDebugLabel>(ERHICommandType::EndDebugLabel, {});
    }

    void RHICommandBuffer::InsertDebugMarker(const char* label, const Float32 color[4])
    {
        UInt32 len = (UInt32)std::strlen(label) + 1;
        RHICmdInsertDebugMarker cmd;
        if (color)
        {
            std::memcpy(cmd.color, color, sizeof(float) * 4);
        }
        else
        {
            float defaultColor[4] = {1.0f, 1.0f, 1.0f, 1.0f};
            std::memcpy(cmd.color, defaultColor, sizeof(float) * 4);
        }
        cmd.labelLen = len;
        RecordCommand<RHICmdInsertDebugMarker>(ERHICommandType::InsertDebugMarker, cmd, label, len);
    }

    void RHICommandBuffer::ExecuteCommands(Containers::Vector<RHICommandBuffer*>&& secondaryBuffers)
    {
        // Not implemented (needs handles or other serialization approach)
    }

    void RHICommandBuffer::BindDescriptorSets(EPipelineBindPoint bindPoint, UInt32 firstSet,
                                              Containers::Vector<std::shared_ptr<RHIDescriptorSet>>& descriptorsets,
                                              UInt32 dynamicOffsetCount, const UInt32* pDynamicOffsets)
    {
        // Not implemented (legacy path)
    }

    void RHICommandBuffer::Replay(IRHICommandExecutor& executor)
    {
        ARISEN_PROFILE_ZONE("RHI::CommandBufferReplay");
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
                        info.pColorAttachments = reinterpret_cast<const RHIRenderingAttachmentInfo*>(m_CommandStream.
                            data() + offset);
                        offset += info.colorAttachmentCount * sizeof(RHIRenderingAttachmentInfo);
                    }

                    if (info.pResolveAttachments != nullptr)
                    {
                        info.pResolveAttachments = reinterpret_cast<const RHIRenderingAttachmentInfo*>(m_CommandStream.
                            data() + offset);
                        offset += info.colorAttachmentCount * sizeof(RHIRenderingAttachmentInfo);
                    }

                    if (info.pDepthAttachment != nullptr)
                    {
                        info.pDepthAttachment = reinterpret_cast<const RHIRenderingAttachmentInfo*>(m_CommandStream.
                            data() + offset);
                        offset += sizeof(RHIRenderingAttachmentInfo);
                    }

                    if (info.pStencilAttachment != nullptr)
                    {
                        info.pStencilAttachment = reinterpret_cast<const RHIRenderingAttachmentInfo*>(m_CommandStream.
                            data() + offset);
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
                        pInheritanceInfo = reinterpret_cast<const RHICommandBufferInheritanceInfo*>(m_CommandStream.
                            data() + offset);
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
                    executor.Draw(cmd->vertexCount, cmd->instanceCount, cmd->firstVertex, cmd->firstInstance,
                                  cmd->firstBinding);
                    break;
                }
            case ERHICommandType::DrawIndexed:
                {
                    const auto* cmd = reinterpret_cast<const RHICmdDrawIndexed*>(m_CommandStream.data() + offset);
                    offset += sizeof(RHICmdDrawIndexed);
                    executor.DrawIndexed(cmd->indexCount, cmd->instanceCount, cmd->firstIndex, cmd->vertexOffset,
                                         cmd->firstInstance, cmd->firstBinding);
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
                    const auto* cmd = reinterpret_cast<const RHICmdDrawIndexedIndirect*>(m_CommandStream.data() +
                        offset);
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
            case ERHICommandType::BindIndexBuffer:
                {
                    const auto* cmd = reinterpret_cast<const RHICmdBindIndexBuffer*>(m_CommandStream.data() + offset);
                    offset += sizeof(RHICmdBindIndexBuffer);
                    executor.BindIndexBuffer(cmd->indexBuffer, cmd->offset, cmd->type);
                    break;
                }
            case ERHICommandType::BindDescriptorSets:
                {
                    const auto* cmd = reinterpret_cast<const RHICmdBindDescriptorSetsPool*>(m_CommandStream.data() +
                        offset);
                    offset += sizeof(RHICmdBindDescriptorSetsPool);
                    executor.BindDescriptorSets(cmd->bindPoint, cmd->firstSet, cmd->poolHandle, cmd->poolId,
                                                cmd->setIndex, cmd->isSingleSet);
                    break;
                }
            case ERHICommandType::BindDescriptorBuffers:
                {
                    const auto* cmd = reinterpret_cast<const RHICmdBindDescriptorBuffers*>(m_CommandStream.data() +
                        offset);
                    offset += sizeof(RHICmdBindDescriptorBuffers);
                    const auto* pBuffers = reinterpret_cast<const RHIBufferHandle*>(m_CommandStream.data() + offset);
                    offset += cmd->bufferCount * sizeof(RHIBufferHandle);

                    executor.BindDescriptorBuffers(cmd->bufferCount, pBuffers);
                    break;
                }
            case ERHICommandType::SetDescriptorBufferOffsets:
                {
                    const auto* cmd = reinterpret_cast<const RHICmdSetDescriptorBufferOffsets*>(m_CommandStream.data() +
                        offset);
                    offset += sizeof(RHICmdSetDescriptorBufferOffsets);
                    const auto* pIndices = reinterpret_cast<const UInt32*>(m_CommandStream.data() + offset);
                    offset += cmd->setCount * sizeof(UInt32);
                    const auto* pOffsets = reinterpret_cast<const UInt64*>(m_CommandStream.data() + offset);
                    offset += cmd->setCount * sizeof(UInt64);

                    executor.SetDescriptorBufferOffsets(cmd->bindPoint, cmd->pipeline, cmd->firstSet, cmd->setCount,
                                                        pIndices, pOffsets);
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
                    executor.CopyBufferToImage(cmd->srcBuffer, cmd->dst, cmd->dstImageLayout, cmd->regionCount,
                                               pRegions);
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
                    const auto* cmd = reinterpret_cast<const RHICmdTransitionImageLayout*>(m_CommandStream.data() +
                        offset);
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
                    const auto* cmd = reinterpret_cast<const RHICmdBuildAccelerationStructures*>(m_CommandStream.data()
                        + offset);
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
                        infos[i].pGeometries = reinterpret_cast<const RHIAccelerationStructureGeometryData*>(
                            currentPtr);
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
                    const auto* cmd = reinterpret_cast<const RHICmdSetFragmentShadingRate*>(m_CommandStream.data() +
                        offset);
                    offset += sizeof(RHICmdSetFragmentShadingRate);
                    EShadingRateCombiner combiners[2] = {cmd->combinerOp[0], cmd->combinerOp[1]};
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
                    const auto* cmd = reinterpret_cast<const RHICmdSetStencilReference*>(m_CommandStream.data() +
                        offset);
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
                    const auto* cmd = reinterpret_cast<const RHICmdSetPrimitiveTopology*>(m_CommandStream.data() +
                        offset);
                    offset += sizeof(RHICmdSetPrimitiveTopology);
                    executor.SetPrimitiveTopology(cmd->topology);
                    break;
                }
            case ERHICommandType::SetDepthTestEnable:
                {
                    const auto* cmd = reinterpret_cast<const RHICmdSetDepthTestEnable*>(m_CommandStream.data() +
                        offset);
                    offset += sizeof(RHICmdSetDepthTestEnable);
                    executor.SetDepthTestEnable(cmd->enable);
                    break;
                }
            case ERHICommandType::SetDepthWriteEnable:
                {
                    const auto* cmd = reinterpret_cast<const RHICmdSetDepthWriteEnable*>(m_CommandStream.data() +
                        offset);
                    offset += sizeof(RHICmdSetDepthWriteEnable);
                    executor.SetDepthWriteEnable(cmd->enable);
                    break;
                }
            case ERHICommandType::SetDepthCompareOp:
                {
                    const auto* cmd = reinterpret_cast<const RHICmdSetDepthCompareOp*>(m_CommandStream.data() + offset);
                    offset += sizeof(RHICmdSetDepthCompareOp);
                    executor.SetDepthCompareOp(cmd->depthCompareOp);
                    break;
                }
            case ERHICommandType::SetStencilTestEnable:
                {
                    const auto* cmd = reinterpret_cast<const RHICmdSetStencilTestEnable*>(m_CommandStream.data() +
                        offset);
                    offset += sizeof(RHICmdSetStencilTestEnable);
                    executor.SetStencilTestEnable(cmd->enable);
                    break;
                }
            case ERHICommandType::SetStencilOp:
                {
                    const auto* cmd = reinterpret_cast<const RHICmdSetStencilOp*>(m_CommandStream.data() + offset);
                    offset += sizeof(RHICmdSetStencilOp);
                    executor.SetStencilOp(cmd->faceMask, cmd->failOp, cmd->passOp, cmd->depthFailOp, cmd->compareOp);
                    break;
                }
            case ERHICommandType::TrackDescriptorPoolUse:
                {
                    const auto* cmd = reinterpret_cast<const RHICmdTrackDescriptorPoolUse*>(m_CommandStream.data() +
                        offset);
                    offset += sizeof(RHICmdTrackDescriptorPoolUse);
                    executor.TrackDescriptorPoolUse(cmd->poolHandle, cmd->poolId);
                    break;
                }
            default:
                // Unknown command, skip or break
                break;
            }
        }
    }
}
