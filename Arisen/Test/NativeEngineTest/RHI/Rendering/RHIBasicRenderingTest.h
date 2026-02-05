#pragma once

#include "../RHIRenderingTestBase.h"



namespace ArisenEngine::Testing
{
    class RHIBasicRenderingTest : public RHIRenderingTestBase
    {
    private:
        RHI_PSOHandle m_Pso = nullptr;
        RHI_PipelineHandle m_Pipeline = 0;
        Containers::Vector<RHI_BufferHandle> m_UboBuffer;
        RHI_ImageHandle m_DepthImage = 0;
        RHI_ImageViewHandle m_DepthView = 0;
        RHI_ImageHandle m_Texture = 0;
        RHI_SamplerHandle m_Sampler = 0;
        
        RHI_ImageHandle m_MSAAColorImage = 0;
        RHI_ImageViewHandle m_MSAAColorView = 0;
        RHI::ESampleCountFlagBits m_SampleCount = RHI::SAMPLE_COUNT_4_BIT;

    public:
        const char* GetName() const override { return "BasicRenderingTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            RHIRenderingTestBase::SetupTest();

            InitCommonResources();
            InitShaderProgram(L"StandardTest");
            CreateResources();
            InitRenderContext();
            CreatePipeline();

            return true;
        }

        void TeardownTest() override
        {
            if (m_Sampler) RHI_Device_ReleaseSampler(m_Device, m_Sampler);
            if (m_Texture) RHI_Device_ReleaseImage(m_Device, m_Texture);
            if (m_DepthImage) RHI_Device_ReleaseImage(m_Device, m_DepthImage);
            if (m_MSAAColorImage) RHI_Device_ReleaseImage(m_Device, m_MSAAColorImage);

            for (auto& ub : m_UboBuffer)
            {
                if (ub) RHI_Device_ReleaseBuffer(m_Device, ub);
            }
            m_UboBuffer.clear();

            if (m_Pso) RHI_PSO_Release(m_Pso);
            
            m_Model.Release(m_Device);

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

            UpdateUniformBuffer();
            RecordAndSubmit();

            NextFrame();
        }

    private:
        void CreateResources()
        {
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = std::filesystem::path(exePathW).parent_path();
            
            std::filesystem::path sponzaPath = exeDir / "Assets" / "glTF-Sample-Models" / "2.0" / "Sponza" / "glTF" / "Sponza.gltf";
            m_Model = LoadGLTF(sponzaPath.string());

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = sizeof(UniformBufferObject);
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
                m_UboBuffer.push_back(RHI_Device_CreateBuffer(m_Device, &ubDesc, "UBO"));
            }

            UInt32 width = HAL::GetWindowWidth(m_WindowId);
            UInt32 height = HAL::GetWindowHeight(m_WindowId);

            if (width == 0 || height == 0)
            {
                LOG_WARNF("[RHIBasicRenderingTest]: Window dimensions are zero ({0}x{1}) during CreateResources. Falling back to 1280x720.", width, height);
                width = 1280;
                height = 720;
            }

            LOG_INFOF("[RHIBasicRenderingTest]: CreateResources Window Size: {0}x{1} (ID={2})", width, height, (unsigned)m_WindowId);

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
            dimgDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
            m_DepthImage = RHI_Device_CreateImage(m_Device, &dimgDesc, "DepthBuffer");

            RHI::RHIImageViewDesc dviewDesc = {};
            dviewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
            dviewDesc.format = RHI::FORMAT_D32_SFLOAT;
            dviewDesc.aspectMask = RHI::IMAGE_ASPECT_DEPTH_BIT;
            dviewDesc.levelCount = 1;
            dviewDesc.layerCount = 1;
            dviewDesc.width = width;
            dviewDesc.height = height;
            m_DepthView = RHI_Image_AddImageView(m_Device, m_DepthImage, &dviewDesc);

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
            msaaDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
            m_MSAAColorImage = RHI_Device_CreateImage(m_Device, &msaaDesc, "MSAAColorBuffer");

            RHI::RHIImageViewDesc msaaViewDesc = {};
            msaaViewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
            msaaViewDesc.format = RHI::FORMAT_B8G8R8A8_SRGB;
            msaaViewDesc.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
            msaaViewDesc.levelCount = 1;
            msaaViewDesc.layerCount = 1;
            msaaViewDesc.width = width;
            msaaViewDesc.height = height;
            m_MSAAColorView = RHI_Image_AddImageView(m_Device, m_MSAAColorImage, &msaaViewDesc);

            // Set a better camera position for Sponza
            m_CameraPos = glm::vec3(0.0f, 1.0f, 0.0f);
            m_CameraRot = glm::vec3(0.0f, 0.0f, 0.0f);
        }


        void InitRenderContext()
        {
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types = { RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER, RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE, RHI::DESCRIPTOR_TYPE_SAMPLER };
                UInt32 matCount = (UInt32)m_Model.materials.size();
                if (matCount == 0) matCount = 1;
                Containers::Vector<UInt32> counts = { matCount, matCount, matCount };
                m_DescriptorPoolIds.push_back(RHI_DescriptorPool_AddPool(m_DescriptorPool, &types, &counts, matCount));
            }
        }

        void CreatePipeline()
        {
            auto pm = RHI_Device_GetPipelineManager(m_Device);
            m_Pso = RHI_PipelineManager_CreatePSO(pm);

            RHI_PSO_AddProgram(m_Pso, m_VertProgram);
            RHI_PSO_AddProgram(m_Pso, m_FragProgram);

            RHI_PSO_AddVertexBindingDescription(m_Pso, 0, m_Model.layout.stride, RHI::VERTEX_INPUT_RATE_VERTEX);
            for (const auto& attr : m_Model.layout.attributes)
            {
                RHI_PSO_AddVertexInputAttributeDescription(m_Pso, attr.location, 0, attr.format, attr.offset);
            }

            RHI::RHIInputAssemblyState ia{};
            ia.topology = RHI::PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
            RHI_PSO_SetInputAssemblyState(m_Pso, &ia);

            RHI::RHIRasterizationState rs{};
            rs.cullMode = RHI::CULL_MODE_NONE;
            rs.frontFace = RHI::FRONT_FACE_COUNTER_CLOCKWISE;
            rs.polygonMode = RHI::EPOLYGON_MODE_FILL;
            rs.lineWidth = 1.0f;
            RHI_PSO_SetRasterizationState(m_Pso, &rs);

            RHI::RHIMultisampleState ms{};
            ms.rasterizationSamples = m_SampleCount;
            RHI_PSO_SetMultisampleState(m_Pso, &ms);

            RHI::RHIColorBlendState cb{};
            RHI::RHIColorBlendAttachmentState blendAttachment{};
            blendAttachment.blendEnable = false;
            blendAttachment.colorWriteMask = RHI::COLOR_COMPONENT_R_BIT | RHI::COLOR_COMPONENT_G_BIT | RHI::COLOR_COMPONENT_B_BIT | RHI::COLOR_COMPONENT_A_BIT;
            cb.attachments.push_back(blendAttachment);
            RHI_PSO_SetColorBlendState(m_Pso, &cb);

            Containers::Vector<RHI::RHIBufferHandle> buffers;
            buffers.push_back(*reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffer[0]));
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 0, &buffers);

            RHI_PSO_BuildDescriptorSetLayout(m_Pso);

            RHI::RHIDepthStencilState ds{};
            ds.depthTestEnable = true;
            ds.depthWriteEnable = true;
            ds.depthCompareOp = RHI::COMPARE_OP_LESS;
            RHI_PSO_SetDepthStencilState(m_Pso, &ds);

            RHI_PSO_SetDynamicStateMask(m_Pso, RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT);

            Containers::Vector<RHI::EFormat> colorFormats = { RHI::FORMAT_B8G8R8A8_SRGB };
            RHI_PSO_SetRenderingFormats(m_Pso, &colorFormats, RHI::FORMAT_D32_SFLOAT, RHI::FORMAT_UNDEFINED);

            m_Pipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_Pso);
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
            ubo.mipmapBias = 0.0f; // Default No bias
            RHI_Buffer_MemoryCopy(m_Device, m_UboBuffer[GetCurrentFrameIndex()], &ubo, sizeof(UniformBufferObject), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, currentIndex);

            // Update descriptors for each material
            RHI_DescriptorPool_Reset(m_DescriptorPool, m_DescriptorPoolIds[currentIndex]);
            
            for (UInt32 i = 0; i < m_Model.materials.size(); ++i)
            {
                auto& mat = m_Model.materials[i];
                Containers::Vector<RHI::RHIBufferHandle> ubos = { *reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffer[currentIndex]) };
                RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 0, &ubos);

                RHI::RHIDescriptorImageInfo texInfo = {};
                texInfo.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&mat.baseColorView);
                texInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                Containers::Vector<RHI::RHIDescriptorImageInfo> texInfos = { texInfo };
                RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 1, &texInfos);

                RHI::RHIDescriptorImageInfo samInfo = {};
                samInfo.sampler = *reinterpret_cast<RHI::RHISamplerHandle*>(&mat.sampler);
                samInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                Containers::Vector<RHI::RHIDescriptorImageInfo> samInfos = { samInfo };
                RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 2, &samInfos);

                UInt32 setIdx = RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[currentIndex], 0, m_Pso);
                RHI_DescriptorPool_UpdateDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[currentIndex], setIdx, m_Pso);
            }

            RHI_Cmd_Begin(cmd, currentIndex, 0);

            auto colorBuffer = RHI_SwapChain_BeginFrame(m_SwapChain, currentIndex);
            if (colorBuffer)
            {
                auto colorView = RHI_SwapChain_GetImageView(m_SwapChain, currentIndex);
                RHI::RHIImageHandle colorImage = *reinterpret_cast<RHI::RHIImageHandle*>(&colorBuffer);

                // Transition swapchain image: UNDEFINED -> COLOR_ATTACHMENT_OPTIMAL
                {
                    RHI::RHIImageMemoryBarrier barrier = {};
                    barrier.srcAccess = RHI::ACCESS_NONE;
                    barrier.dstAccess = RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
                    barrier.oldLayout = RHI::IMAGE_LAYOUT_UNDEFINED;
                    barrier.newLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                    barrier.srcQueueFamilyIndex = 0xFFFFFFFF;
                    barrier.dstQueueFamilyIndex = 0xFFFFFFFF;
                    barrier.image = colorImage;
                    barrier.subresourceRange = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
                    barrier.srcStageMask = RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT;
                    barrier.dstStageMask = RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;

                    Containers::Vector<RHI::RHIImageMemoryBarrier> barriers = { barrier };
                    RHI_Cmd_PipelineBarrier_Image(cmd, RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, 0, &barriers);
                }

                RHI::RHIRenderingAttachmentInfo colorAttachment {};
                colorAttachment.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&m_MSAAColorView);
                colorAttachment.imageLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                colorAttachment.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                colorAttachment.storeOp = RHI::ATTACHMENT_STORE_OP_DONT_CARE;
                colorAttachment.clearValue.float32[0] = 0.0f;
                colorAttachment.clearValue.float32[1] = 0.0f;
                colorAttachment.clearValue.float32[2] = 0.2f;
                colorAttachment.clearValue.float32[3] = 1.0f;

                RHI::RHIRenderingAttachmentInfo resolveAttachment {};
                resolveAttachment.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&colorView);
                resolveAttachment.imageLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                resolveAttachment.loadOp = RHI::ATTACHMENT_LOAD_OP_DONT_CARE;
                resolveAttachment.storeOp = RHI::ATTACHMENT_STORE_OP_STORE;

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
                renderInfo.pResolveAttachments = &resolveAttachment;
                renderInfo.pDepthAttachment = &depthAttachment;

                RHI_Cmd_BeginRendering(cmd, &renderInfo);
                RHI_Cmd_BindPipeline(cmd, m_Pipeline);
                RHI_Cmd_SetViewport(cmd, 0, 0, (float)width, (float)height, 0, 1);
                RHI_Cmd_SetScissor(cmd, 0, 0, width, height);
                
                RHI_Cmd_BindVertexBuffers(cmd, m_Model.vertexBuffer, 0);
                RHI_Cmd_BindIndexBuffer(cmd, m_Model.indexBuffer, 0, RHI::INDEX_TYPE_UINT32);

                for (const auto& prim : m_Model.primitives)
                {
                    UInt32 setIdx = prim.materialIndex >= 0 ? (UInt32)prim.materialIndex : 0;
                    RHI_Cmd_BindDescriptorSet_FromPool(cmd, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPool, m_DescriptorPoolIds[currentIndex], setIdx);
                    RHI_Cmd_DrawIndexed(cmd, prim.indexCount, 1, prim.firstIndex, 0, 0, 0);
                }
                RHI_Cmd_EndRendering(cmd);

                // Transition swapchain image: COLOR_ATTACHMENT_OPTIMAL -> PRESENT_SRC_KHR
                {
                    RHI::RHIImageMemoryBarrier barrier = {};
                    barrier.srcAccess = RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
                    barrier.dstAccess = RHI::ACCESS_NONE;
                    barrier.oldLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                    barrier.newLayout = RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR;
                    barrier.srcQueueFamilyIndex = 0xFFFFFFFF;
                    barrier.dstQueueFamilyIndex = 0xFFFFFFFF;
                    barrier.image = colorImage;
                    barrier.subresourceRange = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
                    barrier.srcStageMask = RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
                    barrier.dstStageMask = RHI::PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT;

                    Containers::Vector<RHI::RHIImageMemoryBarrier> barriers = { barrier };
                    RHI_Cmd_PipelineBarrier_Image(cmd, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, RHI::PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT, 0, &barriers);
                }
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
