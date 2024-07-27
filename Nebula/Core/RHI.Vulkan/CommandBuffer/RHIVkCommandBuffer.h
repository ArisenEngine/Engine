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

        void BeginRenderPass(RenderPassBeginDesc&& desc) override;
        void EndRenderPass() override;
    private:
        VkCommandBuffer m_VkCommandBuffer;
        
    };
}
