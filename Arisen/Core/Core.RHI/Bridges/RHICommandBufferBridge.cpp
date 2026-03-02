#include "RHI/Commands/RHICommandBuffer.h"
#include "Base/BindingMacros.h"

using namespace ArisenEngine::RHI;

ARISEN_BIND_BEGIN_BRIDGE("RHICommandBuffer", "Core.RHI.dll", "Arisen.Native.RHI")

extern "C" {
RHI_DLL void RHICommandBuffer_Begin(RHICommandBuffer* cb, uint32_t frameIndex)
{
    cb->Begin(frameIndex, 0, nullptr);
}

RHI_DLL void RHICommandBuffer_End(RHICommandBuffer* cb)
{
    cb->End();
}

RHI_DLL void RHICommandBuffer_BeginRenderPass(RHICommandBuffer* cb,
                                              RHIRenderPassHandle renderPass, RHIFrameBufferHandle frameBuffer,
                                              int subpassContents, uint32_t clearValueCount,
                                              const RHIClearValue* pClearValues)
{
    RenderPassBeginDesc desc{};
    desc.renderPass = renderPass;
    desc.frameBuffer = frameBuffer;
    desc.subpassContents = static_cast<ESubpassContents>(subpassContents);
    desc.clearValueCount = clearValueCount;
    desc.pClearValues = pClearValues;
    cb->BeginRenderPass(std::move(desc));
}

RHI_DLL void RHICommandBuffer_EndRenderPass(RHICommandBuffer* cb)
{
    cb->EndRenderPass();
}

RHI_DLL void RHICommandBuffer_BindPipeline(RHICommandBuffer* cb, RHIPipelineHandle pipeline)
{
    cb->BindPipeline(pipeline);
}

RHI_DLL void RHICommandBuffer_SetViewport(RHICommandBuffer* cb, float x, float y, float width, float height,
                                          float minDepth, float maxDepth)
{
    cb->SetViewport(x, y, width, height, minDepth, maxDepth);
}

RHI_DLL void RHICommandBuffer_SetScissor(RHICommandBuffer* cb, uint32_t offsetX, uint32_t offsetY, uint32_t width,
                                         uint32_t height)
{
    cb->SetScissor(offsetX, offsetY, width, height);
}

RHI_DLL void RHICommandBuffer_BindVertexBuffers(RHICommandBuffer* cb, RHIBufferHandle buffer, uint64_t offset)
{
    cb->BindVertexBuffers(buffer, offset);
}

RHI_DLL void RHICommandBuffer_BindIndexBuffer(RHICommandBuffer* cb, RHIBufferHandle buffer, uint64_t offset,
                                              int indexType)
{
    cb->BindIndexBuffer(buffer, offset, static_cast<EIndexType>(indexType));
}

RHI_DLL void RHICommandBuffer_Draw(RHICommandBuffer* cb, uint32_t vertexCount, uint32_t instanceCount,
                                   uint32_t firstVertex, uint32_t firstInstance, uint32_t firstBinding)
{
    cb->Draw(vertexCount, instanceCount, firstVertex, firstInstance, firstBinding);
}

RHI_DLL void RHICommandBuffer_DrawIndexed(RHICommandBuffer* cb, uint32_t indexCount, uint32_t instanceCount,
                                          uint32_t firstIndex, int vertexOffset, uint32_t firstInstance,
                                          uint32_t firstBinding)
{
    cb->DrawIndexed(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance, firstBinding);
}

RHI_DLL void RHICommandBuffer_PipelineBarrier(RHICommandBuffer* cb, int srcStage, int dstStage, uint32_t dependency,
                                              const RHIMemoryBarrier* pMemoryBarriers, uint32_t memoryBarrierCount,
                                              const RHIImageMemoryBarrier* pImageMemoryBarriers,
                                              uint32_t imageMemoryBarrierCount,
                                              const RHIBufferMemoryBarrier* pBufferMemoryBarriers,
                                              uint32_t bufferMemoryBarrierCount)
{
    cb->PipelineBarrier(static_cast<EPipelineStageFlag>(srcStage),
                        static_cast<EPipelineStageFlag>(dstStage),
                        dependency,
                        pMemoryBarriers, memoryBarrierCount,
                        pImageMemoryBarriers, imageMemoryBarrierCount,
                        pBufferMemoryBarriers, bufferMemoryBarrierCount);
}

RHI_DLL void RHICommandBuffer_TransitionImageLayout(RHICommandBuffer* cb, RHIImageHandle image, int targetLayout)
{
    cb->TransitionImageLayout(image, static_cast<EImageLayout>(targetLayout));
}

RHI_DLL void RHICommandBuffer_TransitionImageLayoutExplicit(RHICommandBuffer* cb, RHIImageHandle image, int oldLayout,
                                                            int targetLayout)
{
    cb->TransitionImageLayout(image, static_cast<EImageLayout>(oldLayout), static_cast<EImageLayout>(targetLayout));
}

RHI_DLL void RHICommandBuffer_BindDescriptorSets(RHICommandBuffer* cb, int bindPoint, uint32_t firstSet,
                                                 RHIDescriptorPoolHandle poolHandle, uint32_t poolId)
{
    cb->BindDescriptorSets(static_cast<EPipelineBindPoint>(bindPoint), firstSet, poolHandle, poolId);
}

RHI_DLL void RHICommandBuffer_PushConstants(RHICommandBuffer* cb, uint32_t offset, uint32_t size, const void* data,
                                            uint32_t stageFlags)
{
    cb->PushConstants(offset, size, data, stageFlags);
}

RHI_DLL void RHICommandBuffer_CopyBuffer(RHICommandBuffer* cb, RHIBufferHandle src, uint64_t srcOffset,
                                         RHIBufferHandle dst, uint64_t dstOffset, uint64_t size)
{
    cb->CopyBuffer(src, srcOffset, dst, dstOffset, size);
}

RHI_DLL void RHICommandBuffer_BeginDebugLabel(RHICommandBuffer* cb, const char* label, const float color[4])
{
    cb->BeginDebugLabel(label, color);
}

RHI_DLL void RHICommandBuffer_EndDebugLabel(RHICommandBuffer* cb)
{
    cb->EndDebugLabel();
}

RHI_DLL void RHICommandBuffer_InsertDebugMarker(RHICommandBuffer* cb, const char* label, const float color[4])
{
    cb->InsertDebugMarker(label, color);
}

RHI_DLL void RHICommandBuffer_Dispatch(RHICommandBuffer* cb, uint32_t groupCountX, uint32_t groupCountY, uint32_t groupCountZ)
{
    cb->Dispatch(groupCountX, groupCountY, groupCountZ);
}

RHI_DLL void RHICommandBuffer_BindDescriptorSet(RHICommandBuffer* cb, int bindPoint, uint32_t firstSet, RHIDescriptorPoolHandle poolHandle, uint32_t poolId, uint32_t setIdx)
{
    // The native BindDescriptorSets signature varies, but RHIAsyncComputeTest.h uses:
    // cmd->BindDescriptorSet(RHI::PIPELINE_BIND_POINT_COMPUTE, 0, m_DescriptorPoolHandle, m_PoolId, setIdx);
    cb->BindDescriptorSet(static_cast<EPipelineBindPoint>(bindPoint), firstSet, poolHandle, poolId, setIdx);
}

RHI_DLL void RHICommandBuffer_BeginRendering(RHICommandBuffer* cb,
    uint32_t imgViewIdx, uint32_t imgViewGen,
    int imageLayout, int loadOp, int storeOp,
    float clearR, float clearG, float clearB, float clearA,
    int32_t x, int32_t y, uint32_t width, uint32_t height)
{
    RHIRenderingAttachmentInfo colorAttachment{};
    colorAttachment.imageView = { imgViewIdx, imgViewGen };
    colorAttachment.imageLayout = static_cast<EImageLayout>(imageLayout);
    colorAttachment.loadOp = static_cast<EAttachmentLoadOp>(loadOp);
    colorAttachment.storeOp = static_cast<EAttachmentStoreOp>(storeOp);
    colorAttachment.clearValue.float32[0] = clearR;
    colorAttachment.clearValue.float32[1] = clearG;
    colorAttachment.clearValue.float32[2] = clearB;
    colorAttachment.clearValue.float32[3] = clearA;

    RHIRenderingInfo info{};
    info.pColorAttachments = &colorAttachment;
    info.colorAttachmentCount = 1;
    info.pResolveAttachments = nullptr;
    info.pDepthAttachment = nullptr;
    info.pStencilAttachment = nullptr;
    info.layerCount = 1;
    info.RHIRenderArea.x = x;
    info.RHIRenderArea.y = y;
    info.RHIRenderArea.width = width;
    info.RHIRenderArea.height = height;

    cb->BeginRendering(info);
}

RHI_DLL void RHICommandBuffer_EndRendering(RHICommandBuffer* cb)
{
    cb->EndRendering();
}
} // extern "C"

ARISEN_BIND_END_BRIDGE()
