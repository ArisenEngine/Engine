#pragma once
#include "../RHITestBase.h"
#include "RHI/Sync/RHIImageMemoryBarrier.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Queues/RHIQueueType.h"

#include "RHI/Core/RHIDevice.h"

namespace ArisenEngine::Testing
{
    /**
     * @brief Tests RHI Synchronization 2.0 functionality.
     */
    class RHISyncTest : public RHITestBase
    {
    public:
        const char* GetName() const override { return "RHISyncTest"; }
        TestCategory GetCategory() const override { return TestCategory::Unit; }
        bool IsHeadless() const override { return true; }

        bool SetupTest() override
        {
            m_CommandPool = m_Device->GetFactory()->CreateCommandBufferPool(RHI::RHIQueueType::Graphics);
            return m_CommandPool.IsValid();
        }

        bool Run() override
        {
            LOG_INFO("Running Synchronization 2.0 Test...");

            // Create a dummy image for barrier testing
            ArisenEngine::RHI::RHIImageDescriptor desc{
                RHI::IMAGE_TYPE_2D, 1024, 1024, 1, 1, 1,
                RHI::FORMAT_R8G8B8A8_UNORM, RHI::IMAGE_TILING_OPTIMAL,
                RHI::IMAGE_LAYOUT_UNDEFINED,
                RHI::IMAGE_USAGE_SAMPLED_BIT | RHI::IMAGE_USAGE_TRANSFER_DST_BIT,
                RHI::SAMPLE_COUNT_1_BIT, RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::ERHIMemoryUsage::GpuOnly
            };
            LOG_INFO("Creating image...");
            RHI::RHIImageHandle testImage = m_Device->GetFactory()->CreateImage(std::move(desc), "SyncTestImage");

            LOG_INFO("Getting command buffer...");
            auto cmdHandle = m_Device->GetCommandBufferPool(m_CommandPool)->GetCommandBuffer(0);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);
            LOG_INFO("Beginning command buffer...");
            cmd->Begin(RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);

            // Test Image Barrier (Undefined -> Transfer Dst)
            Containers::Vector<RHI::RHIImageMemoryBarrier> imageBarriers = {
                {
                    RHI::ACCESS_NONE,
                    RHI::ACCESS_TRANSFER_WRITE_BIT,
                    RHI::IMAGE_LAYOUT_UNDEFINED,
                    RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    u32Invalid,
                    u32Invalid,
                    testImage,
                    {RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1},
                    RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT,
                    RHI::PIPELINE_STAGE_TRANSFER_BIT
                }
            };

            LOG_INFO("Adding pipeline barrier...");
            // Using the new Sync 2.0 API directly
            cmd->PipelineBarrier(
                RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT,
                RHI::PIPELINE_STAGE_TRANSFER_BIT,
                0, std::move(imageBarriers));

            LOG_INFO("Ending command buffer...");
            cmd->End();

            LOG_INFO("Submitting command buffer...");
            m_Device->GetQueue(RHI::RHIQueueType::Graphics)->Submit(cmdHandle);

            LOG_INFO("Waiting for device idle...");
            m_Device->DeviceWaitIdle();

            LOG_INFO("Synchronization barrier submitted and verified.");

            LOG_INFO("Running Timeline Semaphore Test...");
            RHI::RHISemaphoreHandle timelineSem = m_Device->GetFactory()->CreateTimelineSemaphore(0);
            if (!timelineSem.IsValid())
            {
                LOG_ERROR("Failed to create timeline semaphore!");
                return false;
            }

            LOG_INFO("CPU Signal timeline semaphore to 1...");
            m_Device->GetSync()->SignalSemaphoreValue(timelineSem, 1);

            unsigned long long val = m_Device->GetSync()->GetSemaphoreValue(timelineSem);
            LOG_INFOF("Current timeline value: {}", val);
            if (val != 1)
            {
                LOG_ERRORF("Timeline value mismatch! Expected 1, got {}", val);
                return false;
            }

            LOG_INFO("CPU Wait for timeline value 1...");
            m_Device->GetSync()->WaitSemaphoreValue(timelineSem, 1);

            // Test GPU wait and signal
            LOG_INFO("Testing GPU wait and signal with timeline semaphore...");
            auto timelineCmdHandle = m_Device->GetCommandBufferPool(m_CommandPool)->GetCommandBuffer(0);
            auto timelineCmd = m_Device->GetCommandBuffer(timelineCmdHandle);
            timelineCmd->Begin(RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            // (Dummy work)
            timelineCmd->End();

            RHI::RHISubmitDescriptor submitDesc{};
            RHI::RHISemaphoreHandle waitSemHandle = timelineSem;
            uint64_t waitVal = 1;
            uint64_t signalVal = 2;

            submitDesc.pWaitSemaphores = &waitSemHandle;
            submitDesc.pWaitValues = &waitVal;
            submitDesc.waitSemaphoreCount = 1;

            submitDesc.pSignalSemaphores = &waitSemHandle;
            submitDesc.pSignalValues = &signalVal;
            submitDesc.signalSemaphoreCount = 1;

            LOG_INFO("Submitting command buffer that waits for value 1 and signals value 2...");
            m_Device->GetQueue(RHI::RHIQueueType::Graphics)->Submit(timelineCmdHandle, &submitDesc);

            LOG_INFO("CPU Waiting for timeline value 2 (GPU signal)...");
            m_Device->GetSync()->WaitSemaphoreValue(timelineSem, 2);

            val = m_Device->GetSync()->GetSemaphoreValue(timelineSem);
            LOG_INFOF("Final timeline value: {}", val);
            if (val != 2)
            {
                LOG_ERRORF("Timeline value mismatch after GPU signal! Expected 2, got {}", val);
                return false;
            }

            LOG_INFO("Timeline semaphore test passed.");

            // Cleanup
            m_Device->GetFactory()->ReleaseSemaphore(timelineSem);
            m_Device->GetCommandBufferPool(m_CommandPool)->ReleaseCommandBuffer(0, timelineCmdHandle);

            // Cleanup
            LOG_INFO("Releasing image...");
            m_Device->GetFactory()->ReleaseImage(testImage);
            LOG_INFO("Releasing command buffer...");
            m_Device->GetCommandBufferPool(m_CommandPool)->ReleaseCommandBuffer(0, cmdHandle);

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
