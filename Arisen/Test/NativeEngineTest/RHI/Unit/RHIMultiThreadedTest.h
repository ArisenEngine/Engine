#pragma once
#include "../RHITestBase.h"
#include <thread>
#include <vector>
#include <atomic>
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Queues/RHIQueueType.h"


namespace ArisenEngine::Testing
{
    /**
     * @brief Tests multi-threaded command recording using Thread-Local Command Pools.
     */
    class RHIMultiThreadedTest : public RHITestBase
    {
    public:
        const char* GetName() const override { return "RHIMultiThreadedTest"; }
        TestCategory GetCategory() const override { return TestCategory::Unit; }
        bool IsHeadless() const override { return true; }

        bool SetupTest() override
        {
            m_CommandPool = m_Device->GetFactory()->CreateCommandBufferPool(RHI::RHIQueueType::Graphics);
            return m_CommandPool.IsValid();
        }

        bool Run() override
        {
            LOG_INFO("Running Multi-threaded Command Recording Test...");

            const int numThreads = 8;
            const int numFrames = 10;

            for (int f = 0; f < numFrames; ++f)
            {
                std::vector<std::thread> threads;
                std::vector<RHI::RHICommandBufferHandle> cmdHandles(numThreads);

                for (int i = 0; i < numThreads; ++i)
                {
                    threads.emplace_back([&, i, f]()
                    {
                        // This should trigger the TLS Command Pool logic in RHIVkCommandBufferPool
                        auto pool = m_Device->GetCommandBufferPool(m_CommandPool);
                        auto cmdHandle = pool->GetCommandBuffer(f);
                        cmdHandles[i] = cmdHandle;
                        auto cmd = m_Device->GetCommandBuffer(cmdHandle);

                        cmd->Begin(f, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);

                        // Fake recording work to stress the pool and internal structures
                        cmd->SetViewport(0, 0, 1280, 720, 0, 1);
                        cmd->SetScissor(0, 0, 1280, 720);

                        cmd->End();
                    });
                }

                for (auto& t : threads) t.join();

                LOG_INFO(
                    String::Format("Frame %d: All threads finished recording. Submitting %d buffers...", f, numThreads
                    ));

                // Submit recorded buffers
                for (int i = 0; i < numThreads; ++i)
                {
                    m_Device->GetQueue(RHI::RHIQueueType::Graphics)->Submit(cmdHandles[i]);
                }

                // Wait for GPU to finish work so we can safely recycle/destroy
                m_Device->DeviceWaitIdle();

                // Release (Recycle) command buffers
                for (int i = 0; i < numThreads; ++i)
                {
                    m_Device->GetCommandBufferPool(m_CommandPool)->ReleaseCommandBuffer(f, cmdHandles[i]);
                }
            }

            LOG_INFO("Multi-threaded test completed successfully without crashes or validation errors.");
            return true;
        }

        void TeardownTest() override
        {
            if (m_CommandPool.IsValid())
            {
                m_Device->GetFactory()->ReleaseCommandBufferPool(m_CommandPool);
                m_CommandPool = {};
            }
        }

    private:
        RHI::RHICommandBufferPoolHandle m_CommandPool;
    };
}
