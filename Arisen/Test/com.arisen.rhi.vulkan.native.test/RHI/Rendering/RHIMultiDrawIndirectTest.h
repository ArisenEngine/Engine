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

    struct IndirectCommand
    {
        UInt32 indexCount;
        UInt32 instanceCount;
        UInt32 firstIndex;
        SInt32 vertexOffset;
        UInt32 firstInstance;
    };

    struct MaterialGroup
    {
        SInt32 materialIndex;
        UInt32 commandOffset;
        UInt32 commandCount;
    };

    class RHIMultiDrawIndirectTest : public RHIRenderingTestBase
    {
    private:
        std::unique_ptr<RHI::RHIPipelineState> m_Pso;
        RHI::RHIPipelineHandle m_Pipeline;
        Containers::Vector<RHI::RHIBufferHandle> m_UboBuffer;
        RHI::RHIBufferHandle m_IndirectBuffer;
        RHI::RHIBufferHandle m_CountBuffer;

        RHI::RHIImageHandle m_DepthImage;
        RHI::RHIImageViewHandle m_DepthView;
        RHI::RHIImageHandle m_MSAAColorImage;
        RHI::RHIImageViewHandle m_MSAAColorView;
        RHI::ESampleCountFlagBits m_SampleCount = RHI::SAMPLE_COUNT_4_BIT;

        Containers::Vector<IndirectCommand> m_IndirectCommands;
        Containers::Vector<MaterialGroup> m_MaterialGroups;

    public:
        const char* GetName() const override { return "MultiDrawIndirectTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            RHIRenderingTestBase::SetupTest();

            InitCommonResources();
            InitShaderProgram(L"StandardTest");

            CreateCommonResources();
            CreateSizeDependentResources();
            InitRenderContext();
            CreatePipeline();

            return true;
        }

        void TeardownTest() override
        {
            if (m_IndirectBuffer.IsValid()) m_Device->GetFactory()->ReleaseBuffer(m_IndirectBuffer);
            if (m_DepthView.IsValid()) m_Device->GetFactory()->ReleaseImageView(m_DepthView);
            if (m_DepthImage.IsValid()) m_Device->GetFactory()->ReleaseImage(m_DepthImage);
            if (m_MSAAColorView.IsValid()) m_Device->GetFactory()->ReleaseImageView(m_MSAAColorView);
            if (m_MSAAColorImage.IsValid()) m_Device->GetFactory()->ReleaseImage(m_MSAAColorImage);

            for (auto& ub : m_UboBuffer)
            {
                if (ub.IsValid()) m_Device->GetFactory()->ReleaseBuffer(ub);
            }
            m_UboBuffer.clear();

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
            if (m_MSAAColorView.IsValid()) m_Device->GetFactory()->ReleaseImageView(m_MSAAColorView);
            if (m_MSAAColorImage.IsValid()) m_Device->GetFactory()->ReleaseImage(m_MSAAColorImage);

            m_DepthView = {};
            m_DepthImage = {};
            m_MSAAColorView = {};
            m_MSAAColorImage = {};

            CreateSizeDependentResources();
        }

    private:
        void CreateCommonResources()
        {
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = std::filesystem::path(exePathW).parent_path();

            std::filesystem::path sponzaPath = exeDir / "Assets" / "glTF-Sample-Models" / "2.0" / "Sponza" / "glTF" /
                "Sponza.gltf";
            m_Model = LoadGLTF(sponzaPath.string());

            // Group Primitives by Material
            Containers::Map<SInt32, Containers::Vector<UInt32>> primsByMat;
            for (UInt32 i = 0; i < (UInt32)m_Model.primitives.size(); ++i)
            {
                primsByMat[m_Model.primitives[i].materialIndex].push_back(i);
            }

            // Prepare Indirect Commands Reordered by Material
            m_IndirectCommands.clear();
            m_MaterialGroups.clear();
            for (auto& pair : primsByMat)
            {
                MaterialGroup group;
                group.materialIndex = pair.first;
                group.commandOffset = (UInt32)m_IndirectCommands.size();
                group.commandCount = (UInt32)pair.second.size();
                m_MaterialGroups.push_back(group);

                for (UInt32 primIdx : pair.second)
                {
                    const auto& prim = m_Model.primitives[primIdx];
                    IndirectCommand cmd = {};
                    cmd.indexCount = prim.indexCount;
                    cmd.instanceCount = 1;
                    cmd.firstIndex = prim.firstIndex;
                    cmd.vertexOffset = 0;
                    cmd.firstInstance = 0;
                    m_IndirectCommands.push_back(cmd);
                }
            }

            // Create Indirect Buffer
            RHI::RHIBufferDescriptor indirectDesc = {};
            indirectDesc.size = m_IndirectCommands.size() * sizeof(IndirectCommand);
            indirectDesc.usage = RHI::BUFFER_USAGE_INDIRECT_BUFFER_BIT | RHI::BUFFER_USAGE_TRANSFER_DST_BIT;
            indirectDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
            m_IndirectBuffer = m_Device->GetFactory()->CreateBuffer(std::move(indirectDesc), "IndirectBuffer");

            m_Device->GetFactory()->BufferMemoryCopy(m_IndirectBuffer, m_IndirectCommands.data(),
                                                     m_IndirectCommands.size() * sizeof(IndirectCommand), 0);

            // Count Buffer
            UInt32 count = (UInt32)m_IndirectCommands.size();
            // Assuming count should be the number of indirect commands
            RHI::RHIBufferDescriptor countDesc = {};
            countDesc.size = sizeof(UInt32);
            countDesc.usage = RHI::BUFFER_USAGE_INDIRECT_BUFFER_BIT | RHI::BUFFER_USAGE_TRANSFER_DST_BIT;
            countDesc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;
            m_CountBuffer = m_Device->GetFactory()->CreateBuffer(std::move(countDesc), "CountBuffer");
            m_Device->GetFactory()->BufferMemoryCopy(m_CountBuffer, &count, sizeof(UInt32), 0);

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = sizeof(UniformBufferObject);
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
                m_UboBuffer.push_back(m_Device->GetFactory()->CreateBuffer(std::move(ubDesc), "UBO"));
            }

            m_CameraPos = glm::vec3(0.0f, 1.0f, 0.0f);
            m_CameraRot = glm::vec3(0.0f, 0.0f, 0.0f);
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
            dimgDesc.sampleCount = m_SampleCount;
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

            // MSAA Color Image
            RHI::RHIImageDescriptor msaaDesc = {};
            msaaDesc.imageType = RHI::IMAGE_TYPE_2D;
            msaaDesc.width = width;
            msaaDesc.height = height;
            msaaDesc.depth = 1;
            msaaDesc.mipLevels = 1;
            msaaDesc.arrayLayers = 1;
            msaaDesc.format = RHI::FORMAT_B8G8R8A8_SRGB;
            msaaDesc.tiling = RHI::IMAGE_TILING_OPTIMAL;
            msaaDesc.usage = RHI::IMAGE_USAGE_TRANSIENT_ATTACHMENT_BIT | RHI::IMAGE_USAGE_COLOR_ATTACHMENT_BIT;
            msaaDesc.sampleCount = m_SampleCount;
            msaaDesc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;
            m_MSAAColorImage = m_Device->GetFactory()->CreateImage(std::move(msaaDesc), "MSAAColorBuffer");

            RHI::RHIImageViewDesc msaaViewDesc = {};
            msaaViewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
            msaaViewDesc.format = RHI::FORMAT_B8G8R8A8_SRGB;
            msaaViewDesc.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
            msaaViewDesc.levelCount = 1;
            msaaViewDesc.layerCount = 1;
            msaaViewDesc.width = width;
            msaaViewDesc.height = height;
            m_MSAAColorView = m_Device->GetFactory()->CreateImageView(m_MSAAColorImage, std::move(msaaViewDesc));
        }

    private:
        void InitRenderContext()
        {
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types = {
                    RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER, RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                    RHI::DESCRIPTOR_TYPE_SAMPLER
                };
                UInt32 matCount = (UInt32)m_Model.materials.size();
                if (matCount == 0) matCount = 1;
                Containers::Vector<UInt32> counts = {matCount, matCount, matCount};
                m_DescriptorPoolIds.push_back(m_DescriptorPool->AddPool(types, counts, matCount));
            }
        }

        void CreatePipeline()
        {
            auto pm = m_Device->GetPipelineCache();
            m_Pso = pm->GetPipelineState();

            m_Pso->AddProgram(m_VertProgram);
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
            rs.frontFace = RHI::FRONT_FACE_COUNTER_CLOCKWISE;
            rs.polygonMode = RHI::EPOLYGON_MODE_FILL;
            rs.lineWidth = 1.0f;
            m_Pso->SetRasterizationState(rs);

            RHI::RHIMultisampleState ms{};
            ms.rasterizationSamples = m_SampleCount;
            m_Pso->SetMultisampleState(ms);

            RHI::RHIColorBlendState cb{};
            RHI::RHIColorBlendAttachmentState blendAttachment{};
            blendAttachment.blendEnable = false;
            blendAttachment.colorWriteMask = RHI::COLOR_COMPONENT_R_BIT | RHI::COLOR_COMPONENT_G_BIT |
                RHI::COLOR_COMPONENT_B_BIT | RHI::COLOR_COMPONENT_A_BIT;
            cb.attachments.push_back(blendAttachment);
            m_Pso->SetColorBlendState(cb);

            m_Pso->UpdateDescriptorSet(0, 0, Containers::Vector<RHI::RHIBufferHandle>{m_UboBuffer[0]});

            m_Pso->BuildDescriptorSetLayout();

            RHI::RHIDepthStencilState ds{};
            ds.depthTestEnable = true;
            ds.depthWriteEnable = true;
            ds.depthCompareOp = RHI::COMPARE_OP_LESS;
            m_Pso->SetDepthStencilState(ds);

            m_Pso->SetDynamicStateMask(RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT);

            Containers::Vector<RHI::EFormat> colorFormats = {RHI::FORMAT_B8G8R8A8_SRGB};
            m_Pso->SetRenderingFormats(colorFormats, RHI::FORMAT_D32_SFLOAT, RHI::FORMAT_UNDEFINED);

            m_Pipeline = pm->GetGraphicsPipeline(m_Pso.get());
        }

        void UpdateUniformBuffer()
        {
            UpdateCamera((float)frameTime);
            UniformBufferObject ubo;
            ubo.model = glm::mat4(1.0f);
            ubo.view = GetViewMatrix();
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);
            ubo.projection = GetProjectionMatrix(width / height);
            ubo.mipmapBias = 0.0f;
            m_Device->GetFactory()->BufferMemoryCopy(m_UboBuffer[GetCurrentFrameIndex()], &ubo,
                                                     sizeof(UniformBufferObject), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto pool = m_Device->GetCommandBufferPool(m_CmdPool);
            auto cmdHandle = pool->GetCommandBuffer(currentIndex);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);

            m_DescriptorPool->ResetPool(m_DescriptorPoolIds[currentIndex]);

            Containers::Vector<UInt32> setIndices;
            for (UInt32 i = 0; i < (UInt32)m_Model.materials.size(); ++i)
            {
                auto& mat = m_Model.materials[i];
                m_Pso->UpdateDescriptorSet(0, 0, Containers::Vector<RHI::RHIBufferHandle>{m_UboBuffer[currentIndex]});

                RHI::RHIDescriptorImageInfo texInfo = {};
                texInfo.imageView = mat.baseColorView;
                texInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                m_Pso->UpdateDescriptorSet(0, 1, Containers::Vector<RHI::RHIDescriptorImageInfo>{texInfo});

                RHI::RHIDescriptorImageInfo samInfo = {};
                samInfo.sampler = mat.sampler;
                samInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                m_Pso->UpdateDescriptorSet(0, 2, Containers::Vector<RHI::RHIDescriptorImageInfo>{samInfo});

                UInt32 setIdx = m_DescriptorPool->AllocDescriptorSet(m_DescriptorPoolIds[currentIndex], (UInt32)0,
                                                                     (RHI::RHIPipelineState*)m_Pso.get());
                m_DescriptorPool->UpdateDescriptorSet(m_DescriptorPoolIds[currentIndex], setIdx, m_Pso.get());
                setIndices.push_back(setIdx);
            }

            cmd->Begin(currentIndex, 0);

            auto colorBuffer = m_SwapChain->BeginFrame(currentIndex);
            if (colorBuffer.IsValid())
            {
                auto colorView = m_SwapChain->GetImageView(currentIndex);

                // Transition swapchain image
                cmd->TransitionImageLayout(colorBuffer, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);

                RHI::RHIRenderingAttachmentInfo colorAttachment{};
                colorAttachment.imageView = m_MSAAColorView;
                colorAttachment.imageLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                colorAttachment.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                colorAttachment.storeOp = RHI::ATTACHMENT_STORE_OP_DONT_CARE;
                colorAttachment.clearValue.float32[0] = 0.0f;
                colorAttachment.clearValue.float32[1] = 0.2f;
                colorAttachment.clearValue.float32[2] = 0.2f;
                colorAttachment.clearValue.float32[3] = 1.0f;

                RHI::RHIRenderingAttachmentInfo resolveAttachment{};
                resolveAttachment.imageView = colorView;
                resolveAttachment.imageLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                resolveAttachment.loadOp = RHI::ATTACHMENT_LOAD_OP_DONT_CARE;
                resolveAttachment.storeOp = RHI::ATTACHMENT_STORE_OP_STORE;

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
                renderInfo.pResolveAttachments = &resolveAttachment;
                renderInfo.pDepthAttachment = &depthAttachment;

                cmd->BeginRendering(renderInfo);
                cmd->BindPipeline(m_Pipeline);
                cmd->SetViewport(0, 0, (float)width, (float)height, 0, 1);
                cmd->SetScissor(0, 0, width, height);

                cmd->BindVertexBuffers(m_Model.vertexBuffer, 0);
                cmd->BindIndexBuffer(m_Model.indexBuffer, 0, RHI::INDEX_TYPE_UINT32);

                glm::vec4 tintColor = glm::vec4(1.0f, 1.0f, 1.0f, 1.0f);
                cmd->PushConstants(0, sizeof(glm::vec4), &tintColor,
                                   static_cast<UInt32>(RHI::SHADER_STAGE_FRAGMENT_BIT | RHI::SHADER_STAGE_VERTEX_BIT));

                // DRAW EACH MATERIAL GROUP
                for (const auto& group : m_MaterialGroups)
                {
                    UInt32 setIdx = group.materialIndex >= 0 ? setIndices[group.materialIndex] : 0;
                    cmd->BindDescriptorSet(RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPoolHandle,
                                           m_DescriptorPoolIds[currentIndex], setIdx);

                    cmd->DrawIndexedIndirect(m_IndirectBuffer, group.commandOffset * sizeof(IndirectCommand),
                                             group.commandCount, sizeof(IndirectCommand));
                }

                cmd->EndRendering();

                // Transition swapchain image to PRESENT
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
