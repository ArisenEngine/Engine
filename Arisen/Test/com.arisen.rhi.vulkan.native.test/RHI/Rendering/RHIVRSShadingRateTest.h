#pragma once

#include "../RHIRenderingTestBase.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Presentation/RHISwapChain.h"

#include <memory>
#include "RHI/Pipeline/RHIPipelineCache.h"
#include "RHI/Pipeline/RHIPipelineState.h"
#include "RHI/Descriptors/RHIDescriptorPool.h"

namespace ArisenEngine::Testing
{
    using namespace ArisenEngine;

    class RHIVRSShadingRateTest : public RHIRenderingTestBase
    {
    private:
        std::unique_ptr<RHI::RHIPipelineState> m_Pso;
        RHI::RHIPipelineHandle m_Pipeline;

        struct UniformBufferObject
        {
            glm::mat4 model;
            glm::mat4 view;
            glm::mat4 proj;
        };

        Containers::Vector<RHI::RHIBufferHandle> m_UboBuffers;

        RHI::RHIImageHandle m_DepthImage;
        RHI::RHIImageViewHandle m_DepthView;

    public:
        const char* GetName() const override { return "VRSShadingRateTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            if (!RHIRenderingTestBase::SetupTest()) return false;

            InitCommonResources();
            InitShaderProgram(L"VRSShadingRate");

            // Load Sponza Model
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = std::filesystem::path(exePathW).parent_path();
            std::filesystem::path modelPath = exeDir / "Assets" / "glTF-Sample-Models" / "2.0" / "Sponza" / "glTF" /
                "Sponza.gltf";
            m_Model = LoadGLTF(modelPath.string());

            CreateCommonResources();
            CreateSizeDependentResources();
            CreatePipeline();

            m_CameraPos = glm::vec3(0.0f, 1.5f, 5.0f);
            m_CameraRot = glm::vec3(0.0f, -glm::half_pi<float>(), 0.0f);

            return true;
        }

        void TeardownTest() override
        {
            auto factory = m_Device->GetFactory();
            if (m_DepthView.IsValid()) factory->ReleaseImageView(m_DepthView);
            if (m_DepthImage.IsValid()) factory->ReleaseImage(m_DepthImage);
            for (auto& ubo : m_UboBuffers)
            {
                if (ubo.IsValid()) factory->ReleaseBuffer(ubo);
            }
            m_Pso.reset();

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

            UpdateCameraData();
            RecordAndSubmit();
            NextFrame();
        }

        void OnResize(UInt32 width, UInt32 height) override
        {
            if (width == 0 || height == 0) return;

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
            auto factory = m_Device->GetFactory();
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor uboDesc = {};
                uboDesc.size = sizeof(UniformBufferObject);
                uboDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                uboDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
                m_UboBuffers.push_back(factory->CreateBuffer(std::move(uboDesc), "VRS_UBO"));
            }
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
            m_DepthImage = factory->CreateImage(std::move(dimgDesc), "VRS_DepthBuffer");

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

        void CreatePipeline()
        {
            auto cache = m_Device->GetPipelineCache();
            m_Pso = cache->GetPipelineState();

            m_Pso->AddProgram(m_VertProgram);
            m_Pso->AddProgram(m_FragProgram);

            m_Pso->AddVertexBindingDescription(0, sizeof(GLTFVertex), RHI::VERTEX_INPUT_RATE_VERTEX);
            m_Pso->AddVertexInputAttributeDescription(0, 0, RHI::FORMAT_R32G32B32_SFLOAT, offsetof(GLTFVertex, pos));
            m_Pso->AddVertexInputAttributeDescription(1, 0, RHI::FORMAT_R32G32B32_SFLOAT, offsetof(GLTFVertex, normal));
            m_Pso->AddVertexInputAttributeDescription(2, 0, RHI::FORMAT_R32G32_SFLOAT, offsetof(GLTFVertex, uv));
            m_Pso->AddVertexInputAttributeDescription(3, 0, RHI::FORMAT_R32G32B32A32_SFLOAT,
                                                      offsetof(GLTFVertex, color));

            RHI::RHIInputAssemblyState ia{};
            ia.topology = RHI::PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
            m_Pso->SetInputAssemblyState(ia);

            RHI::RHIRasterizationState rs{};
            rs.cullMode = RHI::CULL_MODE_BACK_BIT;
            rs.polygonMode = RHI::EPOLYGON_MODE_FILL;
            rs.lineWidth = 1.0f;
            m_Pso->SetRasterizationState(rs);

            RHI::RHIMultisampleState ms{};
            ms.rasterizationSamples = RHI::SAMPLE_COUNT_1_BIT;
            m_Pso->SetMultisampleState(ms);

            RHI::RHIDepthStencilState ds{};
            ds.depthTestEnable = true;
            ds.depthWriteEnable = true;
            ds.depthCompareOp = RHI::COMPARE_OP_LESS_OR_EQUAL;
            m_Pso->SetDepthStencilState(ds);

            RHI::RHIColorBlendState cb{};
            RHI::RHIColorBlendAttachmentState blendAttachment{};
            blendAttachment.blendEnable = false;
            blendAttachment.colorWriteMask = RHI::COLOR_COMPONENT_R_BIT | RHI::COLOR_COMPONENT_G_BIT |
                RHI::COLOR_COMPONENT_B_BIT | RHI::COLOR_COMPONENT_A_BIT;
            cb.attachments.push_back(blendAttachment);
            m_Pso->SetColorBlendState(cb);

            m_Pso->SetDynamicStateMask(
                RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT |
                RHI::DYNAMIC_STATE_FRAGMENT_SHADING_RATE_BIT);

            Containers::Vector<RHI::EFormat> colorFormats = {RHI::FORMAT_B8G8R8A8_SRGB};
            m_Pso->SetRenderingFormats(colorFormats, RHI::FORMAT_D32_SFLOAT, RHI::FORMAT_UNDEFINED);

            m_Pso->UpdateDescriptorSet(0, 0, {m_UboBuffers[0]});

            if (!m_Model.materials.empty())
            {
                auto& mat = m_Model.materials[0];
                RHI::RHIDescriptorImageInfo imgInfo{};
                imgInfo.imageView = mat.baseColorView;
                imgInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                m_Pso->UpdateDescriptorSet(0, 1, {imgInfo});

                RHI::RHIDescriptorImageInfo samplerInfo{};
                samplerInfo.sampler = mat.sampler;
                m_Pso->UpdateDescriptorSet(0, 2, {samplerInfo});
            }

            m_Pso->BuildDescriptorSetLayout();

            UInt32 matCount = (UInt32)m_Model.materials.size();
            if (matCount == 0) matCount = 1;

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                m_DescriptorPoolIds.push_back(m_DescriptorPool->AddPool({
                                                                            RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                                                                            RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                                                                            RHI::DESCRIPTOR_TYPE_SAMPLER
                                                                        }, {matCount, matCount, matCount}, matCount));
            }

            m_Pipeline = cache->GetGraphicsPipeline(m_Pso.get());
        }

        void UpdateCameraData()
        {
            UpdateCamera((float)frameTime);
            UInt32 width = HAL::GetWindowWidth(m_WindowId);
            UInt32 height = HAL::GetWindowHeight(m_WindowId);

            UniformBufferObject ubo = {};
            ubo.model = glm::mat4(1.0f);
            ubo.view = GetViewMatrix();
            ubo.proj = GetProjectionMatrix((float)width / (float)height);

            m_Device->GetFactory()->BufferMemoryCopy(m_UboBuffers[GetCurrentFrameIndex()], &ubo,
                                                     sizeof(UniformBufferObject), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto pool = m_Device->GetCommandBufferPool(m_CmdPool);
            auto cmdHandle = pool->GetCommandBuffer(currentIndex);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);

            UInt32 poolId = m_DescriptorPoolIds[currentIndex];
            m_DescriptorPool->ResetPool(poolId);

            Containers::Vector<UInt32> setIndices;
            for (UInt32 i = 0; i < (UInt32)m_Model.materials.size(); ++i)
            {
                auto& mat = m_Model.materials[i];
                m_Pso->UpdateDescriptorSet(0, 0, {m_UboBuffers[currentIndex]});

                RHI::RHIDescriptorImageInfo texInfo = {};
                texInfo.imageView = mat.baseColorView;
                texInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                m_Pso->UpdateDescriptorSet(0, 1, {texInfo});

                RHI::RHIDescriptorImageInfo samInfo = {};
                samInfo.sampler = mat.sampler;
                m_Pso->UpdateDescriptorSet(0, 2, {samInfo});

                UInt32 setIdx = m_DescriptorPool->AllocDescriptorSet(poolId, (UInt32)0,
                                                                     (RHI::RHIPipelineState*)m_Pso.get());
                m_DescriptorPool->UpdateDescriptorSet(poolId, setIdx, m_Pso.get());
                setIndices.push_back(setIdx);
            }

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
                colorAttachment.clearValue.float32[2] = 0.05f;
                colorAttachment.clearValue.float32[3] = 1.0f;

                RHI::RHIRenderingAttachmentInfo depthAttachment{};
                depthAttachment.imageView = m_DepthView;
                depthAttachment.imageLayout = RHI::IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;
                depthAttachment.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                depthAttachment.storeOp = RHI::ATTACHMENT_STORE_OP_DONT_CARE;
                depthAttachment.clearValue.float32[0] = 1.0f;

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

                cmd->BindVertexBuffers(m_Model.vertexBuffer, 0);
                cmd->BindIndexBuffer(m_Model.indexBuffer, 0, RHI::INDEX_TYPE_UINT32);

                RHI::EShadingRate rates[] = {
                    RHI::EShadingRate::Rate1x1,
                    RHI::EShadingRate::Rate2x2,
                    RHI::EShadingRate::Rate4x4
                };
                RHI::EShadingRateCombiner combiners[2] = {
                    RHI::EShadingRateCombiner::Keep, RHI::EShadingRateCombiner::Keep
                };

                for (int i = 0; i < 3; ++i)
                {
                    float quadWidth = (float)width / 3.0f;
                    float xPos = i * quadWidth;

                    cmd->SetViewport(xPos, 0, quadWidth, (float)height, 0, 1);
                    cmd->SetScissor((UInt32)xPos, 0, (UInt32)quadWidth, height);
                    cmd->SetFragmentShadingRate(rates[i], combiners);

                    for (const auto& prim : m_Model.primitives)
                    {
                        UInt32 setIdx = prim.materialIndex >= 0 ? setIndices[prim.materialIndex] : 0;
                        cmd->BindDescriptorSet(RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPoolHandle, poolId,
                                               setIdx);
                        cmd->DrawIndexed(prim.indexCount, 1, prim.firstIndex, 0, 0, 0);
                    }
                }

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
