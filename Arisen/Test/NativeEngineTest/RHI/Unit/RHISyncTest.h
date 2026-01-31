#pragma once
#include "../RHITestBase.h"
#include "RHI/Sync/RHIImageMemoryBarrier.h"
#include "../../Engine/NativeEngine/RHI/SyncExports.h"
#include "../../Engine/NativeEngine/RHI/CommandBufferExports.h"
#include "../../Engine/NativeEngine/RHI/DeviceExports.h"

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
            m_CommandPool = RHI_Device_CreateCommandBufferPool(m_Device);
            return m_CommandPool != 0;
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
                RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT
            };
            LOG_INFO("Creating image...");
            RHI_ImageHandle testImage = RHI_Device_CreateImage(m_Device, &desc, "SyncTestImage");

            LOG_INFO("Getting command buffer...");
            RHI_CommandBufferHandle cmd = RHI_Device_GetCommandBuffer(m_Device, m_CommandPool, 0);
            LOG_INFO("Beginning command buffer...");
            RHI_Cmd_Begin(cmd, 0, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);

            // Test Image Barrier (Undefined -> Transfer Dst)
            Containers::Vector<RHI::RHIImageMemoryBarrier> imageBarriers = {
                {
                    RHI::ACCESS_NONE,
                    RHI::ACCESS_TRANSFER_WRITE_BIT,
                    RHI::IMAGE_LAYOUT_UNDEFINED,
                    RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    u32Invalid,
                    u32Invalid,
                    *reinterpret_cast<RHI::RHIImageHandle*>(&testImage),
                    { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 },
                    RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT,
                    RHI::PIPELINE_STAGE_TRANSFER_BIT
                }
            };

            LOG_INFO("Adding pipeline barrier...");
            // Using the new Sync 2.0 API (internally) via the existing export
            RHI_Cmd_PipelineBarrier_Image(cmd, 
                RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, 
                RHI::PIPELINE_STAGE_TRANSFER_BIT, 
                0, &imageBarriers);

            LOG_INFO("Ending command buffer...");
            RHI_Cmd_End(cmd);

            LOG_INFO("Submitting command buffer...");
            RHI_Device_Submit(m_Device, cmd, 0);

            LOG_INFO("Waiting for device idle...");
            RHI_Device_WaitIdle(m_Device);

            LOG_INFO("Synchronization barrier submitted and verified.");

            // Cleanup
            LOG_INFO("Releasing image...");
            RHI_Device_ReleaseImage(m_Device, testImage);
            LOG_INFO("Releasing command buffer...");
            RHI_Device_ReleaseCommandBuffer(m_Device, m_CommandPool, 0, cmd);

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
