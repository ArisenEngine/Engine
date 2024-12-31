#pragma once
#include "RHIVkCommandBufferPool.h"
#include "../Devices/RHIVkDevice.h"
#include "../Surfaces/RHIVkFrameBuffer.h"
#include "RHI/CommandBuffer/RHICommandBuffer.h"
#include "RHI/Enums/Pipeline/EIndexType.h"

namespace  NebulaEngine::RHI
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

        void BeginRenderPass(u32 frameIndex, RenderPassBeginDesc&& desc) override;
        void EndRenderPass() override;
        void Begin(u32 frameIndex) override;
        void Begin(u32 frameIndex, u32 commandBufferUsage) override;
        void End() override;

        void SetViewport(f32 x, f32 y, f32 width, f32 height, f32 minDepth, f32 maxDepth) override;
        void SetViewport(f32 x, f32 y, f32 width, f32 height) override;
        void SetScissor(u32 offsetX, u32 offsetY, u32 width, u32 height) override;

        void BindPipeline(u32 frameIndex, GPUPipeline* pipeline) override;
        
        void Draw(u32 vertexCount, u32 instanceCount, u32 firstVertex, u32 firstInstance, u32 firstBinding) override;
        void DrawIndexed(u32 indexCount, u32 instanceCount, u32 firstIndex, u32 vertexOffset, u32 firstInstance,  u32 firstBinding) override;
        void BindVertexBuffers(BufferHandle* buffers, u64 offset) override;
        void BindIndexBuffer(BufferHandle* indexBuffer, u64 offset, EIndexType type) override;
        
        void WaitSemaphore(RHISemaphore* semaphore, EPipelineStageFlag stage) override;
        void SignalSemaphore(RHISemaphore* semaphore) override;
        void CopyBuffer(BufferHandle const * src, u64 srcOffset, BufferHandle const * dst, u64 dstOffset, u64 size) override;
        
        void InjectFence(RHIFence* fence) override;
        void WaitForFence(u32 frameIndex) override;
        
    public:
        
        const VkSemaphore* GetWaitSemaphores() const;
        u32 GetWaitSemaphoresCount() const;
        const VkSemaphore* GetSignalSemaphores() const;
        u32 GetSignalSemaphoresCount() const;
        const VkPipelineStageFlags* GetWaitStageMask() const;
        VkFence GetSubmissionFence() const;
        
    protected:
        
        void Release() override;
        void Reset() override;
        void ReadyForBegin(u32 frameIndex) override;
        void DoBegin() override;
        
    private:
        
        VkCommandBuffer m_VkCommandBuffer;
        VkCommandPool m_VkCommandPool;
        VkDevice m_VkDevice;
        RHIVkCommandBufferPool* m_RHICommandPool;
        Containers::Vector<VkBuffer> m_VertexBuffers;
        Containers::Vector<u64> m_VertexBindingOffsets;
        std::optional<VkBuffer> m_IndexBuffer;
        std::optional<u64> m_IndexOffset;
        std::optional<EIndexType> m_IndexType;

        Containers::Vector<VkSemaphore> m_WaitSemaphores;
        Containers::Vector<VkSemaphore> m_SignalSemaphores;
        Containers::Vector<VkPipelineStageFlags> m_WaitStages;
       
        VkCommandBufferBeginInfo m_VkBeginInfo {};
        std::optional<VkFence> m_SubmissionFence;
    };
}
