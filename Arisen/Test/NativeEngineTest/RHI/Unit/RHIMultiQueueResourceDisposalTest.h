#include "../RHITestBase.h"
#include "../../../../Core/RHI.Vulkan/Commands/RHIVkCommandBuffer.h"
#include "RHI/Handles/RHIHandle.h" 
#include "RHI/Descriptors/RHIResourceDescriptors.h"
#include "RHI/Queues/RHIQueueType.h"

namespace ArisenEngine::Testing
{
    class RHIMultiQueueResourceDisposalTest : public RHITestBase
    {
    public:
        const char* GetName() const override { return "RHIMultiQueueResourceDisposalTest"; }
        TestCategory GetCategory() const override { return TestCategory::Unit; }
        bool IsHeadless() const override { return true; }

        bool Run() override
        {
            LOG_INFO("Running Multi-Queue Resource Disposal Test ('Wait-All')...");

            auto* graphicsQueue = m_Device->GetQueue(RHI::RHIQueueType::Graphics);
            auto* computeQueue = m_Device->GetQueue(RHI::RHIQueueType::Compute);

            if (!computeQueue)
            {
                LOG_WARN("Compute queue not available, skipping multi-queue disposal test.");
                return true;
            }

            // Create a resource
            RHI::RHIBufferDescriptor desc{};
            desc.size = 1024;
            desc.usage = RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT;
            desc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;
            RHI::RHIBufferHandle buffer = m_Device->GetFactory()->CreateBuffer(std::move(desc), "WaitAllBuffer");

            // 1. Use on Graphics
            auto graphicsPool = m_Device->GetFactory()->CreateCommandBufferPool(RHI::RHIQueueType::Graphics);
            auto gCmdHandle = m_Device->GetCommandBufferPool(graphicsPool)->GetCommandBuffer(0);
            auto gCmd = m_Device->GetCommandBuffer(gCmdHandle);
            gCmd->Begin(RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            // Track resource
            static_cast<RHI::RHIVkCommandBuffer*>(gCmd)->CaptureResource(buffer); 
            gCmd->End();
            auto gTicket = graphicsQueue->Submit(gCmdHandle, nullptr);

            // 2. Use on Compute
            auto computePool = m_Device->GetFactory()->CreateCommandBufferPool(RHI::RHIQueueType::Compute);
            auto cCmdHandle = m_Device->GetCommandBufferPool(computePool)->GetCommandBuffer(0);
            auto cCmd = m_Device->GetCommandBuffer(cCmdHandle);
            cCmd->Begin(RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            // Track resource
            static_cast<RHI::RHIVkCommandBuffer*>(cCmd)->CaptureResource(buffer);
            cCmd->End();
            auto cTicket = computeQueue->Submit(cCmdHandle, nullptr);

            // 3. Release resource - it should NOT be destroyed until BOTH tickets are finished.
            LOG_INFO("Releasing buffer (queued for deferred deletion)...");
            m_Device->GetFactory()->ReleaseBuffer(buffer);

            // 4. Verify completion
            LOG_INFO("Waiting for tickets...");
            graphicsQueue->WaitForTicket(gTicket);
            computeQueue->WaitForTicket(cTicket);

            // Trigger flushes
            graphicsQueue->Update();
            computeQueue->Update();

            LOG_INFO("Multi-queue disposal test passed (completed without crash).");

            m_Device->GetFactory()->ReleaseCommandBufferPool(graphicsPool);
            m_Device->GetFactory()->ReleaseCommandBufferPool(computePool);

            return true;
        }
    };
}
