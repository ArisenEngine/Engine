#pragma once
#include "RHICommandBuffer.h"
#include "../../Common/CommandHeaders.h"
#include "RHI/Synchronization/RHIFence.h"

namespace ArisenEngine::RHI
{
    class RHICommandBuffer;
    class RHIFence;

    class RHICommandBufferPool
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHICommandBufferPool)
        RHICommandBufferPool(Device* device, UInt32 maxFramesInFlight);;
        virtual ~RHICommandBufferPool()
        {
            m_Fences.clear();
            m_Device = nullptr;
        }
        virtual void* GetHandle() = 0;
        std::shared_ptr<RHICommandBuffer> GetCommandBuffer(UInt32 currentFrameIndex)
        {
            std::shared_ptr<RHICommandBuffer> commandBuffer;

            auto index = currentFrameIndex % m_MaxFramesInFlight;
            if (m_CommandBuffers[index].size() > 0)
            {
                commandBuffer = m_CommandBuffers[index].back();
                m_CommandBuffers[index].pop_back();
            }
            else
            {
                commandBuffer = CreateCommandBuffer();
            }
            
            return commandBuffer;
        }
        
        void ReleaseCommandBuffer(UInt32 currentFrameIndex, std::shared_ptr<RHICommandBuffer> commandBuffer)
        {
            auto index = currentFrameIndex % m_MaxFramesInFlight;
            commandBuffer->Release();
            m_CommandBuffers[index].emplace_back(commandBuffer);
        }
        virtual std::shared_ptr<RHICommandBuffer> CreateCommandBuffer() = 0;
        
        RHIFence* GetFence(UInt32 currentFrameIndex) const
        {
            return m_Fences[currentFrameIndex % m_MaxFramesInFlight].get();
        }
        
    protected:
        Containers::Vector<std::unique_ptr<RHIFence>> m_Fences;
        Device* m_Device;
        // NOTE: should clear by inherent class 
        Containers::Vector<Containers::Vector<std::shared_ptr<RHICommandBuffer>>> m_CommandBuffers;
        UInt32 m_MaxFramesInFlight;
    };

    inline RHICommandBufferPool::RHICommandBufferPool(Device* device, UInt32 maxFramesInFlight):
        m_Device(device), m_MaxFramesInFlight(maxFramesInFlight)
    {
        m_CommandBuffers.resize(m_MaxFramesInFlight);
    }
}
