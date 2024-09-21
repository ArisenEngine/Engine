#pragma once
#include "../CommandBuffer/RHIVkCommandBuffer.h"
#include "RHI/Handles/BufferHandle.h"

namespace NebulaEngine::RHI
{
    class RHIVkBufferHandle final : public BufferHandle
    {
   
    public:
        NO_COPY_NO_MOVE(RHIVkBufferHandle)
        RHIVkBufferHandle(VkDevice device);
        ~RHIVkBufferHandle() noexcept override;
        void* GetHandle() const override { return m_VkBuffer; }

        bool AllocBuffer(BufferAllocDesc && desc) override;
    private:
        
        VkBuffer m_VkBuffer { VK_NULL_HANDLE };
        VkDevice m_VKDevice;
    };
}
