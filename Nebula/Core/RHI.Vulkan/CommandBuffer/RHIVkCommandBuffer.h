#pragma once
#include "RHIVkCommandBufferPool.h"
#include "../Devices/RHIVkDevice.h"
#include "../Surfaces/RHIVkFrameBuffer.h"
#include "RHI/CommandBuffer/RHICommandBuffer.h"

namespace  NebulaEngine::RHI
{
    class RHIVkCommandBufferPool;
    
    class RHIVkCommandBuffer final : public RHICommandBuffer
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkCommandBuffer)
        ~RHIVkCommandBuffer() noexcept override;
        RHIVkCommandBuffer(RHIVkDevice* device, RHIVkCommandBufferPool* pool);

        void* GetHandle() const override { return m_VkCommandBuffer; }
        void* GetHandlerPointer() override { return &m_VkCommandBuffer; }

        void BeginRenderPass(u32 frameIndex, RenderPassBeginDesc&& desc) override;
        void EndRenderPass() override;
        
        void Clear() override;
        void Begin(u32 frameIndex) override;
        void End() override;

        void SetViewport(f32 x, f32 y, f32 width, f32 height, f32 minDepth, f32 maxDepth) override;
        void SetViewport(f32 x, f32 y, f32 width, f32 height) override;
        void SetScissor(u32 offsetX, u32 offsetY, u32 width, u32 height) override;

        void BindPipeline(u32 frameIndex, GPUPipeline* pipeline) override;

        void Draw(u32 vertexCount, u32 instanceCount, u32 firstVertex, u32 firstInstance) override;
        void BindVertexBuffers(u32 firstBinding, Containers::Vector<BufferHandle*> buffers, Containers::Vector<u64> offsets) override;
        
    private:
        VkCommandBuffer m_VkCommandBuffer;
        VkCommandPool m_VkCommandPool;
        VkDevice m_VkDevice;
        RHIVkCommandBufferPool* m_RHICommandPool;
        Containers::Vector<VkBuffer> m_VertexBuffers;
    };
}
