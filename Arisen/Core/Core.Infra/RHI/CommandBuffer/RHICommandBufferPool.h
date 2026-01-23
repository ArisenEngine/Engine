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
            (void)currentFrameIndex;
            std::lock_guard<std::mutex> lock(m_BuffersMutex);

            // Fetch any buffer from the free list. 
            // Buffers only enter m_FreeCommandBuffers via deferred release, so they are guaranteed GPU-safe.
            if (!m_FreeCommandBuffers.empty())
            {
                RHICommandBuffer* commandBuffer = m_FreeCommandBuffers.back();
                m_FreeCommandBuffers.pop_back();
                return commandBuffer;
            }
            
            // If empty, always create new to avoid CPU stalls.
            return CreateCommandBuffer();
        }
        
        struct CommandBufferRecycler {
            RHICommandBufferPool* pool;
            RHICommandBuffer* buffer;
            ~CommandBufferRecycler() {
                if (pool && buffer) {
                    std::lock_guard<std::mutex> lock(pool->m_BuffersMutex);
                    buffer->Release();
                    pool->m_FreeCommandBuffers.emplace_back(buffer);
                }
            }
        };

        void ReleaseCommandBuffer(UInt32 currentFrameIndex, RHICommandBuffer* commandBuffer)
        {
            (void)currentFrameIndex;
            auto ticket = commandBuffer->GetLatestSubmitTicket();
            
            // If the GPU is already done with it, recycle immediately.
            if (m_Device->GetCompletedSubmitTicket() >= ticket)
            {
                std::lock_guard<std::mutex> lock(m_BuffersMutex);
                commandBuffer->Release();
                m_FreeCommandBuffers.emplace_back(commandBuffer);
            }
            else
            {
                // Otherwise, defer recycling until the GPU ticket is reached.
                m_Device->DeferredDelete(RHIQueueType::Graphics, static_cast<RHIGpuTicket>(ticket), 
                    MakeDeferredDeleteItem(new CommandBufferRecycler{this, commandBuffer}));
            }
        }
        virtual RHICommandBuffer* CreateCommandBuffer() = 0;
        
    protected:
        RHIDevice* m_Device;
        Containers::Vector<RHICommandBuffer*> m_FreeCommandBuffers;
        UInt32 m_MaxFramesInFlight;
        std::mutex m_BuffersMutex;
    };

    inline RHICommandBufferPool::RHICommandBufferPool(RHIDevice* device, UInt32 maxFramesInFlight):
        m_Device(device), m_MaxFramesInFlight(maxFramesInFlight)
    {
    }
}
