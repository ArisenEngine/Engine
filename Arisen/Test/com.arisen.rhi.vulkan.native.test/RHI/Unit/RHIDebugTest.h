#pragma once
#include "../RHITestBase.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Queues/RHIQueueType.h"


namespace ArisenEngine::Testing
{
    class RHIDebugTest : public RHITestBase
    {
    public:
        const char* GetName() const override { return "RHIDebugTest"; }
        TestCategory GetCategory() const override { return TestCategory::Unit; }
        bool IsHeadless() const override { return true; }

        bool SetupTest() override
        {
            m_CommandPool = m_Device->GetFactory()->CreateCommandBufferPool(RHI::RHIQueueType::Graphics);
            return m_CommandPool.IsValid();
        }

        bool Run() override
        {
            LOG_INFO("Running RHI Debug Markers and Naming Test...");

            // 1. Test Resource Naming
            LOG_INFO("Testing RHI_Device_SetObjectName...");
            ArisenEngine::RHI::RHIBufferDescriptor bufferDesc{
                0, 1024, RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT, RHI::SHARING_MODE_EXCLUSIVE, 0, nullptr,
                RHI::ERHIMemoryUsage::GpuOnly
            };
            RHI::RHIBufferHandle buffer = m_Device->GetFactory()->CreateBuffer(
                std::move(bufferDesc), "DebugBufferInitial");

            if (!buffer.IsValid())
            {
                LOG_ERROR("Buffer creation failed!");
                return false;
            }

            LOG_INFO("Setting buffer name to 'TestBuffer'...");
            m_Device->SetObjectName(RHI::ERHIObjectType::Buffer, *reinterpret_cast<UInt64*>(&buffer), "TestBuffer");

            // 2. Test Debug Labels and Markers
            LOG_INFO("Testing Debug Labels and Markers...");
            auto cmdHandle = m_Device->GetCommandBufferPool(m_CommandPool)->GetCommandBuffer(0);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);
            cmd->Begin(0, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);

            float red[4] = {1.0f, 0.0f, 0.0f, 1.0f};
            float green[4] = {0.0f, 1.0f, 0.0f, 1.0f};
            float blue[4] = {0.0f, 0.0f, 1.0f, 1.0f};

            cmd->BeginDebugLabel("Render Loop", red);
            cmd->InsertDebugMarker("Start Frame", green);

            // Nested labels
            cmd->BeginDebugLabel("Geometry Pass", blue);
            cmd->InsertDebugMarker("Draw Mesh", nullptr);
            cmd->EndDebugLabel(); // End Geometry Pass

            cmd->EndDebugLabel(); // End Render Loop

            cmd->End();

            // 3. Submit
            LOG_INFO("Submitting command buffer with debug markers...");
            RHI::RHISubmitDescriptor submitDesc = {};
            m_Device->GetQueue(RHI::RHIQueueType::Graphics)->Submit(cmdHandle, &submitDesc);

            m_Device->GraphicQueueWaitIdle();
            LOG_INFO("Submission completed.");

            // Cleanup
            m_Device->GetFactory()->ReleaseBuffer(buffer);
            m_Device->GetCommandBufferPool(m_CommandPool)->ReleaseCommandBuffer(0, cmdHandle);

            LOG_INFO("RHI Debug Markers and Naming Test completed successfully.");
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
