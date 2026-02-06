#pragma once
#include "RHICommandBuffer.h"
#include "Base/FoundationMinimal.h"
#include "RHI/Core/RHIDevice.h"
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


        virtual RHICommandBuffer* GetCommandBuffer(UInt32 currentFrameIndex, ECommandBufferLevel level = COMMAND_BUFFER_LEVEL_PRIMARY)
        {
            (void)currentFrameIndex;
            std::lock_guard<std::mutex> lock(m_BuffersMutex);

            auto& freeList = (level == COMMAND_BUFFER_LEVEL_PRIMARY) ? m_FreePrimaryCommandBuffers : m_FreeSecondaryCommandBuffers;

            // Fetch any buffer from the free list. 
            // Buffers only enter the free lists via deferred release, so they are guaranteed GPU-safe.
            if (!freeList.empty())
            {
                RHICommandBuffer* commandBuffer = freeList.back();
                freeList.pop_back();
                return commandBuffer;
            }
            
            // If empty, always create new to avoid CPU stalls.
            return CreateCommandBuffer(level);
        }
        
        struct CommandBufferRecycler {
            RHICommandBufferPool* pool;
            RHICommandBuffer* buffer;
            ~CommandBufferRecycler() {
                if (pool && buffer) {
                    pool->InternalRecycle(buffer);
                }
            }
        };

        virtual void ReleaseCommandBuffer(UInt32 currentFrameIndex, RHICommandBuffer* commandBuffer)
        {
            (void)currentFrameIndex;
            auto ticket = commandBuffer->GetLatestSubmitTicket();
            
            // If the GPU is already done with it, recycle immediately.
            if (m_Device->GetCompletedSubmitTicket() >= ticket)
            {
                InternalRecycle(commandBuffer);
            }
            else
            {
                // Otherwise, defer recycling until the GPU ticket is reached.
                m_Device->DeferredDelete(RHIQueueType::Graphics, static_cast<RHIGpuTicket>(ticket), 
                    MakeDeferredDeleteItem(new CommandBufferRecycler{this, commandBuffer}));
            }
        }

    protected:
        virtual void InternalRecycle(RHICommandBuffer* commandBuffer)
        {
            if (!commandBuffer) return;
            std::lock_guard<std::mutex> lock(m_BuffersMutex);
            if (commandBuffer->GetLevel() == COMMAND_BUFFER_LEVEL_PRIMARY)
                m_FreePrimaryCommandBuffers.push_back(commandBuffer);
            else
                m_FreeSecondaryCommandBuffers.push_back(commandBuffer);
        }
        virtual RHICommandBuffer* CreateCommandBuffer(ECommandBufferLevel level) = 0;
        
    private:
        RHIDevice* m_Device;
        Containers::Vector<RHICommandBuffer*> m_FreePrimaryCommandBuffers;
        Containers::Vector<RHICommandBuffer*> m_FreeSecondaryCommandBuffers;
        UInt32 m_MaxFramesInFlight;
        std::mutex m_BuffersMutex;

    protected:
        RHIDevice* GetDevice() const { return m_Device; }
        std::mutex& GetBuffersMutex() { return m_BuffersMutex; }
    };

    inline RHICommandBufferPool::RHICommandBufferPool(RHIDevice* device, UInt32 maxFramesInFlight):
        m_Device(device), m_MaxFramesInFlight(maxFramesInFlight)
    {
    }
}

