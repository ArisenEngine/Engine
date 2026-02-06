#pragma once
#include "../RHITestBase.h"
#include "../../Engine/NativeEngine/RHI/CommandBufferExports.h"
#include "../../Engine/NativeEngine/RHI/DeviceExports.h"

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
            m_CommandPool = RHI_Device_CreateCommandBufferPool(m_Device);
            return m_CommandPool != 0;
        }

        bool Run() override
        {
            LOG_INFO("Running Secondary Command Buffer Test...");

            const int numFrames = 3;
            for (int f = 0; f < numFrames; ++f)
            {
                // 1. Get Primary and Secondary Command Buffers
                RHI_CommandBufferHandle primaryCmd = RHI_Device_GetCommandBuffer(m_Device, m_CommandPool, f);
                RHI_CommandBufferHandle secondaryCmd = RHI_Device_GetSecondaryCommandBuffer(m_Device, m_CommandPool, f);

                // 2. Record Secondary Command Buffer
                // Secondary buffers need inheritance info if they were used in a render pass, 
                // but since this is a unit test without a render pass, we use default.
                RHI_Cmd_Begin(secondaryCmd, f, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
                RHI_Cmd_SetViewport(secondaryCmd, 0, 0, 1920, 1080, 0, 1);
                RHI_Cmd_SetScissor(secondaryCmd, 0, 0, 1920, 1080);
                RHI_Cmd_End(secondaryCmd);

                // 3. Record Primary Command Buffer and Execute Secondary
                RHI_Cmd_Begin(primaryCmd, f, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
                
                Containers::Vector<RHI_CommandBufferHandle> secondaryBuffers = { secondaryCmd };
                RHI_Cmd_ExecuteCommands(primaryCmd, &secondaryBuffers);
                
                RHI_Cmd_End(primaryCmd);

                // 4. Submit Primary
                RHI::RHISubmitDescriptor submitDesc = {};
                RHI_Device_Submit(m_Device, primaryCmd, reinterpret_cast<const ::RHISubmitDescriptor*>(&submitDesc));

                // 5. Wait and Recycle
                RHI_Device_WaitIdle(m_Device);
                RHI_Device_ReleaseCommandBuffer(m_Device, m_CommandPool, f, primaryCmd);
                RHI_Device_ReleaseCommandBuffer(m_Device, m_CommandPool, f, secondaryCmd);

                LOG_INFO(String::Format("Frame %d completed.", f));
            }

            LOG_INFO("Secondary Command Buffer test completed successfully.");
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
