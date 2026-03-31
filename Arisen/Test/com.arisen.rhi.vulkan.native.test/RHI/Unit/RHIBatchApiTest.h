#pragma once
#include "../RHITestBase.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"
#include "RHI/Pipeline/RHIPipelineCache.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Queues/RHIQueueType.h"

#include "RHI/Pipeline/RHIPipelineState.h"

namespace ArisenEngine::Testing
{
    class RHIBatchApiTest : public RHITestBase
    {
    public:
        const char* GetName() const override { return "RHIBatchApiTest"; }
        TestCategory GetCategory() const override { return TestCategory::Unit; }
        bool IsHeadless() const override { return true; }

        bool SetupTest() override
        {
            m_CommandPool = m_Device->GetFactory()->CreateCommandBufferPool(RHI::RHIQueueType::Graphics);
            return m_CommandPool.IsValid();
        }

        bool Run() override
        {
            LOG_INFO("Running Batch API Test...");

            // 1. Create Buffers
            LOG_INFO("Creating Buffers...");
            ArisenEngine::RHI::RHIBufferDescriptor desc1{
                0, 1024, RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT, RHI::SHARING_MODE_EXCLUSIVE, 0, nullptr,
                RHI::ERHIMemoryUsage::GpuOnly
            };
            ArisenEngine::RHI::RHIBufferDescriptor desc2{
                0, 2048, RHI::BUFFER_USAGE_INDEX_BUFFER_BIT, RHI::SHARING_MODE_EXCLUSIVE, 0, nullptr,
                RHI::ERHIMemoryUsage::GpuOnly
            };

            RHI::RHIBufferHandle handles[2];
            handles[0] = m_Device->GetFactory()->CreateBuffer(std::move(desc1), "BatchBuffer1");
            handles[1] = m_Device->GetFactory()->CreateBuffer(std::move(desc2), "BatchBuffer2");

            if (!handles[0].IsValid() || !handles[1].IsValid())
            {
                LOG_ERROR("Buffer creation failed!");
                return false;
            }
            LOG_INFO("Buffer creation successful.");

            // 2. Test Descriptor updates
            LOG_INFO("Testing Manual Descriptor Updates...");
            auto pso = m_Device->GetPipelineCache()->GetPipelineState();

            pso->UpdateDescriptorSet(0, 0, Containers::Vector<RHI::RHIBufferHandle>{handles[0]});
            pso->UpdateDescriptorSet(0, 1, Containers::Vector<RHI::RHIBufferHandle>{handles[1]});
            LOG_INFO("Descriptor update called.");

            // 3. Test Pipeline Barrier
            LOG_INFO("Testing Pipeline Barrier...");
            auto cmdHandle = m_Device->GetCommandBufferPool(m_CommandPool)->GetCommandBuffer(0);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);
            cmd->Begin(RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);

            Containers::Vector<RHI::RHIBufferMemoryBarrier> bufferBarriers;
            bufferBarriers.push_back({
                RHI::ACCESS_NONE,
                RHI::ACCESS_VERTEX_ATTRIBUTE_READ_BIT,
                u32Invalid,
                u32Invalid,
                handles[0],
                RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT,
                RHI::PIPELINE_STAGE_VERTEX_INPUT_BIT
            });

            cmd->PipelineBarrier(
                RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT,
                RHI::PIPELINE_STAGE_ALL_COMMANDS_BIT,
                0, std::move(bufferBarriers));

            cmd->End();
            LOG_INFO("Pipeline barrier recorded.");

            // Cleanup
            m_Device->GetFactory()->ReleaseBuffer(handles[0]);
            m_Device->GetFactory()->ReleaseBuffer(handles[1]);
            m_Device->GetCommandBufferPool(m_CommandPool)->ReleaseCommandBuffer(0, cmdHandle);

            LOG_INFO("Batch API Test completed successfully.");
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
