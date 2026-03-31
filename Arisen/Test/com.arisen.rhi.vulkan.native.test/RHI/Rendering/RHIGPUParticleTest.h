#pragma once

#include <memory>
#include "../RHIRenderingTestBase.h"
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

    struct Particle
    {
        glm::vec4 position; // xyz, w = life
        glm::vec4 velocity; // xyz, w = maxLife
    };

    class RHIGPUParticleTest : public RHIRenderingTestBase
    {
    private:
        std::unique_ptr<RHI::RHIPipelineState> m_ComputePso;
        RHI::RHIPipelineHandle m_ComputePipeline;

        std::unique_ptr<RHI::RHIPipelineState> m_GraphicsPso;
        RHI::RHIPipelineHandle m_GraphicsPipeline;

        RHI::RHIBufferHandle m_ParticleBuffer;
        Containers::Vector<RHI::RHIBufferHandle> m_UboBuffer;

        Containers::Vector<UInt32> m_ComputeDescriptorPoolIds;
        Containers::Vector<UInt32> m_GraphicsDescriptorPoolIds;

        RHI::RHIShaderProgramHandle m_ComputeProgram;

        const UInt32 m_ParticleCount = 1000000;

        RHI::RHIShaderProgramHandle CreateProgram(const std::wstring& shaderName, RHI::EShaderStage stageFlag,
                                                  const char* entryPoint)
        {
            std::wstring envStr = GetShaderEnvString().ToWString();

            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();
            auto shaderPath = exeDir / L"Shader" / (shaderName + L".hlsl");
            auto path = shaderPath.wstring();

            RHI::EProgramStage stagePoint;
            if (stageFlag == RHI::SHADER_STAGE_VERTEX_BIT) stagePoint = RHI::EProgramStage::Vertex;
            else if (stageFlag == RHI::SHADER_STAGE_FRAGMENT_BIT) stagePoint = RHI::EProgramStage::Fragment;
            else if (stageFlag == RHI::SHADER_STAGE_COMPUTE_BIT) stagePoint = RHI::EProgramStage::Compute;
            else stagePoint = RHI::EProgramStage::Vertex;

            HAL::ShaderCompileParams params;
            params.input = path;
            params.entry = String::StringToWString(entryPoint);
            params.shaderModel = L"6_0";
            params.target = L"-spirv";
            params.targetEnv = envStr;
            params.optimizeLevel = L"0";
            params.stage = stagePoint;
            params.defines = {};
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
        const char* GetName() const override { return "GPUParticleTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            RHIRenderingTestBase::SetupTest();

            InitCommonResources();

            // Programs
            m_ComputeProgram = CreateProgram(L"GPUParticle", RHI::SHADER_STAGE_COMPUTE_BIT, "CSMain");
            m_VertProgram = CreateProgram(L"GPUParticle", RHI::SHADER_STAGE_VERTEX_BIT, "VSMain");
            m_FragProgram = CreateProgram(L"GPUParticle", RHI::SHADER_STAGE_FRAGMENT_BIT, "PSMain");

            CreateResources();
            CreatePipelines();

            return true;
        }

        void TeardownTest() override
        {
            if (m_ParticleBuffer.IsValid()) m_Device->GetFactory()->ReleaseBuffer(m_ParticleBuffer);
            for (auto& ub : m_UboBuffer) if (ub.IsValid()) m_Device->GetFactory()->ReleaseBuffer(ub);

            if (m_ComputeProgram.IsValid()) m_Device->GetFactory()->ReleaseGPUProgram(m_ComputeProgram);
            if (m_VertProgram.IsValid()) m_Device->GetFactory()->ReleaseGPUProgram(m_VertProgram);
            if (m_FragProgram.IsValid()) m_Device->GetFactory()->ReleaseGPUProgram(m_FragProgram);

            m_ComputePso.reset();
            m_GraphicsPso.reset();

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
            // Particle Buffer
            RHI::RHIBufferDescriptor pDesc = {};
            pDesc.size = m_ParticleCount * sizeof(Particle);
            pDesc.usage = RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT | RHI::BUFFER_USAGE_TRANSFER_DST_BIT;
            pDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
            m_ParticleBuffer = m_Device->GetFactory()->CreateBuffer(std::move(pDesc), "ParticleBuffer");

            // Init particles
            Containers::Vector<Particle> particles(m_ParticleCount);
            for (auto& p : particles)
            {
                p.position = glm::vec4(
                    (rand() % 200 - 100) / 100.0f, // x: -1 to 1
                    (rand() % 100) / 100.0f - 2.0f, // y: starts low
                    (rand() % 200 - 100) / 100.0f, // z: -1 to 1
                    (rand() % 1000) / 100.0f // life
                );
                p.velocity = glm::vec4(
                    (rand() % 40 - 20) / 100.0f, // vx
                    (rand() % 100 + 50) / 100.0f, // vy: upward
                    (rand() % 40 - 20) / 100.0f, // vz
                    p.position.w // maxLife = initial life
                );
            }
            m_Device->GetFactory()->BufferMemoryCopy(m_ParticleBuffer, particles.data(),
                                                     m_ParticleCount * sizeof(Particle), 0);

            // UBO
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = 256; // Padded for safety or use correct struct size
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
                m_UboBuffer.push_back(m_Device->GetFactory()->CreateBuffer(std::move(ubDesc), "UBO"));
            }

            // Descriptors
            m_ComputeDescriptorPoolIds.clear();
            m_GraphicsDescriptorPoolIds.clear();
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                // Compute Pool Family
                Containers::Vector<RHI::EDescriptorType> cTypes = {
                    RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER, RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER
                };
                Containers::Vector<UInt32> cCounts = {128, 128};
                m_ComputeDescriptorPoolIds.push_back(m_DescriptorPool->AddPool(cTypes, cCounts, 128));

                // Graphics Pool Family
                Containers::Vector<RHI::EDescriptorType> gTypes = {
                    RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER, RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER
                };
                Containers::Vector<UInt32> gCounts = {128, 128};
                m_GraphicsDescriptorPoolIds.push_back(m_DescriptorPool->AddPool(gTypes, gCounts, 128));
            }
        }

        void CreatePipelines()
        {
            auto pm = m_Device->GetPipelineCache();

            // Compute Pipeline
            m_ComputePso = pm->GetPipelineState();
            m_ComputePso->SetBindPoint(RHI::PIPELINE_BIND_POINT_COMPUTE);
            m_ComputePso->AddProgram(m_ComputeProgram);

            m_ComputePso->BuildDescriptorSetLayout();

            m_ComputePipeline = pm->GetComputePipeline(m_ComputePso.get());

            // Graphics Pipeline
            m_GraphicsPso = pm->GetPipelineState();
            m_GraphicsPso->AddProgram(m_VertProgram);
            m_GraphicsPso->AddProgram(m_FragProgram);

            m_GraphicsPso->SetBindPoint(RHI::PIPELINE_BIND_POINT_GRAPHICS);

            RHI::RHIInputAssemblyState ia{};
            ia.topology = RHI::PRIMITIVE_TOPOLOGY_POINT_LIST;
            m_GraphicsPso->SetInputAssemblyState(ia);

            RHI::RHIRasterizationState rs{};
            rs.cullMode = RHI::CULL_MODE_NONE;
            m_GraphicsPso->SetRasterizationState(rs);

            RHI::RHIColorBlendState cb{};
            // Additive blending: SrcColor * 1 + DstColor * 1
            RHI::RHIColorBlendAttachmentState att{};
            att.blendEnable = true;
            att.colorWriteMask = 0xF;
            att.srcColorBlendFactor = RHI::BLEND_FACTOR_ONE;
            att.dstColorBlendFactor = RHI::BLEND_FACTOR_ONE;
            att.colorBlendOp = RHI::BLEND_OP_ADD;
            att.srcAlphaBlendFactor = RHI::BLEND_FACTOR_ONE;
            att.dstAlphaBlendFactor = RHI::BLEND_FACTOR_ZERO;
            att.alphaBlendOp = RHI::BLEND_OP_ADD;
            cb.attachments.push_back(att);
            m_GraphicsPso->SetColorBlendState(cb);

            m_GraphicsPso->BuildDescriptorSetLayout();
            m_GraphicsPso->SetDynamicStateMask(RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT);

            Containers::Vector<RHI::EFormat> colorFormats = {RHI::FORMAT_B8G8R8A8_SRGB};
            m_GraphicsPso->SetRenderingFormats(colorFormats, RHI::FORMAT_UNDEFINED, RHI::FORMAT_UNDEFINED);

            m_GraphicsPipeline = pm->GetGraphicsPipeline(m_GraphicsPso.get());
        }

        void UpdateUniformBuffer()
        {
            UpdateCamera((float)frameTime);
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);

            static auto startTime = std::chrono::high_resolution_clock::now();
            auto currentTime = std::chrono::high_resolution_clock::now();
            float time = std::chrono::duration<float, std::chrono::seconds::period>(currentTime - startTime).count();

            struct FireUBO
            {
                glm::mat4 model;
                glm::mat4 view;
                glm::mat4 projection;
                float mipmapBias;
                float time;
                float deltaTime;
                float padding;
            };

            FireUBO fireUbo;
            fireUbo.model = glm::mat4(1.0f);
            fireUbo.view = GetViewMatrix();
            fireUbo.projection = GetProjectionMatrix(width / height);
            fireUbo.mipmapBias = 0.0f;
            fireUbo.time = time;
            fireUbo.deltaTime = (float)frameTime;

            m_Device->GetFactory()->BufferMemoryCopy(m_UboBuffer[GetCurrentFrameIndex()], &fireUbo, sizeof(FireUBO), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();

            // Update Descriptors
            {
                m_DescriptorPool->ResetPool(m_ComputeDescriptorPoolIds[currentIndex]);
                m_DescriptorPool->ResetPool(m_GraphicsDescriptorPoolIds[currentIndex]);

                m_ComputePso->UpdateDescriptorSet(0, 0, Containers::Vector<RHI::RHIBufferHandle>{m_ParticleBuffer});
                m_ComputePso->UpdateDescriptorSet(0, 1, Containers::Vector<RHI::RHIBufferHandle>{
                                                      m_UboBuffer[currentIndex]
                                                  });

                UInt32 setIdx = m_DescriptorPool->AllocDescriptorSet(m_ComputeDescriptorPoolIds[currentIndex],
                                                                     (UInt32)0,
                                                                     (RHI::RHIPipelineState*)m_ComputePso.get());
                m_DescriptorPool->UpdateDescriptorSet(m_ComputeDescriptorPoolIds[currentIndex], setIdx,
                                                      m_ComputePso.get());
            }
            {
                m_GraphicsPso->UpdateDescriptorSet(0, 0, Containers::Vector<RHI::RHIBufferHandle>{m_ParticleBuffer});
                m_GraphicsPso->UpdateDescriptorSet(0, 1, Containers::Vector<RHI::RHIBufferHandle>{
                                                       m_UboBuffer[currentIndex]
                                                   });

                UInt32 setIdx = m_DescriptorPool->AllocDescriptorSet(m_GraphicsDescriptorPoolIds[currentIndex],
                                                                     (UInt32)0,
                                                                     (RHI::RHIPipelineState*)m_GraphicsPso.get());
                m_DescriptorPool->UpdateDescriptorSet(m_GraphicsDescriptorPoolIds[currentIndex], setIdx,
                                                      m_GraphicsPso.get());
            }

            auto pool = m_Device->GetCommandBufferPool(m_CmdPool);
            auto cmdHandle = pool->GetCommandBuffer(currentIndex);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);

            cmd->Begin(currentIndex, 0);

            // Compute Update
            cmd->BindPipeline(m_ComputePipeline);
            cmd->BindDescriptorSet(RHI::PIPELINE_BIND_POINT_COMPUTE, 0, m_DescriptorPoolHandle,
                                   m_ComputeDescriptorPoolIds[currentIndex], 0);
            cmd->Dispatch((m_ParticleCount + 255) / 256, 1, 1);

            // Barrier: Compute Write -> Vertex Read
            RHI::RHIBufferMemoryBarrier barrier = {};
            barrier.buffer = m_ParticleBuffer;
            barrier.srcAccessMask = RHI::ACCESS_SHADER_WRITE_BIT;
            barrier.dstAccessMask = RHI::ACCESS_SHADER_READ_BIT;
            barrier.srcStageMask = RHI::PIPELINE_STAGE_COMPUTE_SHADER_BIT;
            barrier.dstStageMask = RHI::PIPELINE_STAGE_VERTEX_SHADER_BIT;
            barrier.srcQueueFamilyIndex = 0xFFFFFFFF;
            barrier.dstQueueFamilyIndex = 0xFFFFFFFF;

            cmd->PipelineBarrier(RHI::PIPELINE_STAGE_COMPUTE_SHADER_BIT, RHI::PIPELINE_STAGE_VERTEX_SHADER_BIT, 0,
                                 &barrier, 1);

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
                att.clearValue.float32[0] = 0.0f;
                att.clearValue.float32[1] = 0.0f;
                att.clearValue.float32[2] = 0.0f;
                att.clearValue.float32[3] = 1.0f;
                renderInfo.pColorAttachments = &att;

                // Transition: UNDEFINED -> COLOR_ATTACHMENT_OPTIMAL
                cmd->TransitionImageLayout(colorBuffer, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);

                cmd->BeginRendering(renderInfo);
                cmd->BindPipeline(m_GraphicsPipeline);
                cmd->SetViewport(0, 0, (float)renderInfo.RHIRenderArea.width, (float)renderInfo.RHIRenderArea.height, 0,
                                 1);
                cmd->SetScissor(0, 0, renderInfo.RHIRenderArea.width, renderInfo.RHIRenderArea.height);
                cmd->BindDescriptorSet(RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPoolHandle,
                                       m_GraphicsDescriptorPoolIds[currentIndex], 0);
                cmd->Draw(m_ParticleCount, 1, 0, 0, 0);
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
            pool->ReleaseCommandBuffer(0, cmdHandle);
        }
    };
}
