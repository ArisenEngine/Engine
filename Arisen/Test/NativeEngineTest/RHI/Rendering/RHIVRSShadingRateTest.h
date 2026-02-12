#pragma once
#include "../RHIRenderingTestBase.h"

namespace ArisenEngine::Testing
{
    class RHIVRSShadingRateTest : public RHIRenderingTestBase
    {
    private:
        RHI_PSOHandle m_Pso = nullptr;
        RHI_PipelineHandle m_Pipeline = 0;
        
        struct UniformBufferObject
        {
            glm::mat4 model;
            glm::mat4 view;
            glm::mat4 proj;
        };
        Containers::Vector<RHI_BufferHandle> m_UboBuffers;

        RHI_ImageHandle m_DepthImage = 0;
        RHI_ImageViewHandle m_DepthView = 0;

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
            std::filesystem::path modelPath = exeDir / "Assets" / "glTF-Sample-Models" / "2.0" / "Sponza" / "glTF" / "Sponza.gltf";
            m_Model = LoadGLTF(modelPath.string());

            CreateResources();
            CreatePipeline();

            m_CameraPos = glm::vec3(0.0f, 1.5f, 5.0f);
            m_CameraRot = glm::vec3(0.0f, -glm::half_pi<float>(), 0.0f);

            return true;
        }

        void TeardownTest() override
        {
            if (m_DepthImage) RHI_Device_ReleaseImage(m_Device, m_DepthImage);
            for (auto& ubo : m_UboBuffers) RHI_Device_ReleaseBuffer(m_Device, ubo);
            if (m_Pso) RHI_PSO_Release(m_Pso);

            RHIRenderingTestBase::TeardownTest();
        }

    protected:
        void RenderFrame() override
        {
            auto currentIndex = GetCurrentFrameIndex();
            if (m_FrameTickets[currentIndex] > 0)
            {
                RHI_Device_WaitQueueTicket(m_Device, m_FrameTickets[currentIndex]);
            }

            UpdateCameraData();
            RecordAndSubmit();
            NextFrame();
        }

    private:
        void CreateResources()
        {
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor uboDesc = {};
                uboDesc.size = sizeof(UniformBufferObject);
                uboDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                uboDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
                m_UboBuffers.push_back(RHI_Device_CreateBuffer(m_Device, &uboDesc, "VRS_UBO"));
            }

            UInt32 width = HAL::GetWindowWidth(m_WindowId);
            UInt32 height = HAL::GetWindowHeight(m_WindowId);

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
            dimgDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
            m_DepthImage = RHI_Device_CreateImage(m_Device, &dimgDesc, "VRS_DepthBuffer");

            RHI::RHIImageViewDesc dviewDesc = {};
            dviewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
            dviewDesc.format = RHI::FORMAT_D32_SFLOAT;
            dviewDesc.aspectMask = RHI::IMAGE_ASPECT_DEPTH_BIT;
            dviewDesc.levelCount = 1;
            dviewDesc.layerCount = 1;
            dviewDesc.width = width;
            dviewDesc.height = height;
            m_DepthView = RHI_Image_AddImageView(m_Device, m_DepthImage, &dviewDesc);
        }

        void CreatePipeline()
        {
            auto pm = RHI_Device_GetPipelineManager(m_Device);
            m_Pso = RHI_PipelineManager_CreatePSO(pm);

            RHI_PSO_AddProgram(m_Pso, m_VertProgram);
            RHI_PSO_AddProgram(m_Pso, m_FragProgram);

            // Match GLTFVertex: pos(3f), normal(3f), uv(2f), color(4f)
            RHI_PSO_AddVertexBindingDescription(m_Pso, 0, sizeof(GLTFVertex), RHI::VERTEX_INPUT_RATE_VERTEX);
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 0, 0, RHI::FORMAT_R32G32B32_SFLOAT, offsetof(GLTFVertex, pos));
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 1, 0, RHI::FORMAT_R32G32B32_SFLOAT, offsetof(GLTFVertex, normal));
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 2, 0, RHI::FORMAT_R32G32_SFLOAT, offsetof(GLTFVertex, uv));
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 3, 0, RHI::FORMAT_R32G32B32A32_SFLOAT, offsetof(GLTFVertex, color));

            RHI::RHIInputAssemblyState ia{};
            ia.topology = RHI::PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
            RHI_PSO_SetInputAssemblyState(m_Pso, &ia);

            RHI::RHIRasterizationState rs{};
            rs.cullMode = RHI::CULL_MODE_BACK_BIT;
            rs.polygonMode = RHI::EPOLYGON_MODE_FILL;
            rs.lineWidth = 1.0f;
            RHI_PSO_SetRasterizationState(m_Pso, &rs);

            RHI::RHIMultisampleState ms{};
            ms.rasterizationSamples = RHI::SAMPLE_COUNT_1_BIT;
            RHI_PSO_SetMultisampleState(m_Pso, &ms);

            RHI::RHIDepthStencilState ds{};
            ds.depthTestEnable = true;
            ds.depthWriteEnable = true;
            ds.depthCompareOp = RHI::COMPARE_OP_LESS_OR_EQUAL;
            RHI_PSO_SetDepthStencilState(m_Pso, &ds);

            RHI::RHIColorBlendState cb{};
            RHI::RHIColorBlendAttachmentState blendAttachment{};
            blendAttachment.blendEnable = false;
            blendAttachment.colorWriteMask = RHI::COLOR_COMPONENT_R_BIT | RHI::COLOR_COMPONENT_G_BIT | RHI::COLOR_COMPONENT_B_BIT | RHI::COLOR_COMPONENT_A_BIT;
            cb.attachments.push_back(blendAttachment);
            RHI_PSO_SetColorBlendState(m_Pso, &cb);

            RHI_PSO_SetDynamicStateMask(m_Pso, RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT | RHI::DYNAMIC_STATE_FRAGMENT_SHADING_RATE_BIT);

            Containers::Vector<RHI::EFormat> colorFormats = { RHI::FORMAT_B8G8R8A8_SRGB };
            RHI_PSO_SetRenderingFormats(m_Pso, &colorFormats, RHI::FORMAT_D32_SFLOAT, RHI::FORMAT_UNDEFINED);

            // Handle descriptors
            Containers::Vector<RHI_BufferHandle> ubos = { m_UboBuffers[0] };
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 0, &ubos);

            // Initial dummy updates to help reflection/layout creation
            {
                Containers::Vector<RHI_BufferHandle> ubos = { m_UboBuffers[0] };
                RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 0, &ubos);

                if (!m_Model.materials.empty())
                {
                    auto& mat = m_Model.materials[0];
                    ArisenEngine::RHI::RHIDescriptorImageInfo imgInfo{};
                    imgInfo.imageView = *reinterpret_cast<ArisenEngine::RHI::RHIImageViewHandle*>(&mat.baseColorView);
                    imgInfo.imageLayout = ArisenEngine::RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                    Containers::Vector<ArisenEngine::RHI::RHIDescriptorImageInfo> imageInfos = { imgInfo };
                    RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 1, &imageInfos);

                    ArisenEngine::RHI::RHIDescriptorImageInfo samplerInfo{};
                    samplerInfo.sampler = *reinterpret_cast<ArisenEngine::RHI::RHISamplerHandle*>(&mat.sampler);
                    Containers::Vector<ArisenEngine::RHI::RHIDescriptorImageInfo> samplerInfos = { samplerInfo };
                    RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 2, &samplerInfos);
                }
            }

            RHI_PSO_BuildDescriptorSetLayout(m_Pso);

            UInt32 matCount = (UInt32)m_Model.materials.size();
            if (matCount == 0) matCount = 1;

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types = { 
                    RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                    RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                    RHI::DESCRIPTOR_TYPE_SAMPLER
                };
                Containers::Vector<UInt32> counts = { 
                    matCount, 
                    matCount,
                    matCount
                };
                m_DescriptorPoolIds.push_back(RHI_DescriptorPool_AddPool(m_DescriptorPool, &types, &counts, matCount));
            }

            m_Pipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_Pso);
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

            RHI_Buffer_MemoryCopy(m_Device, m_UboBuffers[GetCurrentFrameIndex()], &ubo, sizeof(UniformBufferObject), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, currentIndex);

            // Update descriptors for each material
            UInt32 poolId = m_DescriptorPoolIds[currentIndex];
            RHI_DescriptorPool_Reset(m_DescriptorPool, poolId);
            
            Containers::Vector<UInt32> setIndices;
            for (UInt32 i = 0; i < (UInt32)m_Model.materials.size(); ++i)
            {
                auto& mat = m_Model.materials[i];
                Containers::Vector<RHI_BufferHandle> ubos = { m_UboBuffers[currentIndex] };
                RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 0, &ubos);

                ArisenEngine::RHI::RHIDescriptorImageInfo texInfo = {};
                texInfo.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&mat.baseColorView);
                texInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                Containers::Vector<ArisenEngine::RHI::RHIDescriptorImageInfo> texInfos = { texInfo };
                RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 1, &texInfos);

                ArisenEngine::RHI::RHIDescriptorImageInfo samInfo = {};
                samInfo.sampler = *reinterpret_cast<RHI::RHISamplerHandle*>(&mat.sampler);
                Containers::Vector<ArisenEngine::RHI::RHIDescriptorImageInfo> samInfos = { samInfo };
                RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 2, &samInfos);

                UInt32 setIdx = RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, poolId, 0, m_Pso);
                RHI_DescriptorPool_UpdateDescriptorSet(m_DescriptorPool, poolId, setIdx, m_Pso);
                setIndices.push_back(setIdx);
            }

            RHI_Cmd_Begin(cmd, currentIndex, 0);

            auto colorBuffer = RHI_SwapChain_BeginFrame(m_SwapChain, currentIndex);
            if (colorBuffer)
            {
                auto colorView = RHI_SwapChain_GetImageView(m_SwapChain, currentIndex);
                RHI_Cmd_TransitionImageLayout(cmd, colorBuffer, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);

                RHI::RHIRenderingAttachmentInfo colorAttachment {};
                colorAttachment.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&colorView);
                colorAttachment.imageLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                colorAttachment.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                colorAttachment.storeOp = RHI::ATTACHMENT_STORE_OP_STORE;
                colorAttachment.clearValue.float32[0] = 0.05f;
                colorAttachment.clearValue.float32[1] = 0.05f;
                colorAttachment.clearValue.float32[2] = 0.05f;
                colorAttachment.clearValue.float32[3] = 1.0f;

                RHI::RHIRenderingAttachmentInfo depthAttachment {};
                depthAttachment.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&m_DepthView);
                depthAttachment.imageLayout = RHI::IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;
                depthAttachment.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                depthAttachment.storeOp = RHI::ATTACHMENT_STORE_OP_DONT_CARE;
                depthAttachment.clearValue.float32[0] = 1.0f;
                depthAttachment.clearValue.float32[1] = 0;

                UInt32 width = HAL::GetWindowWidth(m_WindowId);
                UInt32 height = HAL::GetWindowHeight(m_WindowId);

                RHI::RHIRenderingInfo renderInfo {};
                renderInfo.RHIRenderArea = { 0, 0, width, height };
                renderInfo.layerCount = 1;
                renderInfo.colorAttachmentCount = 1;
                renderInfo.pColorAttachments = &colorAttachment;
                renderInfo.pDepthAttachment = &depthAttachment;

                RHI_Cmd_BeginRendering(cmd, &renderInfo);
                RHI_Cmd_BindPipeline(cmd, m_Pipeline);
                
                RHI_Cmd_BindVertexBuffers(cmd, m_Model.vertexBuffer, 0);
                RHI_Cmd_BindIndexBuffer(cmd, m_Model.indexBuffer, 0, RHI::INDEX_TYPE_UINT32);

                RHI::EShadingRate rates[] = {
                    RHI::EShadingRate::Rate1x1,
                    RHI::EShadingRate::Rate2x2,
                    RHI::EShadingRate::Rate4x4
                };
                RHI::EShadingRateCombiner combiners[2] = { RHI::EShadingRateCombiner::Keep, RHI::EShadingRateCombiner::Keep };

                for (int i = 0; i < 3; ++i)
                {
                    float quadWidth = (float)width / 3.0f;
                    float xPos = i * quadWidth;
                    
                    RHI_Cmd_SetViewport(cmd, xPos, 0, quadWidth, (float)height, 0, 1);
                    RHI_Cmd_SetScissor(cmd, (UInt32)xPos, 0, (UInt32)quadWidth, height);
                    RHI_Cmd_SetFragmentShadingRate(cmd, rates[i], combiners);
                    
                    for (const auto& prim : m_Model.primitives)
                    {
                        UInt32 setIdx = prim.materialIndex >= 0 ? setIndices[prim.materialIndex] : 0;
                        RHI_Cmd_BindDescriptorSet_FromPool(cmd, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPool, poolId, setIdx);
                        RHI_Cmd_DrawIndexed(cmd, prim.indexCount, 1, prim.firstIndex, 0, 0, 0);
                    }
                }

                RHI_Cmd_EndRendering(cmd);
                RHI_Cmd_TransitionImageLayout(cmd, colorBuffer, RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR);
            }

            RHI_Cmd_End(cmd);

            RHI::RHISubmitDescriptor submitDesc = {};
            submitDesc.WaitSwapChain = reinterpret_cast<RHI::RHISwapChain*>(m_SwapChain);
            submitDesc.SignalSwapChain = reinterpret_cast<RHI::RHISwapChain*>(m_SwapChain);
            
            m_FrameTickets[currentIndex] = RHI_Device_Submit(m_Device, cmd, reinterpret_cast<const ::RHISubmitDescriptor*>(&submitDesc));
            RHI_SwapChain_EndFrame(m_SwapChain, currentIndex);
            RHI_Device_ReleaseCommandBuffer(m_Device, m_CmdPool, currentIndex, cmd);
        }

    };
}

