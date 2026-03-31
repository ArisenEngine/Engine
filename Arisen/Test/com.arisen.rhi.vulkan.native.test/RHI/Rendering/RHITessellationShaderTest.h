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

    class RHITessellationShaderTest : public RHIRenderingTestBase
    {
    private:
        struct TessellationUBO
        {
            glm::mat4 model;
            glm::mat4 view;
            glm::mat4 projection;
            float time;
            float tessLevel;
            float waveAmplitude;
            float waveFrequency;
        };

        std::unique_ptr<RHI::RHIPipelineState> m_Pso;
        std::unique_ptr<RHI::RHIPipelineState> m_WireframePso;
        RHI::RHIPipelineHandle m_Pipeline;
        RHI::RHIPipelineHandle m_WireframePipeline;

        Containers::Vector<RHI::RHIBufferHandle> m_UboBuffers;

        RHI::RHIShaderProgramHandle m_HsProgram;
        RHI::RHIShaderProgramHandle m_DsProgram;


        RHI::RHIImageHandle m_DepthImage;
        RHI::RHIImageViewHandle m_DepthView;

        float m_AccumulatedTime = 0.0f;
        bool m_ShowWireframe = true;

    public:
        const char* GetName() const override { return "TessellationShaderTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            RHIRenderingTestBase::SetupTest();

            InitCommonResources();

            auto shaderEnv = GetShaderEnvString().ToWString();

            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();
            auto shaderPath = (exeDir / L"Shader" / L"TessellationShaderTest.hlsl").wstring();

            auto factory = m_Device->GetFactory();
            // VS
            HAL::ShaderCompileParams vsParams;
            vsParams.input = shaderPath;
            vsParams.entry = L"vs_main";
            vsParams.stage = RHI::EProgramStage::Vertex;
            vsParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput vsOut;
            if (HAL::CompileShaderFromFile(std::move(vsParams), vsOut) && vsOut.codeSize > 0)
            {
                m_VertProgram = factory->CreateGPUProgram();
                RHI::RHIShaderProgramDesc vsDesc = {
                    vsOut.codeSize, vsOut.codePointer, "vs_main", "Tess_VS", RHI::SHADER_STAGE_VERTEX_BIT
                };
                factory->AttachProgramByteCode(m_VertProgram, std::move(vsDesc));
                if (vsOut.codePointer) std::free(vsOut.codePointer);
            }

            // HS
            HAL::ShaderCompileParams hsParams;
            hsParams.input = shaderPath;
            hsParams.entry = L"hs_main";
            hsParams.stage = RHI::EProgramStage::Hull;
            hsParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput hsOut;
            if (HAL::CompileShaderFromFile(std::move(hsParams), hsOut) && hsOut.codeSize > 0)
            {
                m_HsProgram = factory->CreateGPUProgram();
                RHI::RHIShaderProgramDesc hsDesc = {
                    hsOut.codeSize, hsOut.codePointer, "hs_main", "Tess_HS", RHI::SHADER_STAGE_TESSELLATION_CONTROL_BIT
                };
                factory->AttachProgramByteCode(m_HsProgram, std::move(hsDesc));
                if (hsOut.codePointer) std::free(hsOut.codePointer);
            }

            // DS
            HAL::ShaderCompileParams dsParams;
            dsParams.input = shaderPath;
            dsParams.entry = L"ds_main";
            dsParams.stage = RHI::EProgramStage::Domain;
            dsParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput dsOut;
            if (HAL::CompileShaderFromFile(std::move(dsParams), dsOut) && dsOut.codeSize > 0)
            {
                m_DsProgram = factory->CreateGPUProgram();
                RHI::RHIShaderProgramDesc dsDesc = {
                    dsOut.codeSize, dsOut.codePointer, "ds_main", "Tess_DS",
                    RHI::SHADER_STAGE_TESSELLATION_EVALUATION_BIT
                };
                factory->AttachProgramByteCode(m_DsProgram, std::move(dsDesc));
                if (dsOut.codePointer) std::free(dsOut.codePointer);
            }

            // PS
            HAL::ShaderCompileParams psParams;
            psParams.input = shaderPath;
            psParams.entry = L"ps_main";
            psParams.stage = RHI::EProgramStage::Fragment;
            psParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput psOut;
            if (HAL::CompileShaderFromFile(std::move(psParams), psOut) && psOut.codeSize > 0)
            {
                m_FragProgram = factory->CreateGPUProgram();
                RHI::RHIShaderProgramDesc psDesc = {
                    psOut.codeSize, psOut.codePointer, "ps_main", "Tess_PS", RHI::SHADER_STAGE_FRAGMENT_BIT
                };
                factory->AttachProgramByteCode(m_FragProgram, std::move(psDesc));
                if (psOut.codePointer) std::free(psOut.codePointer);
            }

            CreateCommonResources();
            CreateSizeDependentResources();
            InitRenderContext();
            CreatePipelines();

            return true;
        }

        void TeardownTest() override
        {
            if (m_Device) m_Device->DeviceWaitIdle();

            auto factory = m_Device->GetFactory();
            if (m_DepthView.IsValid()) factory->ReleaseImageView(m_DepthView);
            if (m_DepthImage.IsValid()) factory->ReleaseImage(m_DepthImage);
            for (auto& ub : m_UboBuffers) if (ub.IsValid()) factory->ReleaseBuffer(ub);
            if (m_HsProgram.IsValid()) factory->ReleaseGPUProgram(m_HsProgram);
            if (m_DsProgram.IsValid()) factory->ReleaseGPUProgram(m_DsProgram);

            m_Pso.reset();
            m_WireframePso.reset();

            m_Model.Release(m_Device);
            RHIRenderingTestBase::TeardownTest();
        }

    protected:
        void RenderFrame() override
        {
            auto currentIndex = GetCurrentFrameIndex();
            if (m_FrameTickets[currentIndex] > 0) m_Device->GetQueue(RHI::RHIQueueType::Graphics)->WaitForTicket(m_FrameTickets[currentIndex]);

            m_AccumulatedTime += (float)frameTime;
            UpdateUniformBuffer();
            RecordAndSubmit();

            NextFrame();
        }

        void OnResize(UInt32 width, UInt32 height) override
        {
            if (width == 0 || height == 0) return;

            m_Device->DeviceWaitIdle();

            auto factory = m_Device->GetFactory();
            if (m_DepthView.IsValid()) factory->ReleaseImageView(m_DepthView);
            if (m_DepthImage.IsValid()) factory->ReleaseImage(m_DepthImage);

            m_DepthView = {};
            m_DepthImage = {};

            CreateSizeDependentResources();
        }

    private:
        void CreateCommonResources()
        {
            struct Vertex
            {
                glm::vec3 pos;
                glm::vec2 uv;
            };

            const int gridDim = 20;
            const float size = 10.0f;
            Containers::Vector<Vertex> vertices;
            Containers::Vector<UInt32> indices;

            for (int y = 0; y < gridDim; ++y)
            {
                for (int x = 0; x < gridDim; ++x)
                {
                    float xPos = (x / (float)gridDim) * size - size * 0.5f;
                    float yPos = (y / (float)gridDim) * size - size * 0.5f;
                    float step = size / (float)gridDim;

                    vertices.push_back({{xPos, 0, yPos}, {x / (float)gridDim, y / (float)gridDim}});
                    vertices.push_back({{xPos + step, 0, yPos}, {(x + 1) / (float)gridDim, y / (float)gridDim}});
                    vertices.push_back({
                        {xPos + step, 0, yPos + step}, {(x + 1) / (float)gridDim, (y + 1) / (float)gridDim}
                    });
                    vertices.push_back({{xPos, 0, yPos + step}, {x / (float)gridDim, (y + 1) / (float)gridDim}});

                    UInt32 base = (y * gridDim + x) * 4;
                    indices.push_back(base);
                    indices.push_back(base + 1);
                    indices.push_back(base + 2);
                    indices.push_back(base + 3);
                }
            }

            m_Model.vertexCount = (UInt32)vertices.size();
            m_Model.indexCount = (UInt32)indices.size();

            auto factory = m_Device->GetFactory();
            RHI::RHIBufferDescriptor vbDesc = {};
            vbDesc.size = vertices.size() * sizeof(Vertex);
            vbDesc.usage = RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT;
            vbDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
            m_Model.vertexBuffer = factory->CreateBuffer(std::move(vbDesc), "PatchVB");
            m_Device->GetFactory()->BufferMemoryCopy(m_Model.vertexBuffer, vertices.data(),
                                                     vertices.size() * sizeof(Vertex), 0);

            RHI::RHIBufferDescriptor ibDesc = {};
            ibDesc.size = indices.size() * sizeof(UInt32);
            ibDesc.usage = RHI::BUFFER_USAGE_INDEX_BUFFER_BIT;
            ibDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
            m_Model.indexBuffer = factory->CreateBuffer(std::move(ibDesc), "PatchIB");
            m_Device->GetFactory()->BufferMemoryCopy(m_Model.indexBuffer, indices.data(),
                                                     indices.size() * sizeof(UInt32), 0);

            m_Model.layout.stride = sizeof(Vertex);
            m_Model.layout.attributes.push_back({"pos", RHI::FORMAT_R32G32B32_SFLOAT, 0, 0});
            m_Model.layout.attributes.push_back({"uv", RHI::FORMAT_R32G32_SFLOAT, sizeof(glm::vec3), 1});

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = sizeof(TessellationUBO);
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
                m_UboBuffers.push_back(factory->CreateBuffer(std::move(ubDesc), "TessUBO"));
            }

            m_CameraPos = glm::vec3(0.0f, 5.0f, 10.0f);
        }

        void CreateSizeDependentResources()
        {
            UInt32 width = HAL::GetWindowWidth(m_WindowId);
            UInt32 height = HAL::GetWindowHeight(m_WindowId);

            if (width == 0 || height == 0)
            {
                width = 1280;
                height = 720;
            }

            auto factory = m_Device->GetFactory();
            RHI::RHIImageDescriptor dimgDesc = {};
            dimgDesc.imageType = RHI::IMAGE_TYPE_2D;
            dimgDesc.width = width;
            dimgDesc.height = height;
            dimgDesc.depth = 1;
            dimgDesc.mipLevels = 1;
            dimgDesc.arrayLayers = 1;
            dimgDesc.format = RHI::FORMAT_D32_SFLOAT;
            dimgDesc.tiling = RHI::IMAGE_TILING_OPTIMAL;
            dimgDesc.usage = RHI::IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT;
            dimgDesc.sampleCount = RHI::SAMPLE_COUNT_1_BIT;
            dimgDesc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;
            m_DepthImage = factory->CreateImage(std::move(dimgDesc), "DepthBuffer");

            RHI::RHIImageViewDesc dviewDesc = {};
            dviewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
            dviewDesc.format = RHI::FORMAT_D32_SFLOAT;
            dviewDesc.aspectMask = RHI::IMAGE_ASPECT_DEPTH_BIT;
            dviewDesc.levelCount = 1;
            dviewDesc.layerCount = 1;
            dviewDesc.width = width;
            dviewDesc.height = height;
            m_DepthView = factory->CreateImageView(m_DepthImage, std::move(dviewDesc));
        }

    private:
        void InitRenderContext()
        {
            m_DescriptorPoolIds.push_back(m_DescriptorPool->AddPool({RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER}, {1}, 1));
        }

        void CreatePipelines()
        {
            auto cache = m_Device->GetPipelineCache();
            m_Pso = cache->GetPipelineState();
            m_Pso->AddProgram(m_VertProgram);
            m_Pso->AddProgram(m_HsProgram);
            m_Pso->AddProgram(m_DsProgram);
            m_Pso->AddProgram(m_FragProgram);

            m_Pso->AddVertexBindingDescription(0, m_Model.layout.stride, RHI::VERTEX_INPUT_RATE_VERTEX);
            m_Pso->AddVertexInputAttributeDescription(0, 0, RHI::FORMAT_R32G32B32_SFLOAT, 0);
            m_Pso->AddVertexInputAttributeDescription(1, 0, RHI::FORMAT_R32G32_SFLOAT, sizeof(glm::vec3));

            RHI::RHIInputAssemblyState ia{};
            ia.topology = RHI::PRIMITIVE_TOPOLOGY_PATCH_LIST;
            m_Pso->SetInputAssemblyState(ia);

            RHI::RHITessellationState ts{};
            ts.patchControlPoints = 4;
            m_Pso->SetTessellationState(ts);

            RHI::RHIRasterizationState rs{};
            rs.cullMode = RHI::CULL_MODE_NONE;
            rs.polygonMode = RHI::EPOLYGON_MODE_FILL;
            m_Pso->SetRasterizationState(rs);

            RHI::RHIColorBlendState cb{};
            RHI::RHIColorBlendAttachmentState att{};
            att.blendEnable = false;
            att.colorWriteMask = 0xF;
            cb.attachments.push_back(att);
            m_Pso->SetColorBlendState(cb);

            m_Pso->BuildDescriptorSetLayout();
            m_Pso->SetDynamicStateMask(RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT);

            RHI::RHIDepthStencilState ds{};
            ds.depthTestEnable = true;
            ds.depthWriteEnable = true;
            ds.depthCompareOp = RHI::COMPARE_OP_LESS;
            m_Pso->SetDepthStencilState(ds);

            Containers::Vector<RHI::EFormat> colorFormats = {RHI::FORMAT_B8G8R8A8_SRGB};
            m_Pso->SetRenderingFormats(colorFormats, RHI::FORMAT_D32_SFLOAT, RHI::FORMAT_UNDEFINED);

            m_Pipeline = cache->GetGraphicsPipeline(m_Pso.get());

            // Wireframe PSO
            m_WireframePso = cache->GetPipelineState();
            m_WireframePso->AddProgram(m_VertProgram);
            m_WireframePso->AddProgram(m_HsProgram);
            m_WireframePso->AddProgram(m_DsProgram);
            m_WireframePso->AddProgram(m_FragProgram);
            m_WireframePso->AddVertexBindingDescription(0, m_Model.layout.stride, RHI::VERTEX_INPUT_RATE_VERTEX);
            m_WireframePso->AddVertexInputAttributeDescription(0, 0, RHI::FORMAT_R32G32B32_SFLOAT, 0);
            m_WireframePso->AddVertexInputAttributeDescription(1, 0, RHI::FORMAT_R32G32_SFLOAT, sizeof(glm::vec3));
            m_WireframePso->SetInputAssemblyState(ia);
            m_WireframePso->SetTessellationState(ts);

            RHI::RHIRasterizationState wireRs = rs;
            wireRs.polygonMode = RHI::EPOLYGON_MODE_LINE;
            m_WireframePso->SetRasterizationState(wireRs);
            m_WireframePso->SetColorBlendState(cb);
            m_WireframePso->BuildDescriptorSetLayout();
            m_WireframePso->SetDynamicStateMask(RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT);
            m_WireframePso->SetDepthStencilState(ds);
            m_WireframePso->SetRenderingFormats(colorFormats, RHI::FORMAT_D32_SFLOAT, RHI::FORMAT_UNDEFINED);

            m_WireframePipeline = cache->GetGraphicsPipeline(m_WireframePso.get());
        }

        void UpdateUniformBuffer()
        {
            UpdateCamera((float)frameTime);
            TessellationUBO ubo;
            ubo.model = glm::mat4(1.0f);
            ubo.view = GetViewMatrix();
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);
            ubo.projection = GetProjectionMatrix(width / height);
            ubo.time = m_AccumulatedTime;
            ubo.tessLevel = 32.0f;
            ubo.waveAmplitude = 0.5f;
            ubo.waveFrequency = 2.0f;

            m_Device->GetFactory()->BufferMemoryCopy(m_UboBuffers[GetCurrentFrameIndex()], &ubo,
                                                     sizeof(TessellationUBO), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto pool = m_Device->GetCommandBufferPool(m_CmdPool);
            auto cmdHandle = pool->GetCommandBuffer(currentIndex);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);

            m_DescriptorPool->ResetPool(m_DescriptorPoolIds[0]);

            auto activePso = m_ShowWireframe ? m_WireframePso.get() : m_Pso.get();
            auto activePipeline = m_ShowWireframe ? m_WireframePipeline : m_Pipeline;

            activePso->UpdateDescriptorSet(0, 0, Containers::Vector<RHI::RHIBufferHandle>{m_UboBuffers[currentIndex]});

            UInt32 setIdx = m_DescriptorPool->AllocDescriptorSet(m_DescriptorPoolIds[0], (UInt32)0,
                                                                 (RHI::RHIPipelineState*)activePso);
            m_DescriptorPool->UpdateDescriptorSet(m_DescriptorPoolIds[0], setIdx, activePso);

            cmd->Begin(currentIndex, 0);

            auto colorBuffer = m_SwapChain->BeginFrame(currentIndex);
            if (colorBuffer.IsValid())
            {
                auto colorView = m_SwapChain->GetImageView(currentIndex);
                cmd->TransitionImageLayout(colorBuffer, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);

                RHI::RHIRenderingAttachmentInfo colorAttachment{};
                colorAttachment.imageView = colorView;
                colorAttachment.imageLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                colorAttachment.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                colorAttachment.storeOp = RHI::ATTACHMENT_STORE_OP_STORE;
                colorAttachment.clearValue.float32[0] = 0.05f;
                colorAttachment.clearValue.float32[1] = 0.05f;
                colorAttachment.clearValue.float32[2] = 0.1f;
                colorAttachment.clearValue.float32[3] = 1.0f;

                RHI::RHIRenderingAttachmentInfo depthAttachment{};
                depthAttachment.imageView = m_DepthView;
                depthAttachment.imageLayout = RHI::IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;
                depthAttachment.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                depthAttachment.storeOp = RHI::ATTACHMENT_STORE_OP_DONT_CARE;
                depthAttachment.clearValue.float32[0] = 1.0f;

                RHI::RHIRenderingInfo renderInfo{};
                renderInfo.RHIRenderArea = {
                    0, 0, (UInt32)HAL::GetWindowWidth(m_WindowId), (UInt32)HAL::GetWindowHeight(m_WindowId)
                };
                renderInfo.layerCount = 1;
                renderInfo.colorAttachmentCount = 1;
                renderInfo.pColorAttachments = &colorAttachment;
                renderInfo.pDepthAttachment = &depthAttachment;

                cmd->BeginRendering(renderInfo);
                cmd->BindPipeline(activePipeline);
                cmd->SetViewport(0, 0, (float)renderInfo.RHIRenderArea.width, (float)renderInfo.RHIRenderArea.height, 0,
                                 1);
                cmd->SetScissor(0, 0, renderInfo.RHIRenderArea.width, renderInfo.RHIRenderArea.height);
                cmd->BindVertexBuffers(m_Model.vertexBuffer, 0);
                cmd->BindIndexBuffer(m_Model.indexBuffer, 0, RHI::INDEX_TYPE_UINT32);
                cmd->BindDescriptorSet(RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPoolHandle,
                                       m_DescriptorPoolIds[0], setIdx);
                cmd->DrawIndexed(m_Model.indexCount, 1, 0, 0, 0, 0);
                cmd->EndRendering();

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
