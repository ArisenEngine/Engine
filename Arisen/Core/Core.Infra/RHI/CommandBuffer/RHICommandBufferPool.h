#pragma once
#include "RHICommandBuffer.h"
#include "../../Common/CommandHeaders.h"
#include "RHI/Devices/RHIDevice.h"
#include <mutex>

namespace ArisenEngine::RHI
{
    class RHICommandBuffer;
    class RHICommandBufferPool
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHICommandBufferPool)
        RHICommandBufferPool(RHIDevice* device, UInt32 maxFramesInFlight);;
        virtual ~RHICommandBufferPool()
        {
            m_Device = nullptr;
        }
        virtual void* GetHandle() = 0;
        RHICommandBuffer* GetCommandBuffer(UInt32 currentFrameIndex)
        {
            std::lock_guard<std::mutex> lock(m_BuffersMutex);
            RHICommandBuffer* commandBuffer;

            auto index = currentFrameIndex % m_MaxFramesInFlight;

            // Per-frame reuse safety: wait for the previous submission that used this frame-slot to finish.
            // Centralized fence ownership lives on the device.
            if (m_Device)
            {
                m_Device->WaitFrameFence(currentFrameIndex);
            }

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
        
        void ReleaseCommandBuffer(UInt32 currentFrameIndex, RHICommandBuffer* commandBuffer)
        {
            std::lock_guard<std::mutex> lock(m_BuffersMutex);
            auto index = currentFrameIndex % m_MaxFramesInFlight;
            commandBuffer->Release();
            m_CommandBuffers[index].emplace_back(commandBuffer);
        }
        virtual RHICommandBuffer* CreateCommandBuffer() = 0;
        
    protected:
        RHIDevice* m_Device;
        // NOTE: should clear by inherent class 
        Containers::Vector<Containers::Vector<RHICommandBuffer*>> m_CommandBuffers;
        UInt32 m_MaxFramesInFlight;
        std::mutex m_BuffersMutex;
    };

    inline RHICommandBufferPool::RHICommandBufferPool(RHIDevice* device, UInt32 maxFramesInFlight):
        m_Device(device), m_MaxFramesInFlight(maxFramesInFlight)
    {
        m_CommandBuffers.resize(m_MaxFramesInFlight);
    }
}
