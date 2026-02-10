#pragma once
#include "../RHITestBase.h"
#include "../../../Engine/NativeEngine/RHI/HandlesExports.h"
#include "../../../Engine/NativeEngine/RHI/CommandBufferExports.h"
#include "../../../Engine/NativeEngine/RHI/DeviceExports.h"

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
            m_CommandPool = RHI_Device_CreateCommandBufferPool(m_Device);
            return m_CommandPool != 0;
        }

        bool Run() override
        {
            LOG_INFO("Running RHI Debug Markers and Naming Test...");

            // 1. Test Resource Naming
            LOG_INFO("Testing RHI_Device_SetObjectName...");
            ArisenEngine::RHI::RHIBufferDescriptor bufferDesc{ 0, 1024, RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT, RHI::SHARING_MODE_EXCLUSIVE, 0, nullptr, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT };
            RHI_BufferHandle buffer = RHI_Device_CreateBuffer(m_Device, &bufferDesc, "DebugBufferInitial");
            
            if (buffer == 0)
            {
                LOG_ERROR("Buffer creation failed!");
                return false;
            }

            LOG_INFO("Setting buffer name to 'TestBuffer'...");
            RHI_Device_SetObjectName(m_Device, RHI::ERHIObjectType::Buffer, buffer, "TestBuffer");

            // 2. Test Debug Labels and Markers
            LOG_INFO("Testing Debug Labels and Markers...");
            RHI_CommandBufferHandle cmd = RHI_Device_GetCommandBuffer(m_Device, m_CommandPool, 0);
            RHI_Cmd_Begin(cmd, 0, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);

            float red[4] = { 1.0f, 0.0f, 0.0f, 1.0f };
            float green[4] = { 0.0f, 1.0f, 0.0f, 1.0f };
            float blue[4] = { 0.0f, 0.0f, 1.0f, 1.0f };

            RHI_Cmd_BeginDebugLabel(cmd, "Render Loop", red);
            RHI_Cmd_InsertDebugMarker(cmd, "Start Frame", green);
            
            // Nested labels
            RHI_Cmd_BeginDebugLabel(cmd, "Geometry Pass", blue);
            RHI_Cmd_InsertDebugMarker(cmd, "Draw Mesh", nullptr);
            RHI_Cmd_EndDebugLabel(cmd); // End Geometry Pass

            RHI_Cmd_EndDebugLabel(cmd); // End Render Loop

            RHI_Cmd_End(cmd);
            
            // 3. Submit
            LOG_INFO("Submitting command buffer with debug markers...");
            RHI::RHISubmitDescriptor submitDesc{};
            RHI_Device_Submit(m_Device, cmd, reinterpret_cast<const struct RHISubmitDescriptor*>(&submitDesc));
            
            RHI_Device_GraphicQueueWaitIdle(m_Device);
            LOG_INFO("Submission completed.");

            // Cleanup
            RHI_Device_ReleaseBuffer(m_Device, buffer);
            RHI_Device_ReleaseCommandBuffer(m_Device, m_CommandPool, 0, cmd);

            LOG_INFO("RHI Debug Markers and Naming Test completed successfully.");
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
