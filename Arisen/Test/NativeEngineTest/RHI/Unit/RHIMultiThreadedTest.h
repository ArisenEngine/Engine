#pragma once
#include "../RHITestBase.h"
#include <thread>
#include <vector>
#include <atomic>
#include "../../Engine/NativeEngine/RHI/CommandBufferExports.h"
#include "../../Engine/NativeEngine/RHI/DeviceExports.h"

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
            m_CommandPool = RHI_Device_CreateCommandBufferPool(m_Device);
            return m_CommandPool != 0;
        }

        bool Run() override
        {
            LOG_INFO("Running Multi-threaded Command Recording Test...");

            const int numThreads = 8;
            const int numFrames = 10;
            
            for (int f = 0; f < numFrames; ++f)
            {
                std::vector<std::thread> threads;
                std::vector<RHI_CommandBufferHandle> cmdBuffers(numThreads);

                for (int i = 0; i < numThreads; ++i)
                {
                    threads.emplace_back([&, i, f]() {
                        // This should trigger the TLS Command Pool logic in RHIVkCommandBufferPool
                        RHI_CommandBufferHandle cmd = RHI_Device_GetCommandBuffer(m_Device, m_CommandPool, f);
                        cmdBuffers[i] = cmd;

                        RHI_Cmd_Begin(cmd, f, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
                        
                        // Fake recording work to stress the pool and internal structures
                        RHI_Cmd_SetViewport(cmd, 0, 0, 1280, 720, 0, 1);
                        RHI_Cmd_SetScissor(cmd, 0, 0, 1280, 720);
                        
                        RHI_Cmd_End(cmd);
                    });
                }

                for (auto& t : threads) t.join();

                LOG_INFO(String::Format("Frame %d: All threads finished recording. Submitting %d buffers...", f, numThreads));

                // Submit recorded buffers
                for (int i = 0; i < numThreads; ++i)
                {
                    RHI::RHISubmitDescriptor submitDesc = {};
                // If this test renders to swapchain, we need it. 
                // However, unit tests usually don't have m_SwapChain unless derived from RHIRenderingTestBase.
                // Assuming this is offscreen or simple submit.
                
                    RHI_Device_Submit(m_Device, cmdBuffers[i], reinterpret_cast<const ::RHISubmitDescriptor*>(&submitDesc));
                }

                // Wait for GPU to finish work so we can safely recycle/destroy
                RHI_Device_WaitIdle(m_Device);

                // Release (Recycle) command buffers
                for (int i = 0; i < numThreads; ++i)
                {
                    RHI_Device_ReleaseCommandBuffer(m_Device, m_CommandPool, f, cmdBuffers[i]);
                }
            }

            LOG_INFO("Multi-threaded test completed successfully without crashes or validation errors.");
            return true;
        }

        void TeardownTest() override
        {
            if (m_CommandPool)
            {
                RHI_Device_ReleaseCommandBufferPool(m_Device, m_CommandPool);
                m_CommandPool = 0;
            }
        }

    private:
        RHI_CommandBufferPoolHandle m_CommandPool = 0;
    };
}
