#pragma once

#include "../RHIRenderingTestBase.h"



namespace ArisenEngine::Testing
{
    class RHIDepthBufferingTest : public RHIRenderingTestBase
    {
    private:
        RHI_PSOHandle m_Pso = nullptr;
        RHI_PipelineHandle m_Pipeline = 0;
        Containers::Vector<RHI_BufferHandle> m_UboBuffer;
        RHI_ImageHandle m_DepthImage = 0;
        RHI_ImageViewHandle m_DepthView = 0;
        RHI_ImageHandle m_Texture = 0;
        RHI_SamplerHandle m_Sampler = 0;
        RHI_SubpassHandle m_Subpass = 0;

    public:
        const char* GetName() const override { return "DepthBufferingTest"; }
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
            // if (m_DepthView) RHI_Device_ReleaseImageView(m_Device, m_DepthImage, m_DepthView);
            if (m_DepthImage) RHI_Device_ReleaseImage(m_Device, m_DepthImage);
            
            for (auto& ub : m_UboBuffer)
            {
                if (ub) RHI_Device_ReleaseBuffer(m_Device, ub);
            }
            m_UboBuffer.clear();

            if (m_Pso) RHI_PSO_Destroy(m_Pso);
            
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
            
            m_Model = LoadGLTF((exeDir / "Assets" / "Buggy.gltf").string());

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = sizeof(UniformBufferObject);
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
                m_UboBuffer.push_back(RHI_Device_CreateBuffer(m_Device, &ubDesc, "UBO"));
            }

            // Depth Image
            RHI::RHIImageDescriptor dimgDesc = {};
            dimgDesc.imageType = RHI::IMAGE_TYPE_2D;
            dimgDesc.width = HAL::GetWindowWidth(m_WindowId);
            dimgDesc.height = HAL::GetWindowHeight(m_WindowId);
            dimgDesc.depth = 1;
            dimgDesc.mipLevels = 1;
            dimgDesc.arrayLayers = 1;
            dimgDesc.format = RHI::FORMAT_D32_SFLOAT;
            dimgDesc.tiling = RHI::IMAGE_TILING_OPTIMAL;
            dimgDesc.usage = RHI::IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT;
            dimgDesc.sampleCount = RHI::SAMPLE_COUNT_1_BIT;
            dimgDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
            m_DepthImage = RHI_Device_CreateImage(m_Device, &dimgDesc, "DepthBuffer");

            RHI::RHIImageViewDesc dviewDesc = {};
            dviewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
            dviewDesc.format = RHI::FORMAT_D32_SFLOAT;
            dviewDesc.aspectMask = RHI::IMAGE_ASPECT_DEPTH_BIT;
            dviewDesc.levelCount = 1;
            dviewDesc.layerCount = 1;
            m_DepthView = RHI_Image_AddImageView(m_Device, m_DepthImage, &dviewDesc);

            // Texture
            int texWidth, texHeight, texChannels;
            auto texturePath = (exeDir / "Assets" / "Arisen.png").string();
            stbi_uc* pixels = stbi_load(texturePath.c_str(), &texWidth, &texHeight, &texChannels, STBI_rgb_alpha);
            if (pixels)
            {
                RHI::RHIImageDescriptor texDesc = {};
                texDesc.imageType = RHI::IMAGE_TYPE_2D;
                texDesc.width = texWidth;
                texDesc.height = texHeight;
                texDesc.depth = 1;
                texDesc.mipLevels = 1;
                texDesc.arrayLayers = 1;
                texDesc.format = RHI::FORMAT_R8G8B8A8_SRGB;
                texDesc.tiling = RHI::IMAGE_TILING_OPTIMAL;
                texDesc.usage = RHI::IMAGE_USAGE_TRANSFER_DST_BIT | RHI::IMAGE_USAGE_SAMPLED_BIT;
                texDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
                m_Texture = RHI_Device_CreateImage(m_Device, &texDesc, "Texture");

                RHI::RHIImageViewDesc viewDesc = {};
                viewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
                viewDesc.format = RHI::FORMAT_R8G8B8A8_SRGB;
                viewDesc.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
                viewDesc.levelCount = 1;
                viewDesc.layerCount = 1;
                RHI_Image_AddImageView(m_Device, m_Texture, &viewDesc);

                UploadImage(m_Texture, (UInt64)texWidth * texHeight * 4, pixels, texWidth, texHeight);
                stbi_image_free(pixels);
            }

            RHI::RHISamplerDesc sampDesc = {};
            sampDesc.magFilter = RHI::FILTER_LINEAR;
            sampDesc.minFilter = RHI::FILTER_LINEAR;
            m_Sampler = RHI_Device_CreateSampler(m_Device, &sampDesc);
        }

        void InitRenderContext()
        {
            // Color attachment
            RHI_RenderPass_AddAttachmentAction(m_Device, m_RenderPass,
                RHI::FORMAT_B8G8R8A8_SRGB,
                RHI::SAMPLE_COUNT_1_BIT,
                RHI::ATTACHMENT_LOAD_OP_CLEAR,
                RHI::ATTACHMENT_STORE_OP_STORE,
                RHI::ATTACHMENT_LOAD_OP_DONT_CARE,
                RHI::ATTACHMENT_STORE_OP_DONT_CARE,
                RHI::IMAGE_LAYOUT_UNDEFINED,
                RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR);

            // Depth attachment
            RHI_RenderPass_AddAttachmentAction(m_Device, m_RenderPass,
                RHI::FORMAT_D32_SFLOAT,
                RHI::SAMPLE_COUNT_1_BIT,
                RHI::ATTACHMENT_LOAD_OP_CLEAR,
                RHI::ATTACHMENT_STORE_OP_DONT_CARE,
                RHI::ATTACHMENT_LOAD_OP_DONT_CARE,
                RHI::ATTACHMENT_STORE_OP_DONT_CARE,
                RHI::IMAGE_LAYOUT_UNDEFINED,
                RHI::IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL);

            m_Subpass = RHI_RenderPass_AddSubPass(m_Device, m_RenderPass);
            RHI_Subpass_SetBindPoint(m_Subpass, RHI::PIPELINE_BIND_POINT_GRAPHICS);
            RHI_Subpass_AddColorReference(m_Subpass, 0, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);
            RHI_Subpass_SetDepthStencilReference(m_Subpass, 1, RHI::IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL);

            RHI_Subpass_SetDependency(m_Subpass, u32Invalid,
                RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT | RHI::PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | RHI::PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT,
                RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT | RHI::ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT,
                RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT | RHI::PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | RHI::PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT,
                RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT | RHI::ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT, 0);

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI_RenderPass_Alloc(m_Device, m_RenderPass, i);
            }

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types = { RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER, RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE, RHI::DESCRIPTOR_TYPE_SAMPLER };
                Containers::Vector<UInt32> counts = { 1, 1, 1 };
                m_DescriptorPoolIds.push_back(RHI_DescriptorPool_AddPool(m_DescriptorPool, &types, &counts, 1));
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

            Containers::Vector<RHI::RHIBufferHandle> buffers;
            buffers.push_back(*reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffer[0]));
            RHI_PSO_AddDescriptorSetLayoutBinding_Buffers(m_Pso, 0, 0, RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER, 1, RHI::SHADER_STAGE_VERTEX_BIT, &buffers);

            RHI_PSO_AddDescriptorSetLayoutBinding_Images(m_Pso, 0, 1, RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE, 1, RHI::SHADER_STAGE_FRAGMENT_BIT, nullptr);
            RHI_PSO_AddDescriptorSetLayoutBinding_Images(m_Pso, 0, 2, RHI::DESCRIPTOR_TYPE_SAMPLER, 1, RHI::SHADER_STAGE_FRAGMENT_BIT, nullptr);

            RHI_PSO_BuildDescriptorSetLayout(m_Pso);

            RHI_PSO_AddDynamicState(m_Pso, RHI::DYNAMIC_STATE_VIEWPORT);
            RHI_PSO_AddDynamicState(m_Pso, RHI::DYNAMIC_STATE_SCISSOR);
            
            RHI::RHIDepthStencilState ds{};
            ds.depthTestEnable = true;
            ds.depthWriteEnable = true;
            ds.depthCompareOp = RHI::COMPARE_OP_LESS;
            RHI_PSO_SetDepthStencilState(m_Pso, &ds);

            m_Pipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_Pso);
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i) {
                RHI_Pipeline_AllocGraphics(m_Device, m_Pipeline, i, m_Subpass);
            }
        }

        void UpdateUniformBuffer()
        {
            UpdateCamera((float)frameTime);
            UniformBufferObject ubo;
            ubo.model = glm::mat4(1.0f);
            ubo.view = GetViewMatrix();
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);
            ubo.proj = GetProjectionMatrix(width / height);
            RHI_Buffer_MemoryCopy(m_Device, m_UboBuffer[m_FrameIndex], &ubo, 0);
        }

        void RecordAndSubmit()
        {
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, m_FrameIndex);

            // Update descriptors
            Containers::Vector<RHI::RHIBufferHandle> ubos = { *reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffer[m_FrameIndex]) };
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 0, &ubos);

            auto texView = RHI_Image_GetView(m_Device, m_Texture);
            RHI::RHIDescriptorImageInfo texInfo = {};
            texInfo.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&texView);
            texInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
            Containers::Vector<RHI::RHIDescriptorImageInfo> texInfos = { texInfo };
            RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 1, &texInfos);

            RHI::RHIDescriptorImageInfo samInfo = {};
            samInfo.sampler = *reinterpret_cast<RHI::RHISamplerHandle*>(&m_Sampler);
            samInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
            Containers::Vector<RHI::RHIDescriptorImageInfo> samInfos = { samInfo };
            RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 2, &samInfos);

            RHI_DescriptorPool_Reset(m_DescriptorPool, m_DescriptorPoolIds[m_FrameIndex]);
            RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[m_FrameIndex], 0, m_Pso);
            RHI_DescriptorPool_UpdateDescriptorSets(m_DescriptorPool, m_DescriptorPoolIds[m_FrameIndex], m_Pso);

            RHI_Cmd_Begin(cmd, m_FrameIndex, 0);

            auto surface = RHI_Instance_GetSurface(m_Instance, m_WindowId);
            auto swapchain = RHI_Surface_GetSwapChain(surface);
            auto colorBuffer = RHI_SwapChain_AquireCurrentImage(swapchain, m_FrameIndex);
            if (colorBuffer)
            {
                auto colorView = RHI_SwapChain_GetImageView(swapchain, m_FrameIndex);
                RHI_RenderPass_Alloc(m_Device, m_RenderPass, m_FrameIndex);
                RHI_FrameBuffer_SetAttachment(m_Device, m_FrameBuffer, m_FrameIndex, colorView, m_RenderPass, 0);
                RHI_FrameBuffer_SetAttachment(m_Device, m_FrameBuffer, m_FrameIndex, m_DepthView, m_RenderPass, 1);

                RHI::RHIClearValue clearValues[2];
                clearValues[0].color[0] = 0.0f; clearValues[0].color[1] = 0.0f; clearValues[0].color[2] = 0.2f; clearValues[0].color[3] = 1.0f;
                clearValues[1].depthStencil.depth = 1.0f; clearValues[1].depthStencil.stencil = 0;

                RHI::RenderPassBeginDesc rpBegin = {
                    *reinterpret_cast<RHI::RHIRenderPassHandle*>(&m_RenderPass),
                    *reinterpret_cast<RHI::RHIFrameBufferHandle*>(&m_FrameBuffer),
                    RHI::SUBPASS_CONTENTS_INLINE,
                    2,
                    clearValues
                };

                RHI_Cmd_BeginRenderPass(cmd, m_FrameIndex, &rpBegin);
                RHI_Cmd_BindPipeline(cmd, m_FrameIndex, m_Pipeline);
                UInt32 width = HAL::GetWindowWidth(m_WindowId);
                UInt32 height = HAL::GetWindowHeight(m_WindowId);
                RHI_Cmd_SetViewport(cmd, 0, 0, (float)width, (float)height, 0, 1);
                RHI_Cmd_SetScissor(cmd, 0, 0, width, height);
                RHI_Cmd_BindDescriptorSets_FromPool(cmd, m_FrameIndex, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPool, m_DescriptorPoolIds[m_FrameIndex]);
                RHI_Cmd_BindVertexBuffers(cmd, m_Model.vertexBuffer, 0);
                RHI_Cmd_BindIndexBuffer(cmd, m_Model.indexBuffer, 0, RHI::INDEX_TYPE_UINT32);
                RHI_Cmd_DrawIndexed(cmd, m_Model.indexCount, 1, 0, 0, 0, 0);
                RHI_Cmd_EndRenderPass(cmd);

                auto imageAvailableSem = RHI_SwapChain_GetImageAvailableSemaphore(swapchain, m_FrameIndex);
                auto renderFinishedSem = RHI_SwapChain_GetRenderFinishSemaphore(swapchain, m_FrameIndex);
                RHI_Cmd_WaitSemaphore(cmd, imageAvailableSem, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT);
                RHI_Cmd_SignalSemaphore(cmd, renderFinishedSem);
            }

            RHI_Cmd_End(cmd);
            m_FrameTickets[m_FrameIndex] = RHI_Device_Submit(m_Device, cmd, m_FrameIndex);
            RHI_SwapChain_Present(swapchain, m_FrameIndex);
            RHI_Device_ReleaseCommandBuffer(m_Device, m_CmdPool, m_FrameIndex, cmd);
        }
    };
}
