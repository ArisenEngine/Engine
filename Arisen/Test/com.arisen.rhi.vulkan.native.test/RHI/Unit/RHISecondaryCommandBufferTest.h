#pragma once
#include "../RHITestBase.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Queues/RHIQueueType.h"


namespace ArisenEngine::Testing
{
    /**
     * @brief Tests Secondary Command Buffers.
     */
    class RHISecondaryCommandBufferTest : public RHITestBase
    {
    public:
        const char* GetName() const override { return "RHISecondaryCommandBufferTest"; }
        TestCategory GetCategory() const override { return TestCategory::Unit; }
        bool IsHeadless() const override { return true; }

        bool SetupTest() override
        {
            m_CommandPool = m_Device->GetFactory()->CreateCommandBufferPool(RHI::RHIQueueType::Graphics);
            return m_CommandPool.IsValid();
        }

        bool Run() override
        {
            LOG_INFO("Running Secondary Command Buffer Test...");

            const int numFrames = 3;
            for (int f = 0; f < numFrames; ++f)
            {
                // 1. Get Primary and Secondary Command Buffers
                auto pool = m_Device->GetCommandBufferPool(m_CommandPool);
                auto primaryCmdHandle = pool->GetCommandBuffer(
                    f, RHI::ECommandBufferLevel::COMMAND_BUFFER_LEVEL_PRIMARY);
                auto secondaryCmdHandle = pool->GetCommandBuffer(
                    f, RHI::ECommandBufferLevel::COMMAND_BUFFER_LEVEL_SECONDARY);
                auto primaryCmd = m_Device->GetCommandBuffer(primaryCmdHandle);
                auto secondaryCmd = m_Device->GetCommandBuffer(secondaryCmdHandle);

                // 2. Record Secondary Command Buffer
                secondaryCmd->Begin(f, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
                secondaryCmd->SetViewport(0, 0, 1920, 1080, 0, 1);
                secondaryCmd->SetScissor(0, 0, 1920, 1080);
                secondaryCmd->End();

                // 3. Record Primary Command Buffer and Execute Secondary
                primaryCmd->Begin(f, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);

                Containers::Vector<RHI::RHICommandBuffer*> secondaryBuffers = {secondaryCmd};
                primaryCmd->ExecuteCommands(std::move(secondaryBuffers));

                primaryCmd->End();

                // 4. Submit Primary
                RHI::RHISubmitDescriptor submitDesc = {};
                m_Device->GetQueue(RHI::RHIQueueType::Graphics)->Submit(primaryCmdHandle, &submitDesc);

                // 5. Wait and Recycle
                m_Device->DeviceWaitIdle();
                pool->ReleaseCommandBuffer(f, primaryCmdHandle);
                pool->ReleaseCommandBuffer(f, secondaryCmdHandle);

                LOG_INFO(String::Format("Frame %d completed.", f));
            }

            LOG_INFO("Secondary Command Buffer test completed successfully.");
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
