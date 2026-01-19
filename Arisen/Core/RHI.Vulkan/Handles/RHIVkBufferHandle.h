#pragma once
#include "../CommandBuffer/RHIVkCommandBuffer.h"
#include "RHI/Handles/BufferHandle.h"
#include "RHI/Utils/RHIResourceHandle.h"

namespace ArisenEngine::RHI
{
    class RHIVkBufferHandle final : public BufferHandle
    {
   
    public:
        NO_COPY_NO_MOVE(RHIVkBufferHandle)
        explicit RHIVkBufferHandle(RHIDevice* device);
        ~RHIVkBufferHandle() noexcept override;
        void* GetHandle() const override;

        bool AllocBufferHandle(BufferDescriptor && desc) override;
        void FreeBufferHandle() override;

        bool AllocDeviceMemory(UInt32 memoryPropertiesBits) override;
        void MemoryCopy(void const* src, UInt32 offset) override;
    private:
        
        VkBuffer m_VkBuffer { VK_NULL_HANDLE };
        RHIDevice* m_Device;
        RHIResourceHandle m_RHIHandle;

    public:
        void SetRHIHandle(RHIResourceHandle h) { m_RHIHandle = h; }
        RHIResourceHandle GetRHIHandle() const { return m_RHIHandle; }
        
    };
}
