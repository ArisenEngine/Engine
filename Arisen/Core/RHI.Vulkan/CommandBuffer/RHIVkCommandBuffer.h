#pragma once
#include "RHIVkCommandBufferPool.h"
#include "../Devices/RHIVkDevice.h"
#include "../Surfaces/RHIVkFrameBuffer.h"
#include "RHI/CommandBuffer/RHICommandBuffer.h"
#include "RHI/Enums/Pipeline/EIndexType.h"
#include "RHI/Enums/Subpass/EDependencyFlag.h"
#include "RHI/Synchronization/RHIBufferMemoryBarrier.h"
#include "RHI/Synchronization/RHIImageMemoryBarrier.h"
#include "RHI/Synchronization/RHIMemoryBarrier.h"

namespace  ArisenEngine::RHI
{
    class RHIVkCommandBufferPool;
    class RHIVkCommandBuffer final : public RHICommandBuffer
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkCommandBuffer)
        ~RHIVkCommandBuffer() noexcept override;
        RHIVkCommandBuffer(RHIVkDevice* device, RHIVkCommandBufferPool* pool);

        void* GetHandle() const override;
        void* GetHandlerPointer() override;

        void BeginRenderPass(UInt32 frameIndex, RenderPassBeginDesc&& desc) override;
        void EndRenderPass() override;
        void Begin(UInt32 frameIndex) override;
        void Begin(UInt32 frameIndex, UInt32 commandBufferUsage) override;
        void End() override;

        void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height, Float32 minDepth, Float32 maxDepth) override;
        void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height) override;
        void SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width, UInt32 height) override;

        void BindPipeline(UInt32 frameIndex, GPUPipeline* pipeline) override;
        
        void Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance, UInt32 firstBinding) override;
        void DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset, UInt32 firstInstance,  UInt32 firstBinding) override;
        void BindVertexBuffers(BufferHandle* buffers, UInt64 offset) override;
        void BindIndexBuffer(BufferHandle* indexBuffer, UInt64 offset, EIndexType type) override;
        
        void WaitSemaphore(RHISemaphore* semaphore, EPipelineStageFlag stage) override;
        void SignalSemaphore(RHISemaphore* semaphore) override;
        void CopyBuffer(BufferHandle const * src, UInt64 srcOffset, BufferHandle const * dst, UInt64 dstOffset, UInt64 size) override;
        
        void InjectFence(RHIFence* fence) override;
        void WaitForFence(UInt32 frameIndex) override;
        void BindDescriptorSets(UInt32 frameIndex, EPipelineBindPoint bindPoint,
    UInt32 firstSet, Containers::Vector<std::shared_ptr<RHIDescriptorSet>>& descriptorsets, UInt32 dynamicOffsetCount, const UInt32* pDynamicOffsets) override;

        void CopyBufferToImage(BufferHandle const * srcBuffer, ImageHandle const * dst,
            EImageLayout dstImageLayout, Containers::Vector<BufferImageCopy>&& regions) override;
        void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
    Containers::Vector<RHIMemoryBarrier>&& memoryBarriers,
    Containers::Vector<RHIImageMemoryBarrier> && imageMemoryBarriers,
    Containers::Vector<RHIBufferMemoryBarrier> && bufferMemoryBarriers) override;
        void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
   Containers::Vector<RHIMemoryBarrier>&& memoryBarriers) override;
        void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
   Containers::Vector<RHIImageMemoryBarrier> && imageMemoryBarriers) override;
        void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
    Containers::Vector<RHIBufferMemoryBarrier> && bufferMemoryBarriers) override;

        
    public:
        // Vulkan only
        const VkSemaphore* GetWaitSemaphores() const;
        UInt32 GetWaitSemaphoresCount() const;
        const VkSemaphore* GetSignalSemaphores() const;
        UInt32 GetSignalSemaphoresCount() const;
        const VkPipelineStageFlags* GetWaitStageMask() const;
        VkFence GetSubmissionFence() const;
        
    protected:
        
        void Release() override;
        void Reset() override;
        void ReadyForBegin(UInt32 frameIndex) override;
        void DoBegin() override;
        
    private:
        
        VkCommandBuffer m_VkCommandBuffer;
        VkCommandPool m_VkCommandPool;
        VkDevice m_VkDevice;
        RHIVkCommandBufferPool* m_RHICommandPool;
        Containers::Vector<VkBuffer> m_VertexBuffers;
        Containers::Vector<UInt64> m_VertexBindingOffsets;
        std::optional<VkBuffer> m_IndexBuffer;
        std::optional<UInt64> m_IndexOffset;
        std::optional<EIndexType> m_IndexType;

        Containers::Vector<VkSemaphore> m_WaitSemaphores;
        Containers::Vector<VkSemaphore> m_SignalSemaphores;
        Containers::Vector<VkPipelineStageFlags> m_WaitStages;
       
        VkCommandBufferBeginInfo m_VkBeginInfo {};
        std::optional<VkFence> m_SubmissionFence;

        Containers::Vector<VkMemoryBarrier> m_VkMemoryBarriers {};
        Containers::Vector<VkBufferMemoryBarrier> m_VkBufferMemoryBarriers {};
        Containers::Vector<VkImageMemoryBarrier> m_VkImageMemoryBarriers {};

        GPUPipeline* m_CurrentPipeline { nullptr };
    };
}
