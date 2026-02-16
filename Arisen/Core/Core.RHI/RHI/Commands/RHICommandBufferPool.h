#pragma once
#include "RHICommandBuffer.h"
#include "Base/FoundationMinimal.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Definitions/CoreRHICommon.h"
#include <mutex>

namespace ArisenEngine::RHI
{
    class RHICommandBuffer;
    class RHI_DLL RHICommandBufferPool
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHICommandBufferPool)
        RHICommandBufferPool(RHIDevice* device, UInt32 maxFramesInFlight);
        virtual ~RHICommandBufferPool();


        virtual RHICommandBufferHandle GetCommandBuffer(UInt32 currentFrameIndex, ECommandBufferLevel level = COMMAND_BUFFER_LEVEL_PRIMARY)
        {
            (void)currentFrameIndex;
            std::lock_guard<std::mutex> lock(m_BuffersMutex);

            auto& freeList = (level == COMMAND_BUFFER_LEVEL_PRIMARY) ? m_FreePrimaryCommandBuffers : m_FreeSecondaryCommandBuffers;

            if (!freeList.empty())
            {
                RHICommandBufferHandle handle = freeList.back();
                freeList.pop_back();
                return handle;
            }
            
            return CreateCommandBuffer(level);
        }
        
        struct CommandBufferRecycler {
            RHICommandBufferPool* pool;
            RHICommandBufferHandle handle;
            ~CommandBufferRecycler() {
                if (pool && handle.IsValid()) {
                    pool->InternalRecycle(handle);
                }
            }
        };

        virtual void ReleaseCommandBuffer(UInt32 currentFrameIndex, RHICommandBufferHandle handle)
        {
            (void)currentFrameIndex;
            auto* commandBuffer = m_Device->GetCommandBuffer(handle);
            if (!commandBuffer) return;

            auto ticket = commandBuffer->GetLatestSubmitTicket();
            
            if (m_Device->GetCompletedSubmitTicket() >= ticket)
            {
                InternalRecycle(handle);
            }
            else
            {
                m_Device->DeferredDelete(RHIQueueType::Graphics, static_cast<RHIGpuTicket>(ticket), 
                    MakeDeferredDeleteItem(new CommandBufferRecycler{this, handle}));
            }
        }

    protected:
        virtual void InternalRecycle(RHICommandBufferHandle handle)
        {
            if (!handle.IsValid()) return;
            std::lock_guard<std::mutex> lock(m_BuffersMutex);
            auto* commandBuffer = m_Device->GetCommandBuffer(handle);
            if (!commandBuffer) return;

            if (commandBuffer->GetLevel() == COMMAND_BUFFER_LEVEL_PRIMARY)
                m_FreePrimaryCommandBuffers.push_back(handle);
            else
                m_FreeSecondaryCommandBuffers.push_back(handle);
        }
        virtual RHICommandBufferHandle CreateCommandBuffer(ECommandBufferLevel level) = 0;
        
    private:
        RHIDevice* m_Device;
        Containers::Vector<RHICommandBufferHandle> m_FreePrimaryCommandBuffers;
        Containers::Vector<RHICommandBufferHandle> m_FreeSecondaryCommandBuffers;
        UInt32 m_MaxFramesInFlight;
        std::mutex m_BuffersMutex;

    protected:
        RHIDevice* GetDevice() const;
        std::mutex& GetBuffersMutex() { return m_BuffersMutex; }
    };

}

