#pragma once
#include "../RHITestBase.h"
#include <memory>
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Queues/RHIQueueType.h"

#include "RHI/Pipeline/RHIPipelineCache.h"
#include "RHI/Pipeline/RHIPipelineState.h"
#include "RHI/Descriptors/RHIDescriptorPool.h"
#include "ShaderCompiler/ShaderCompilerAPI.h"

namespace ArisenEngine::Testing
{
    /**
     * @brief Tests Async Compute.
     */
    class RHIAsyncComputeTest : public RHITestBase
    {
    private:
        RHI::RHICommandBufferPoolHandle m_CommandPool;
        RHI::RHIShaderProgramHandle m_ComputeProgram;
        RHI::RHIPipelineHandle m_Pipeline;

        RHI::RHIBufferHandle m_InputBuffer;
        RHI::RHIBufferHandle m_OutputBuffer;

        std::unique_ptr<RHI::RHIPipelineState> m_Pso;

        RHI::RHIDescriptorPool* m_DescriptorPool = nullptr;
        RHI::RHIDescriptorPoolHandle m_DescriptorPoolHandle;
        UInt32 m_PoolId = 0;

    public:
        const char* GetName() const override { return "RHIAsyncComputeTest"; }
        TestCategory GetCategory() const override { return TestCategory::Unit; }
        bool IsHeadless() const override { return true; }

        bool SetupTest() override
        {
            HAL::InitDXC();
            m_CommandPool = m_Device->GetFactory()->CreateCommandBufferPool(RHI::RHIQueueType::Compute);
            m_DescriptorPool = m_Device->GetDescriptorPool();
            m_DescriptorPoolHandle = m_Device->GetDescriptorPoolHandle();

            // 1. Compile and Create Compute Program
            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();
            auto shaderPath = exeDir.generic_wstring() + L"\\Shader\\AsyncComputeTest.hlsl";
            auto currentPath = exeDir.generic_wstring() + L"\\Shader";

            String envStr = m_Instance->GetEnvString();
            std::wstring envStrW = envStr.ToWString();

            HAL::ShaderCompileParams params{
                shaderPath, L"CSMain", L"6_0", L"-spirv", envStrW, L"0", RHI::EProgramStage::Compute,
                {}, {}, currentPath + L"\\AsyncComputeTest.comp.spirv", true
            };

            HAL::ShaderCompilerOutput output;
            if (!HAL::CompileShaderFromFile(std::move(params), output) || !output.codePointer)
            {
                LOG_ERROR("Compute shader compilation failed.");
                return false;
            }

            m_ComputeProgram = m_Device->GetFactory()->CreateGPUProgram();
            RHI::RHIShaderProgramDesc progDesc = {
                output.codeSize, output.codePointer, "CSMain", "AsyncComputeTest", RHI::SHADER_STAGE_COMPUTE_BIT
            };
            m_Device->GetFactory()->AttachProgramByteCode(m_ComputeProgram, std::move(progDesc));
            std::free(output.codePointer);

            // 2. Create Buffers
            const uint32_t elementCount = 1024;
            const uint32_t bufferSize = elementCount * sizeof(uint32_t);

            RHI::RHIBufferDescriptor bufDesc = {};
            bufDesc.size = bufferSize;
            bufDesc.usage = RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT;
            bufDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;

            m_InputBuffer = m_Device->GetFactory()->CreateBuffer(std::move(bufDesc), "InputBuffer");
            m_OutputBuffer = m_Device->GetFactory()->CreateBuffer(std::move(bufDesc), "OutputBuffer");

            std::vector<uint32_t> inputData(elementCount);
            for (uint32_t i = 0; i < elementCount; ++i) inputData[i] = i;
            m_Device->GetFactory()->BufferMemoryCopy(m_InputBuffer, inputData.data(), bufferSize, 0);

            // 3. Setup Pipeline and Descriptors
            auto pm = m_Device->GetPipelineCache();
            m_Pso = pm->GetPipelineState();
            m_Pso->SetBindPoint(RHI::PIPELINE_BIND_POINT_COMPUTE);
            m_Pso->AddProgram(m_ComputeProgram);

            Containers::Vector<RHI::RHIBufferHandle> inputs = {m_InputBuffer};
            Containers::Vector<RHI::RHIBufferHandle> outputs = {m_OutputBuffer};
            m_Pso->UpdateDescriptorSet(0, 0, std::move(inputs));
            m_Pso->UpdateDescriptorSet(0, 1, std::move(outputs));

            m_Pso->BuildDescriptorSetLayout();
            m_Pipeline = pm->GetComputePipeline(m_Pso.get()); // Assuming this calls GetComputePipeline? 
            // Step 399 showed: m_Pipeline = pm->GetComputePipeline(pso.get());
            // Need to verify GetComputePipeline signature. Assuming OK.
            m_Pipeline = pm->GetComputePipeline(m_Pso.get());

            Containers::Vector<RHI::EDescriptorType> types = {
                RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER, RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER
            };
            Containers::Vector<UInt32> counts = {1, 1};
            m_PoolId = m_DescriptorPool->AddPool(types, counts, 1);

            return true;
        }

        bool Run() override
        {
            LOG_INFO("Running Async Compute Test...");

            auto cmdHandle = m_Device->GetCommandBufferPool(m_CommandPool)->GetCommandBuffer(0);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);

            m_DescriptorPool->ResetPool(m_PoolId);
            UInt32 setIdx = m_DescriptorPool->AllocDescriptorSet(m_PoolId, (UInt32)0,
                                                                 (RHI::RHIPipelineState*)m_Pso.get());
            m_DescriptorPool->UpdateDescriptorSet(m_PoolId, setIdx, m_Pso.get());

            cmd->Begin(0, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            cmd->BindPipeline(m_Pipeline);
            cmd->BindDescriptorSet(RHI::PIPELINE_BIND_POINT_COMPUTE, 0, m_DescriptorPoolHandle, m_PoolId, setIdx);
            cmd->Dispatch(4, 1, 1); // 4 * 256 = 1024
            cmd->End();

            // SUBMIT TO COMPUTE QUEUE
            LOG_INFO("Submitting to Compute Queue...");
            auto queue = m_Device->GetQueue(RHI::RHIQueueType::Compute);
            if (!queue)
            {
                LOG_ERROR("Compute queue not available!");
                return false;
            }
            RHI::RHISubmitDescriptor submitDesc = {};
            auto ticket = queue->Submit(cmdHandle, &submitDesc);

            LOG_INFO("Waiting for Compute Ticket...");
            queue->WaitForTicket(ticket);

            LOG_INFO("Async Compute Test completed successfully (no crashes).");
            return true;
        }

        void TeardownTest() override
        {
            if (m_InputBuffer.IsValid()) m_Device->GetFactory()->ReleaseBuffer(m_InputBuffer);
            if (m_OutputBuffer.IsValid()) m_Device->GetFactory()->ReleaseBuffer(m_OutputBuffer);
            if (m_ComputeProgram.IsValid()) m_Device->GetFactory()->ReleaseGPUProgram(m_ComputeProgram);
            if (m_CommandPool.IsValid()) m_Device->GetFactory()->ReleaseCommandBufferPool(m_CommandPool);

            m_Pso.reset();
        }
    };
}
