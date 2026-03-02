#include "../RHITestBase.h"
#include "RHI/Queues/RHIQueueType.h"
#include "RHI/Sync/RHISemaphore.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "Logger/Logger.h"

namespace ArisenEngine::Testing
{
    class RHIMultiQueueNativeTest : public RHITestBase
    {
    public:
        const char* GetName() const override { return "RHIMultiQueueNativeTest"; }
        TestCategory GetCategory() const override { return TestCategory::Unit; }
        bool IsHeadless() const override { return true; }

        bool Run() override
        {
            LOG_INFO("Running Multi-Queue Native Synchronization Test...");

            auto* graphicsQueue = m_Device->GetQueue(RHI::RHIQueueType::Graphics);
            auto* computeQueue = m_Device->GetQueue(RHI::RHIQueueType::Compute);

            if (!computeQueue)
            {
                LOG_WARN("Compute queue not available, skipping multi-queue test.");
                return true;
            }

            auto graphicsPool = m_Device->GetFactory()->CreateCommandBufferPool(RHI::RHIQueueType::Graphics);
            auto computePool = m_Device->GetFactory()->CreateCommandBufferPool(RHI::RHIQueueType::Compute);

            RHI::RHISemaphoreHandle sharedSem = m_Device->GetFactory()->CreateTimelineSemaphore(0);

            // 1. Submit to Graphics, signaling value 1
            auto gCmdHandle = m_Device->GetCommandBufferPool(graphicsPool)->GetCommandBuffer(0);
            auto gCmd = m_Device->GetCommandBuffer(gCmdHandle);
            gCmd->Begin(RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            gCmd->End();

            RHI::RHISubmitDescriptor gSubmit{};
            gSubmit.pSignalSemaphores = &sharedSem;
            uint64_t signalVal1 = 1;
            gSubmit.pSignalValues = &signalVal1;
            gSubmit.signalSemaphoreCount = 1;

            LOG_INFO("Submitting Graphics work (signal 1)...");
            graphicsQueue->Submit(gCmdHandle, &gSubmit);

            // 2. Submit to Compute, waiting for 1, signaling 2
            auto cCmdHandle = m_Device->GetCommandBufferPool(computePool)->GetCommandBuffer(0);
            auto cCmd = m_Device->GetCommandBuffer(cCmdHandle);
            cCmd->Begin(RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            cCmd->End();

            RHI::RHISubmitDescriptor cSubmit{};
            cSubmit.pWaitSemaphores = &sharedSem;
            uint64_t waitVal1 = 1;
            cSubmit.pWaitValues = &waitVal1;
            cSubmit.waitSemaphoreCount = 1;
            
            cSubmit.pSignalSemaphores = &sharedSem;
            uint64_t signalVal2 = 2;
            cSubmit.pSignalValues = &signalVal2;
            cSubmit.signalSemaphoreCount = 1;

            LOG_INFO("Submitting Compute work (wait 1, signal 2)...");
            computeQueue->Submit(cCmdHandle, &cSubmit);

            // 3. CPU Wait for 2
            LOG_INFO("CPU Waiting for Compute completion (value 2)...");
            m_Device->GetSync()->WaitSemaphoreValue(sharedSem, 2);

            if (m_Device->GetSync()->GetSemaphoreValue(sharedSem) < 2)
            {
                LOG_ERROR("Multi-queue sync failed!");
                return false;
            }

            LOG_INFO("Multi-queue native sync test passed.");

            // Cleanup
            m_Device->GetFactory()->ReleaseSemaphore(sharedSem);
            m_Device->GetFactory()->ReleaseCommandBufferPool(graphicsPool);
            m_Device->GetFactory()->ReleaseCommandBufferPool(computePool);

            return true;
        }
    };
}
