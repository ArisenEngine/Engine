#pragma once
#include "../RHITestBase.h"
#include "../../../Engine/NativeEngine/RHI/HandlesExports.h"
#include "../../../Engine/NativeEngine/RHI/PipelineExports.h"
#include "../../../Engine/NativeEngine/RHI/CommandBufferExports.h"
#include "../../../Engine/NativeEngine/RHI/DeviceExports.h"

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
            m_CommandPool = RHI_Device_CreateCommandBufferPool(m_Device);
            return m_CommandPool != 0;
        }

        bool Run() override
        {
            LOG_INFO("Running Batch API Test...");

            // 1. Test RHI_Device_BatchCreateBuffers
            LOG_INFO("Testing RHI_Device_BatchCreateBuffers...");
            ArisenEngine::RHI::RHIBufferDescriptor desc1{ 0, 1024, RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT, RHI::SHARING_MODE_EXCLUSIVE, 0, nullptr, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT };
            ArisenEngine::RHI::RHIBufferDescriptor desc2{ 0, 2048, RHI::BUFFER_USAGE_INDEX_BUFFER_BIT, RHI::SHARING_MODE_EXCLUSIVE, 0, nullptr, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT };
            
            ArisenEngine::RHI::RHIBufferDescriptor descs[] = { desc1, desc2 };
            const char* names[] = { "BatchBuffer1", "BatchBuffer2" };
            RHI_BufferHandle handles[2] = { 0, 0 };

            RHI_Device_BatchCreateBuffers(m_Device, 2, descs, names, handles);

            if (handles[0] == 0 || handles[1] == 0)
            {
                LOG_ERROR("Batch buffer creation failed!");
                return false;
            }
            LOG_INFO("Batch buffer creation successful.");

            // 2. Test RHI_PSO_BatchUpdateDescriptors
            LOG_INFO("Testing RHI_PSO_BatchUpdateDescriptors...");
            RHI_PSOHandle pso = RHI_PipelineManager_CreatePSO(RHI_Device_GetPipelineManager(m_Device));
            
            Containers::Vector<RHI::RHIBufferHandle> bufferVector1;
            bufferVector1.push_back(*reinterpret_cast<RHI::RHIBufferHandle*>(&handles[0]));
            
            Containers::Vector<RHI::RHIBufferHandle> bufferVector2;
            bufferVector2.push_back(*reinterpret_cast<RHI::RHIBufferHandle*>(&handles[1]));

            RHI_DescriptorUpdateEntry entries[2];
            entries[0] = { 0, 0, &bufferVector1, nullptr };
            entries[1] = { 0, 1, &bufferVector2, nullptr };

            RHI_PSO_BatchUpdateDescriptors(pso, 2, entries);
            LOG_INFO("Batch descriptor update called.");

            // 3. Test RHI_Cmd_BatchPipelineBarrier
            LOG_INFO("Testing RHI_Cmd_BatchPipelineBarrier...");
            RHI_CommandBufferHandle cmd = RHI_Device_GetCommandBuffer(m_Device, m_CommandPool, 0);
            RHI_Cmd_Begin(cmd, 0, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);

            Containers::Vector<RHI::RHIBufferMemoryBarrier> bufferBarriers;
            bufferBarriers.push_back({
                RHI::ACCESS_NONE,
                RHI::ACCESS_VERTEX_ATTRIBUTE_READ_BIT,
                u32Invalid,
                u32Invalid,
                *reinterpret_cast<RHI::RHIBufferHandle*>(&handles[0]),
                RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT,
                RHI::PIPELINE_STAGE_VERTEX_INPUT_BIT
            });

            RHI_Cmd_BatchPipelineBarrier(cmd,
                RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT,
                RHI::PIPELINE_STAGE_ALL_COMMANDS_BIT,
                0, nullptr, nullptr, &bufferBarriers);

            RHI_Cmd_End(cmd);
            LOG_INFO("Batch pipeline barrier recorded.");

            // Cleanup
            RHI_PSO_Release(pso);
            RHI_Device_ReleaseBuffer(m_Device, handles[0]);
            RHI_Device_ReleaseBuffer(m_Device, handles[1]);
            RHI_Device_ReleaseCommandBuffer(m_Device, m_CommandPool, 0, cmd);

            LOG_INFO("Batch API Test completed successfully.");
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
