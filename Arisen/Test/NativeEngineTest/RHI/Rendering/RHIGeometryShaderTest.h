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

    class RHIGeometryShaderTest : public RHIRenderingTestBase
    {
    private:
        std::unique_ptr<RHI::RHIPipelineState> m_Pso;
        RHI::RHIPipelineHandle m_Pipeline;
        Containers::Vector<RHI::RHIBufferHandle> m_UboBuffers;

        RHI::RHIShaderProgramHandle m_GsProgram;

        RHI::RHIImageHandle m_DepthImage;
        RHI::RHIImageViewHandle m_DepthView;

    public:
        const char* GetName() const override { return "GeometryShaderTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            RHIRenderingTestBase::SetupTest();

            InitCommonResources();

            // Override programs to include GS
            auto shaderEnv = GetShaderEnvString().ToWString();

            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();
            auto currentPath = exeDir.generic_wstring() + L"\\Shader";
            auto shaderPath = currentPath + L"\\GeometryShaderTest.hlsl";

            HAL::ShaderCompileParams vsParams;
            vsParams.input = shaderPath;
            vsParams.entry = L"vs_main";
            vsParams.stage = RHI::Vertex;
            vsParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput vsOut;
            if (!HAL::CompileShaderFromFile(std::move(vsParams), vsOut) || vsOut.codeSize == 0)
            {
                LOG_ERROR("Failed to compile VS for GeometryShaderTest");
                return false;
            }

            RHI::RHIShaderProgramDesc vsDesc;
            vsDesc.byteCode = vsOut.codePointer;
            vsDesc.codeSize = vsOut.codeSize;
            vsDesc.stage = RHI::SHADER_STAGE_VERTEX_BIT;
            vsDesc.entry = "vs_main";
            vsDesc.name = "GS_VS";
            m_VertProgram = m_Device->GetFactory()->CreateGPUProgram();
            m_Device->GetFactory()->AttachProgramByteCode(m_VertProgram, std::move(vsDesc));
            if (vsOut.codePointer) std::free(vsOut.codePointer);

            HAL::ShaderCompileParams gsParams;
            gsParams.input = shaderPath;
            gsParams.entry = L"gs_main";
            gsParams.stage = RHI::Geometry;
            gsParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput gsOut;
            if (!HAL::CompileShaderFromFile(std::move(gsParams), gsOut) || gsOut.codeSize == 0)
            {
                LOG_ERROR("Failed to compile GS for GeometryShaderTest");
                return false;
            }

            RHI::RHIShaderProgramDesc gsDesc;
            gsDesc.byteCode = gsOut.codePointer;
            gsDesc.codeSize = gsOut.codeSize;
            gsDesc.stage = RHI::SHADER_STAGE_GEOMETRY_BIT;
            gsDesc.entry = "gs_main";
            gsDesc.name = "GS_GS";
            m_GsProgram = m_Device->GetFactory()->CreateGPUProgram();
            m_Device->GetFactory()->AttachProgramByteCode(m_GsProgram, std::move(gsDesc));
            if (gsOut.codePointer) std::free(gsOut.codePointer);

            HAL::ShaderCompileParams psParams;
            psParams.input = shaderPath;
            psParams.entry = L"ps_main";
            psParams.stage = RHI::Fragment;
            psParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput psOut;
            if (!HAL::CompileShaderFromFile(std::move(psParams), psOut) || psOut.codeSize == 0)
            {
                LOG_ERROR("Failed to compile PS for GeometryShaderTest");
                return false;
            }

            RHI::RHIShaderProgramDesc psDesc;
            psDesc.byteCode = psOut.codePointer;
            psDesc.codeSize = psOut.codeSize;
            psDesc.stage = RHI::SHADER_STAGE_FRAGMENT_BIT;
            psDesc.entry = "ps_main";
            psDesc.name = "GS_PS";
            m_FragProgram = m_Device->GetFactory()->CreateGPUProgram();
            m_Device->GetFactory()->AttachProgramByteCode(m_FragProgram, std::move(psDesc));
            if (psOut.codePointer) std::free(psOut.codePointer);

            CreateCommonResources();
            CreateSizeDependentResources();
            InitRenderContext();
            CreatePipeline();

            return true;
        }

        void TeardownTest() override
        {
            if (m_DepthView.IsValid()) m_Device->GetFactory()->ReleaseImageView(m_DepthView);
            if (m_DepthImage.IsValid()) m_Device->GetFactory()->ReleaseImage(m_DepthImage);
            for (auto& ub : m_UboBuffers)
            {
                if (ub.IsValid()) m_Device->GetFactory()->ReleaseBuffer(ub);
            }
            if (m_GsProgram.IsValid()) m_Device->GetFactory()->ReleaseGPUProgram(m_GsProgram);

            m_Pso.reset();

            m_Model.Release(m_Device);

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

            if (m_DepthView.IsValid()) m_Device->GetFactory()->ReleaseImageView(m_DepthView);
            if (m_DepthImage.IsValid()) m_Device->GetFactory()->ReleaseImage(m_DepthImage);

            m_DepthView = {};
            m_DepthImage = {};

            CreateSizeDependentResources();
        }

    private:
        void CreateCommonResources()
        {
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = std::filesystem::path(exePathW).parent_path();

            std::filesystem::path duckPath = exeDir / "Assets" / "glTF-Sample-Models" / "2.0" / "Duck" / "glTF" /
                "Duck.gltf";
            m_Model = LoadGLTF(duckPath.string());

            // UBOs
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = sizeof(UniformBufferObject);
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
                m_UboBuffers.push_back(m_Device->GetFactory()->CreateBuffer(std::move(ubDesc), "UBO"));
            }

            m_CameraPos = glm::vec3(0.0f, 1.0f, 3.0f);
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

            // Depth Image
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
            m_DepthImage = m_Device->GetFactory()->CreateImage(std::move(dimgDesc), "DepthBuffer");

            RHI::RHIImageViewDesc dviewDesc = {};
            dviewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
            dviewDesc.format = RHI::FORMAT_D32_SFLOAT;
            dviewDesc.aspectMask = RHI::IMAGE_ASPECT_DEPTH_BIT;
            dviewDesc.levelCount = 1;
            dviewDesc.layerCount = 1;
            dviewDesc.width = width;
            dviewDesc.height = height;
            m_DepthView = m_Device->GetFactory()->CreateImageView(m_DepthImage, std::move(dviewDesc));
        }

    private:
        void InitRenderContext()
        {
            Containers::Vector<RHI::EDescriptorType> types = {
                RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                RHI::DESCRIPTOR_TYPE_SAMPLER
            };
            UInt32 matCount = (UInt32)m_Model.materials.size();
            if (matCount == 0) matCount = 1;
            Containers::Vector<UInt32> counts = {matCount, matCount, matCount};
            m_DescriptorPoolIds.push_back(m_DescriptorPool->AddPool(types, counts, matCount));
        }

        void CreatePipeline()
        {
            auto pm = m_Device->GetPipelineCache();
            m_Pso = pm->GetPipelineState();

            m_Pso->AddProgram(m_VertProgram);
            m_Pso->AddProgram(m_GsProgram);
            m_Pso->AddProgram(m_FragProgram);

            m_Pso->AddVertexBindingDescription(0, m_Model.layout.stride, RHI::VERTEX_INPUT_RATE_VERTEX);
            for (const auto& attr : m_Model.layout.attributes)
            {
                m_Pso->AddVertexInputAttributeDescription(attr.location, 0, attr.format, attr.offset);
            }

            RHI::RHIInputAssemblyState ia{};
            ia.topology = RHI::PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
            m_Pso->SetInputAssemblyState(ia);

            RHI::RHIRasterizationState rs{};
            rs.cullMode = RHI::CULL_MODE_NONE;
            m_Pso->SetRasterizationState(rs);

            RHI::RHIColorBlendState cb{};
            RHI::RHIColorBlendAttachmentState att{};
            att.blendEnable = false;
            att.colorWriteMask = RHI::COLOR_COMPONENT_R_BIT | RHI::COLOR_COMPONENT_G_BIT | RHI::COLOR_COMPONENT_B_BIT |
                RHI::COLOR_COMPONENT_A_BIT;
            cb.attachments.push_back(att);
            m_Pso->SetColorBlendState(cb);

            m_Pso->BuildDescriptorSetLayout();
            m_Pso->SetDynamicStateMask(RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT);

            Containers::Vector<RHI::EFormat> colorFormats = {RHI::FORMAT_B8G8R8A8_SRGB};
            m_Pso->SetRenderingFormats(colorFormats, RHI::FORMAT_D32_SFLOAT, RHI::FORMAT_UNDEFINED);

            m_Pipeline = pm->GetGraphicsPipeline(m_Pso.get());
        }

        void UpdateUniformBuffer()
        {
            UpdateCamera((float)frameTime);
            UniformBufferObject ubo;
            ubo.model = glm::mat4(1.0f); // Disabled rotation
            ubo.view = GetViewMatrix();
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);
            ubo.projection = GetProjectionMatrix(width / height);
            ubo.mipmapBias = 0.0f;

            m_Device->GetFactory()->BufferMemoryCopy(m_UboBuffers[GetCurrentFrameIndex()], &ubo,
                                                     sizeof(UniformBufferObject), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto pool = m_Device->GetCommandBufferPool(m_CmdPool);
            auto cmdHandle = pool->GetCommandBuffer(currentIndex);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);

            m_DescriptorPool->ResetPool(m_DescriptorPoolIds[0]);

            cmd->Begin(currentIndex, 0);

            auto colorBuffer = m_SwapChain->BeginFrame(currentIndex);

            if (colorBuffer.IsValid())
            {
                auto colorView = m_SwapChain->GetImageView(currentIndex);

                // Transition swapchain image: UNDEFINED -> COLOR_ATTACHMENT_OPTIMAL
                cmd->TransitionImageLayout(colorBuffer, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);

                RHI::RHIRenderingAttachmentInfo colorAttachment{};
                colorAttachment.imageView = colorView;
                colorAttachment.imageLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                colorAttachment.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                colorAttachment.storeOp = RHI::ATTACHMENT_STORE_OP_STORE;
                colorAttachment.clearValue.float32[0] = 0.1f;
                colorAttachment.clearValue.float32[1] = 0.1f;
                colorAttachment.clearValue.float32[2] = 0.1f;
                colorAttachment.clearValue.float32[3] = 1.0f;

                RHI::RHIRenderingAttachmentInfo depthAttachment{};
                depthAttachment.imageView = m_DepthView;
                depthAttachment.imageLayout = RHI::IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;
                depthAttachment.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                depthAttachment.storeOp = RHI::ATTACHMENT_STORE_OP_DONT_CARE;
                depthAttachment.clearValue.float32[0] = 1.0f;
                depthAttachment.clearValue.float32[1] = 0;

                UInt32 width = HAL::GetWindowWidth(m_WindowId);
                UInt32 height = HAL::GetWindowHeight(m_WindowId);

                RHI::RHIRenderingInfo renderInfo{};
                renderInfo.RHIRenderArea = {0, 0, width, height};
                renderInfo.layerCount = 1;
                renderInfo.colorAttachmentCount = 1;
                renderInfo.pColorAttachments = &colorAttachment;
                renderInfo.pDepthAttachment = &depthAttachment;

                cmd->BeginRendering(renderInfo);
                cmd->BindPipeline(m_Pipeline);
                cmd->SetViewport(0, 0, (float)width, (float)height, 0, 1);
                cmd->SetScissor(0, 0, width, height);

                cmd->BindVertexBuffers(m_Model.vertexBuffer, 0);
                cmd->BindIndexBuffer(m_Model.indexBuffer, 0, RHI::INDEX_TYPE_UINT32);

                for (const auto& prim : m_Model.primitives)
                {
                    auto& mat = m_Model.materials[prim.materialIndex >= 0 ? prim.materialIndex : 0];

                    m_Pso->UpdateDescriptorSet(0, 0, Containers::Vector<RHI::RHIBufferHandle>{
                                                   m_UboBuffers[currentIndex]
                                               });

                    RHI::RHIDescriptorImageInfo texInfo = {};
                    texInfo.imageView = mat.baseColorView;
                    texInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                    m_Pso->UpdateDescriptorSet(0, 1, Containers::Vector<RHI::RHIDescriptorImageInfo>{texInfo});

                    RHI::RHIDescriptorImageInfo samInfo = {};
                    samInfo.sampler = mat.sampler;
                    samInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                    m_Pso->UpdateDescriptorSet(0, 2, Containers::Vector<RHI::RHIDescriptorImageInfo>{samInfo});

                    UInt32 setIdx = m_DescriptorPool->AllocDescriptorSet(
                        m_DescriptorPoolIds[0], (UInt32)0, (RHI::RHIPipelineState*)m_Pso.get());
                    m_DescriptorPool->UpdateDescriptorSet(m_DescriptorPoolIds[0], setIdx, m_Pso.get());


                    cmd->BindDescriptorSet(RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPoolHandle,
                                           m_DescriptorPoolIds[0], setIdx);
                    cmd->DrawIndexed(prim.indexCount, 1, prim.firstIndex, 0, 0, 0);
                }

                cmd->EndRendering();

                // Transition swapchain image: COLOR_ATTACHMENT_OPTIMAL -> PRESENT_SRC_KHR
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
