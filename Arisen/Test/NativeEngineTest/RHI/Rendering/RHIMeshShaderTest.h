#pragma once

#include "../RHIRenderingTestBase.h"
#include <memory>
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Presentation/RHISwapChain.h"

#include "RHI/Pipeline/RHIPipelineCache.h"
#include "RHI/Pipeline/RHIPipelineState.h"
#include "RHI/Descriptors/RHIDescriptorPool.h"

namespace ArisenEngine::Testing
{
    using namespace ArisenEngine;

    class RHIMeshShaderTest : public RHIRenderingTestBase
    {
    private:
        std::unique_ptr<RHI::RHIPipelineState> m_MeshPso;
        RHI::RHIPipelineHandle m_MeshPipeline;

        Containers::Vector<RHI::RHIBufferHandle> m_UboBuffer;
        Containers::Vector<UInt32> m_DescriptorPoolIds;

        RHI::RHIShaderProgramHandle m_MeshProgram;
        RHI::RHIShaderProgramHandle m_FragProgram;

        // ... (omitting helper methods for brevity if possible, or just replacing the whole class/file content if easier, 
        // but replace_file_content works best on chunks. I'll do chunked replacements)


        RHI::RHIShaderProgramHandle CreateProgram(const std::wstring& shaderName, RHI::EShaderStage stageFlag,
                                                  const char* entryPoint,
                                                  const Containers::Vector<String>& defines = {})
        {
            std::wstring envStr = GetShaderEnvString().ToWString();

            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();

            // Search in source directory relative to engine root (common in dev builds)
            auto shaderPath = exeDir / L"../../Arisen/Test/NativeEngineTest/Shader" / (shaderName + L".hlsl");
            if (!fs::exists(shaderPath))
            {
                // Fallback or generic path logic
                shaderPath = exeDir / L"Shader" / (shaderName + L".hlsl");
            }
            auto path = shaderPath.wstring();

            RHI::EProgramStage stagePoint;
            if (stageFlag == RHI::SHADER_STAGE_MESH_BIT_EXT) stagePoint = RHI::EProgramStage::Mesh;
            else if (stageFlag == RHI::SHADER_STAGE_TASK_BIT_EXT) stagePoint = RHI::EProgramStage::Amplification;
            else if (stageFlag == RHI::SHADER_STAGE_FRAGMENT_BIT) stagePoint = RHI::EProgramStage::Fragment;
            else stagePoint = RHI::EProgramStage::Mesh;

            HAL::ShaderCompileParams params;
            params.input = path;
            params.entry = String::StringToWString(entryPoint);
            params.shaderModel = L"6_5"; // Mesh shaders require 6.5+
            params.target = L"-spirv";
            params.targetEnv = envStr;
            params.optimizeLevel = L"0";
            params.stage = stagePoint;
            params.defines = defines;
            params.includes = {};
            params.output = std::nullopt;
            params.useDXLayout = true;

            HAL::ShaderCompilerOutput output;
            if (!HAL::CompileShaderFromFile(std::move(params), output) || output.codePointer == nullptr || output.
                codeSize == 0)
            {
                LOG_ERROR(
                    (std::string("Shader compilation failed for ") + entryPoint + ": " + output.msgOut.c_str()).c_str(
                    ));
                return {};
            }

            auto program = m_Device->GetFactory()->CreateGPUProgram();
            {
                std::string nameStr = String::WStringToString(path);
                RHI::RHIShaderProgramDesc desc = {
                    output.codeSize, output.codePointer, entryPoint, nameStr.c_str(), stageFlag
                };
                m_Device->GetFactory()->AttachProgramByteCode(program, std::move(desc));
            }
            if (output.codePointer) std::free(output.codePointer);
            return program;
        }

    public:
        const char* GetName() const override { return "MeshShaderTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            RHIRenderingTestBase::SetupTest();

            InitCommonResources();

            // Programs
            m_MeshProgram = CreateProgram(L"MeshShaderTest", RHI::SHADER_STAGE_MESH_BIT_EXT, "MSMain", {L"MESH_STAGE"});
            m_FragProgram = CreateProgram(L"MeshShaderTest", RHI::SHADER_STAGE_FRAGMENT_BIT, "PSMain",
                                          {L"PIXEL_STAGE"});

            if (!m_MeshProgram.IsValid() || !m_FragProgram.IsValid())
            {
                LOG_ERROR("MeshShaderTest: Shader compilation failed, skipping test setup.");
                return false;
            }

            CreateResources();
            CreatePipelines();

            return true;
        }

        void TeardownTest() override
        {
            for (auto& ub : m_UboBuffer) if (ub.IsValid()) m_Device->GetFactory()->ReleaseBuffer(ub);

            if (m_MeshProgram.IsValid()) m_Device->GetFactory()->ReleaseGPUProgram(m_MeshProgram);
            if (m_FragProgram.IsValid()) m_Device->GetFactory()->ReleaseGPUProgram(m_FragProgram);

            m_MeshPso.reset();

            RHIRenderingTestBase::TeardownTest();
        }

    protected:
        void RenderFrame() override
        {
            auto currentIndex = GetCurrentFrameIndex();
            if (m_FrameTickets[currentIndex] > 0)
            {
                m_Device->GetQueue(RHI::RHIQueueType::Graphics)->WaitForTicket(m_FrameTickets[currentIndex]);
            }

            UpdateUniformBuffer();
            RecordAndSubmit();

            NextFrame();
        }

        void OnResize(UInt32 width, UInt32 height) override
        {
            if (width == 0 || height == 0) return;
        }

    private:
        void CreateResources()
        {
            // UBO
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = 256; // Padded MeshUBO size
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
                m_UboBuffer.push_back(m_Device->GetFactory()->CreateBuffer(std::move(ubDesc), "MeshUBO"));
            }

            // Descriptors
            m_DescriptorPoolIds.clear();
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types = {RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER};
                Containers::Vector<UInt32> counts = {128};
                m_DescriptorPoolIds.push_back(m_DescriptorPool->AddPool(types, counts, 128));
            }
        }

        void CreatePipelines()
        {
            auto pm = m_Device->GetPipelineCache();

            // Mesh Pipeline
            m_MeshPso = pm->GetPipelineState();
            m_MeshPso->AddProgram(m_MeshProgram);
            m_MeshPso->AddProgram(m_FragProgram);

            m_MeshPso->SetBindPoint(RHI::PIPELINE_BIND_POINT_GRAPHICS);

            RHI::RHIInputAssemblyState ia{};
            ia.topology = RHI::PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
            m_MeshPso->SetInputAssemblyState(ia);

            RHI::RHIRasterizationState rs{};
            rs.cullMode = RHI::CULL_MODE_NONE;
            m_MeshPso->SetRasterizationState(rs);

            RHI::RHIColorBlendState cb{};
            RHI::RHIColorBlendAttachmentState att{};
            att.blendEnable = false;
            att.colorWriteMask = 0xF;
            cb.attachments.push_back(att);
            m_MeshPso->SetColorBlendState(cb);

            m_MeshPso->BuildDescriptorSetLayout();
            m_MeshPso->SetDynamicStateMask(RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT);

            Containers::Vector<RHI::EFormat> colorFormats = {RHI::FORMAT_B8G8R8A8_SRGB};
            m_MeshPso->SetRenderingFormats(colorFormats, RHI::FORMAT_UNDEFINED, RHI::FORMAT_UNDEFINED);
            m_MeshPipeline = pm->GetGraphicsPipeline(m_MeshPso.get());
        }

        void UpdateUniformBuffer()
        {
            UpdateCamera((float)frameTime);
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);

            static auto startTime = std::chrono::high_resolution_clock::now();
            auto currentTime = std::chrono::high_resolution_clock::now();
            float time = std::chrono::duration<float, std::chrono::seconds::period>(currentTime - startTime).count();

            struct MeshUBO
            {
                glm::mat4 model;
                glm::mat4 view;
                glm::mat4 projection;
                float time;
                float padding[3];
            };

            MeshUBO ubo;
            ubo.model = glm::rotate(glm::mat4(1.0f), time * 0.5f, glm::vec3(0, 1, 0));
            ubo.view = GetViewMatrix();
            ubo.projection = GetProjectionMatrix(width / height);
            ubo.time = time;

            m_Device->GetFactory()->BufferMemoryCopy(m_UboBuffer[GetCurrentFrameIndex()], &ubo, sizeof(MeshUBO), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();

            // Update Descriptors
            {
                m_DescriptorPool->ResetPool(m_DescriptorPoolIds[currentIndex]);

                m_MeshPso->UpdateDescriptorSet(
                    0, 0, Containers::Vector<RHI::RHIBufferHandle>{m_UboBuffer[currentIndex]});

                UInt32 setIdx = m_DescriptorPool->AllocDescriptorSet(m_DescriptorPoolIds[currentIndex], (UInt32)0,
                                                                     (RHI::RHIPipelineState*)m_MeshPso.get());
                m_DescriptorPool->UpdateDescriptorSet(m_DescriptorPoolIds[currentIndex], setIdx, m_MeshPso.get());
            }

            auto pool = m_Device->GetCommandBufferPool(m_CmdPool);
            auto cmdHandle = pool->GetCommandBuffer(currentIndex);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);

            cmd->Begin(currentIndex, 0);

            // Graphics Render
            auto colorBuffer = m_SwapChain->BeginFrame(currentIndex);
            if (colorBuffer.IsValid())
            {
                auto colorView = m_SwapChain->GetImageView(currentIndex);

                RHI::RHIRenderingInfo renderInfo = {};
                renderInfo.RHIRenderArea = {0, 0, HAL::GetWindowWidth(m_WindowId), HAL::GetWindowHeight(m_WindowId)};
                renderInfo.layerCount = 1;
                renderInfo.colorAttachmentCount = 1;

                RHI::RHIRenderingAttachmentInfo att = {};
                att.imageView = colorView;
                att.imageLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                att.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                att.storeOp = RHI::ATTACHMENT_STORE_OP_STORE;
                att.clearValue.float32[0] = 0.05f;
                att.clearValue.float32[1] = 0.05f;
                att.clearValue.float32[2] = 0.05f;
                att.clearValue.float32[3] = 1.0f;
                renderInfo.pColorAttachments = &att;

                // Transition: UNDEFINED -> COLOR_ATTACHMENT_OPTIMAL
                cmd->TransitionImageLayout(colorBuffer, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);

                cmd->BeginRendering(renderInfo);
                cmd->BindPipeline(m_MeshPipeline);
                cmd->SetViewport(0, 0, (float)renderInfo.RHIRenderArea.width, (float)renderInfo.RHIRenderArea.height, 0,
                                 1);
                cmd->SetScissor(0, 0, renderInfo.RHIRenderArea.width, renderInfo.RHIRenderArea.height);
                cmd->BindDescriptorSet(RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPoolHandle,
                                       m_DescriptorPoolIds[currentIndex], 0);

                // Draw 10 rings/groups of tasks
                cmd->DrawMeshTasks(10, 1, 1);

                cmd->EndRendering();

                // Transition: COLOR_ATTACHMENT_OPTIMAL -> PRESENT_SRC_KHR
                cmd->TransitionImageLayout(colorBuffer, RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR);
            }

            cmd->End();

            RHI::RHISubmitDescriptor submitDesc = {};
            submitDesc.WaitSwapChain = m_SwapChain;
            submitDesc.SignalSwapChain = m_SwapChain;

            m_FrameTickets[currentIndex] = m_Device->GetQueue(RHI::RHIQueueType::Graphics)->Submit(cmdHandle, &submitDesc);
            m_SwapChain->EndFrame(currentIndex);
            pool->ReleaseCommandBuffer(currentIndex, cmdHandle);
        }
    };
}
