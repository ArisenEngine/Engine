#pragma once
#include "RHICommandBuffer.h"
#include "../../Common/CommandHeaders.h"
#include "RHI/Synchronization/RHIFence.h"

namespace NebulaEngine::RHI
{
    class RHICommandBuffer;
    class RHIFence;

    class RHICommandBufferPool
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHICommandBufferPool)
        RHICommandBufferPool(Device* device, u32 maxFramesInFlight) :
        m_Device(device), m_MaxFramesInFlight(maxFramesInFlight) { };
        virtual ~RHICommandBufferPool()
        {
            m_Fences.clear();
            m_Device = nullptr;
            m_CommandBuffers.clear();
        }
        virtual void* GetHandle() = 0;
        std::shared_ptr<RHICommandBuffer> GetCommandBuffer()
        {
            std::shared_ptr<RHICommandBuffer> commandBuffer;
            if (m_CommandBuffers.size() > 0)
            {
                commandBuffer = m_CommandBuffers.back();
                m_CommandBuffers.pop_back();
            }
            else
            {
                commandBuffer = CreateCommandBuffer();
            }
            
            return commandBuffer;
        }
        
        void ReleaseCommandBuffer(std::shared_ptr<RHICommandBuffer> commandBuffer)
        {
            commandBuffer->Clear();
            m_CommandBuffers.emplace_back(commandBuffer);
        }
        virtual std::shared_ptr<RHICommandBuffer> CreateCommandBuffer() = 0;
        
        RHIFence* GetFence(u32 currentFrameIndex) const
        {
            return m_Fences[currentFrameIndex % m_MaxFramesInFlight].get();
        }
        
    protected:
        Containers::Vector<std::unique_ptr<RHIFence>> m_Fences;
        Device* m_Device;
        Containers::Vector<std::shared_ptr<RHICommandBuffer>> m_CommandBuffers;
        u32 m_MaxFramesInFlight;
    };
}
